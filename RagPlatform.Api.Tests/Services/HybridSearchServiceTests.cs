using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class HybridSearchServiceTests
{
    [TestMethod]
    public async Task SearchAsync_DuplicateChunkIds_ReturnsOneMergedResult()
    {
        var chunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(
            CreateVectorResponse(
                CreateVectorResult(chunkId, documentId, "vector snippet"),
                CreateVectorResult(chunkId, documentId, "duplicate vector snippet")));
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(chunkId, documentId, "full text content"),
            CreateFullTextResult(chunkId, documentId, "duplicate full text content"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(chunkId, results[0].ChunkId);
        Assert.AreEqual("full text content", results[0].Content);
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
    }

    [TestMethod]
    public async Task SearchAsync_CandidatesFromBothServices_MarksOverlapAndSource()
    {
        var overlapId = Guid.NewGuid();
        var vectorOnlyId = Guid.NewGuid();
        var textOnlyId = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(overlapId, Guid.NewGuid(), "overlap snippet"),
            CreateVectorResult(vectorOnlyId, Guid.NewGuid(), "vector content")));
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(overlapId, vectorService.Results.Results[0].DocumentId, "overlap full content"),
            CreateFullTextResult(textOnlyId, Guid.NewGuid(), "text content"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query");

        Assert.AreEqual(3, results.Count);
        var resultsByChunkId = results.ToDictionary(result => result.ChunkId);
        Assert.IsTrue(resultsByChunkId[overlapId].FoundByVectorSearch);
        Assert.IsTrue(resultsByChunkId[overlapId].FoundByFullTextSearch);
        Assert.IsTrue(resultsByChunkId[vectorOnlyId].FoundByVectorSearch);
        Assert.IsFalse(resultsByChunkId[vectorOnlyId].FoundByFullTextSearch);
        Assert.IsFalse(resultsByChunkId[textOnlyId].FoundByVectorSearch);
        Assert.IsTrue(resultsByChunkId[textOnlyId].FoundByFullTextSearch);
    }

    [TestMethod]
    public async Task SearchAsync_OverlappingCandidate_RanksAboveSingleSourceCandidates()
    {
        var overlap = Guid.NewGuid();
        var vectorOnly = Guid.NewGuid();
        var textOnly = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(vectorOnly, Guid.NewGuid(), "vector content"),
            CreateVectorResult(overlap, Guid.NewGuid(), "overlap snippet")));
        var overlapDocumentId = vectorService.Results.Results[1].DocumentId;
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(textOnly, Guid.NewGuid(), "text content"),
            CreateFullTextResult(overlap, overlapDocumentId, "overlap content"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query", topK: 3);

        var expectedOverlapScore = 1d / (60 + 2) + 1d / (60 + 2);
        var expectedSingleSourceScore = 1d / (60 + 1);
        Assert.IsGreaterThan(expectedSingleSourceScore, expectedOverlapScore);
        Assert.AreEqual(overlap, results[0].ChunkId);
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_VectorOnlyCandidates_UseTheirVectorRanks()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(first, Guid.NewGuid(), "first vector result"),
            CreateVectorResult(second, Guid.NewGuid(), "second vector result")));
        var service = CreateService(vectorService, new FakeFullTextSearchService());

        var results = await service.SearchAsync("query");

        CollectionAssert.AreEqual(new[] { first, second }, results.Select(result => result.ChunkId).ToArray());
        Assert.IsTrue(results.All(result => result.FoundByVectorSearch && !result.FoundByFullTextSearch));
    }

    [TestMethod]
    public async Task SearchAsync_FullTextOnlyCandidates_UseTheirFullTextRanks()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(first, Guid.NewGuid(), "first text result"),
            CreateFullTextResult(second, Guid.NewGuid(), "second text result"));
        var service = CreateService(new FakeVectorSearchService(CreateVectorResponse()), fullTextService);

        var results = await service.SearchAsync("query");

        CollectionAssert.AreEqual(new[] { first, second }, results.Select(result => result.ChunkId).ToArray());
        Assert.IsTrue(results.All(result => !result.FoundByVectorSearch && result.FoundByFullTextSearch));
    }

    [TestMethod]
    public async Task SearchAsync_EqualRrfScores_UsesChunkIdAsDeterministicTieBreaker()
    {
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(higherId, Guid.NewGuid(), "vector candidate")));
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(lowerId, Guid.NewGuid(), "text candidate"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query");

        CollectionAssert.AreEqual(new[] { lowerId, higherId }, results.Select(result => result.ChunkId).ToArray());
    }

    [TestMethod]
    public async Task SearchAsync_MixedRanks_UsesExpectedRrfCalculations()
    {
        var vectorFirstTextThird = Guid.NewGuid();
        var vectorSecondTextFirst = Guid.NewGuid();
        var vectorOnly = Guid.NewGuid();
        var fullTextOnly = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(vectorFirstTextThird, Guid.NewGuid(), "vector rank one"),
            CreateVectorResult(vectorSecondTextFirst, Guid.NewGuid(), "vector rank two"),
            CreateVectorResult(vectorOnly, Guid.NewGuid(), "vector-only rank three")));
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(vectorSecondTextFirst, vectorService.Results.Results[1].DocumentId, "text rank one"),
            CreateFullTextResult(fullTextOnly, Guid.NewGuid(), "text-only rank two"),
            CreateFullTextResult(vectorFirstTextThird, vectorService.Results.Results[0].DocumentId, "text rank three"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query", topK: 4);

        var firstCandidateScore = 1d / (60 + 1) + 1d / (60 + 3);
        var secondCandidateScore = 1d / (60 + 2) + 1d / (60 + 1);
        Assert.IsGreaterThan(firstCandidateScore, secondCandidateScore);
        CollectionAssert.AreEqual(
            new[] { vectorSecondTextFirst, vectorFirstTextThird, fullTextOnly, vectorOnly },
            results.Select(result => result.ChunkId).ToArray());
        Assert.IsFalse(results[2].FoundByVectorSearch);
        Assert.IsTrue(results[2].FoundByFullTextSearch);
        Assert.IsTrue(results[3].FoundByVectorSearch);
        Assert.IsFalse(results[3].FoundByFullTextSearch);
    }

    [TestMethod]
    public async Task SearchAsync_LargeTopK_CapsCandidateSetsAtFifty()
    {
        var vectorService = new FakeVectorSearchService(CreateVectorResponse());
        var fullTextService = new FakeFullTextSearchService();
        var service = CreateService(vectorService, fullTextService);

        await service.SearchAsync("query", topK: 40);

        Assert.AreEqual(50, vectorService.LastTopK);
        Assert.AreEqual(50, fullTextService.LastTopK);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_Tags_AreNormalizedAndPassedToBothSources()
    {
        var vector = new FakeVectorSearchService(CreateVectorResponse());
        var fullText = new FakeFullTextSearchService();
        var service = CreateService(vector, fullText);

        await service.SearchAsync("query", tags: [" beta ", "alpha"]);

        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, vector.LastTags!.ToArray());
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, fullText.LastTags!.ToArray());
    }

    private static HybridSearchService CreateService(
        FakeVectorSearchService vectorService,
        FakeFullTextSearchService fullTextService)
    {
        return new HybridSearchService(
            vectorService,
            fullTextService,
            NullLogger<HybridSearchService>.Instance);
    }

    private static VectorSearchResponse CreateVectorResponse(params VectorSearchResult[] results)
    {
        return new VectorSearchResponse { Results = results };
    }

    private static VectorSearchResult CreateVectorResult(Guid chunkId, Guid documentId, string content)
    {
        return new VectorSearchResult
        {
            ChunkId = chunkId,
            DocumentId = documentId,
            ContentSnippet = content
        };
    }

    private static FullTextSearchResult CreateFullTextResult(Guid chunkId, Guid documentId, string content)
    {
        return new FullTextSearchResult
        {
            ChunkId = chunkId,
            DocumentId = documentId,
            Content = content
        };
    }

    private sealed class FakeVectorSearchService(VectorSearchResponse results) : IVectorSearchService
    {
        public VectorSearchResponse Results { get; } = results;

        public int LastTopK { get; private set; }
        public IReadOnlyCollection<string?>? LastTags { get; private set; }

        public Task<VectorSearchResponse> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null,
            IReadOnlyCollection<string?>? tags = null)
        {
            LastTopK = topK;
            LastTags = tags;
            return Task.FromResult(Results);
        }
    }

    private sealed class FakeFullTextSearchService(params FullTextSearchResult[] results) : IFullTextSearchService
    {
        public int LastTopK { get; private set; }
        public IReadOnlyCollection<string?>? LastTags { get; private set; }

        public Task<IReadOnlyList<FullTextSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null,
            IReadOnlyCollection<string?>? tags = null)
        {
            LastTopK = topK;
            LastTags = tags;
            return Task.FromResult<IReadOnlyList<FullTextSearchResult>>(results);
        }
    }
}