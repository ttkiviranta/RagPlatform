using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Controllers;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class TagRetrievalControllerTests
{
    [TestMethod]
    public async Task VectorSearch_PassesTagsAndDocumentId()
    {
        var search = new CapturingVectorSearch();
        var controller = new VectorSearchController(search, NullLogger<VectorSearchController>.Instance);
        var documentId = Guid.NewGuid();

        var result = await controller.SearchAsync(new VectorSearchRequest
        {
            Query = "query", DocumentId = documentId, Tags = ["alpha", "beta"]
        }, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreEqual(documentId, search.DocumentId);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, search.Tags!.ToArray());
    }

    [TestMethod]
    public async Task RagQuery_PassesTagsAndConversationId()
    {
        var rag = new CapturingRag();
        var controller = new RagController(rag, NullLogger<RagController>.Instance);
        var conversationId = Guid.NewGuid();

        var result = await controller.QueryAsync(new RagQueryRequest
        {
            Query = "question", ConversationId = conversationId, Tags = ["alpha", "beta"]
        }, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreEqual(conversationId, rag.ConversationId);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, rag.Tags!.ToArray());
    }

    [TestMethod]
    public async Task InvalidTags_AreReportedAsTagValidationErrors()
    {
        var search = new CapturingVectorSearch { RejectTags = true };
        var controller = new VectorSearchController(search, NullLogger<VectorSearchController>.Instance);

        var result = await controller.SearchAsync(new VectorSearchRequest
        {
            Query = "query", Tags = ["alpha", "alpha"]
        }, CancellationToken.None);

        var error = result.Result as ObjectResult;
        Assert.IsNotNull(error);
        var details = error.Value as ValidationProblemDetails;
        Assert.IsNotNull(details);
        Assert.IsTrue(details.Errors.ContainsKey("tags"));
        StringAssert.Contains(details.Errors["tags"].Single(), "Duplicate tags");
    }

    private sealed class CapturingVectorSearch : IVectorSearchService
    {
        public Guid? DocumentId { get; private set; }
        public IReadOnlyCollection<string?>? Tags { get; private set; }
        public bool RejectTags { get; init; }

        public Task<VectorSearchResponse> SearchAsync(string query, int topK = 5,
            CancellationToken cancellationToken = default, Guid? documentId = null, IReadOnlyCollection<string?>? tags = null)
        {
            DocumentId = documentId;
            Tags = tags;
            if (RejectTags) throw new ArgumentException("Duplicate tags", "tags");
            return Task.FromResult(new VectorSearchResponse());
        }
    }

    private sealed class CapturingRag : IRagService
    {
        public Guid? ConversationId { get; private set; }
        public IReadOnlyCollection<string?>? Tags { get; private set; }

        public Task<RagQueryResponse> QueryAsync(string query, int topK = 5,
            CancellationToken cancellationToken = default, Guid? conversationId = null, IReadOnlyCollection<string?>? tags = null)
        {
            ConversationId = conversationId;
            Tags = tags;
            return Task.FromResult(new RagQueryResponse());
        }
    }
}
