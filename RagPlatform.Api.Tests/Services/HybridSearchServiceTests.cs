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
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
        Assert.IsTrue(results[1].FoundByVectorSearch);
        Assert.IsFalse(results[1].FoundByFullTextSearch);
        Assert.IsFalse(results[2].FoundByVectorSearch);
        Assert.IsTrue(results[2].FoundByFullTextSearch);
    }

    [TestMethod]
    public async Task SearchAsync_Results_AreOrderedOverlapThenVectorOnlyThenFullTextOnly()
    {
        var vectorOnlyFirst = Guid.NewGuid();
        var overlap = Guid.NewGuid();
        var vectorOnlySecond = Guid.NewGuid();
        var textOnly = Guid.NewGuid();
        var vectorService = new FakeVectorSearchService(CreateVectorResponse(
            CreateVectorResult(vectorOnlyFirst, Guid.NewGuid(), "vector 1"),
            CreateVectorResult(overlap, Guid.NewGuid(), "overlap snippet"),
            CreateVectorResult(vectorOnlySecond, Guid.NewGuid(), "vector 2")));
        var overlapDocumentId = vectorService.Results.Results[1].DocumentId;
        var fullTextService = new FakeFullTextSearchService(
            CreateFullTextResult(overlap, overlapDocumentId, "overlap content"),
            CreateFullTextResult(textOnly, Guid.NewGuid(), "text content"));
        var service = CreateService(vectorService, fullTextService);

        var results = await service.SearchAsync("query", topK: 4);

        CollectionAssert.AreEqual(
            new[] { overlap, vectorOnlyFirst, vectorOnlySecond, textOnly },
            results.Select(result => result.ChunkId).ToArray());
        Assert.AreEqual(8, vectorService.LastTopK);
        Assert.AreEqual(8, fullTextService.LastTopK);
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

        public Task<VectorSearchResponse> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null)
        {
            LastTopK = topK;
            return Task.FromResult(Results);
        }
    }

    private sealed class FakeFullTextSearchService(params FullTextSearchResult[] results) : IFullTextSearchService
    {
        public int LastTopK { get; private set; }

        public Task<IReadOnlyList<FullTextSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null)
        {
            LastTopK = topK;
            return Task.FromResult<IReadOnlyList<FullTextSearchResult>>(results);
        }
    }
}