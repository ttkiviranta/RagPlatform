using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[DoNotParallelize]
public sealed class WebContentServiceSecurityTests : PostgreSqlTestClassBase
{
    private const int MaximumResponseBytes = 5 * 1024 * 1024;

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("0.1.2.3")]
    [DataRow("10.1.2.3")]
    [DataRow("100.64.0.1")]
    [DataRow("127.0.0.1")]
    [DataRow("169.254.1.2")]
    [DataRow("172.16.0.1")]
    [DataRow("192.168.1.1")]
    [DataRow("198.18.0.1")]
    [DataRow("224.0.0.1")]
    [DataRow("240.0.0.1")]
    [DataRow("::")]
    [DataRow("::1")]
    [DataRow("::ffff:192.168.1.1")]
    [DataRow("fc00::1")]
    [DataRow("fe80::1")]
    [DataRow("2001:db8::1")]
    [DataRow("2002::1")]
    public void IsPublicAddress_PrivateOrSpecialAddress_ReturnsFalse(string address)
    {
        Assert.IsFalse(WebContentDestinationPolicy.IsPublicAddress(IPAddress.Parse(address)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("8.8.8.8")]
    [DataRow("2606:4700:4700::1111")]
    public void IsPublicAddress_PublicAddress_ReturnsTrue(string address)
    {
        Assert.IsTrue(WebContentDestinationPolicy.IsPublicAddress(IPAddress.Parse(address)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("https://user:password@example.com/")]
    [DataRow("http://example.com:8080/")]
    [DataRow("https://example.com:80/")]
    [DataRow("http://127.0.0.1/")]
    [DataRow("http://[::ffff:192.168.1.1]/")]
    public void ValidateUri_DisallowedDestination_Throws(string value)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WebContentDestinationPolicy.ValidateUri(new Uri(value), "url"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("https://example.com/")]
    [DataRow("http://example.com/")]
    public void ValidateUri_PublicHttpDestination_IsAllowed(string value)
    {
        WebContentDestinationPolicy.ValidateUri(new Uri(value), "url");
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_ValidHtml_PersistsDocumentAndPreservesSourceUrl()
    {
        var fixture = CreateFixture(_ => Task.FromResult(CreateHtmlResponse("text/html; charset=utf-8")));
        using (fixture)
        {
            var sourceUrl = "https://example.com/article";

            var document = await fixture.Service.ImportAsync(sourceUrl);

            Assert.AreEqual(sourceUrl, document.SourceUrl);
            Assert.AreEqual("Example", document.Title);
            Assert.AreEqual("Readable content.", document.Content);
            Assert.AreEqual(1, await fixture.DbContext.Documents.CountAsync());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_LoopbackLiteral_IsRejectedBeforeSendingRequest()
    {
        var fixture = CreateFixture(_ => Task.FromResult(CreateHtmlResponse("text/html")));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => fixture.Service.ImportAsync("https://127.0.0.1/private"));

            Assert.AreEqual(0, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_FollowsPublicRedirect_AndKeepsOriginalSourceUrl()
    {
        var fixture = CreateFixture(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/start")
            {
                return Task.FromResult(CreateRedirectResponse(HttpStatusCode.MovedPermanently, "/final"));
            }

            return Task.FromResult(CreateHtmlResponse("text/html"));
        });
        using (fixture)
        {
            var sourceUrl = "https://example.com/start";

            var document = await fixture.Service.ImportAsync(sourceUrl);

            Assert.AreEqual(2, fixture.Handler.RequestedUris.Count);
            Assert.AreEqual("/final", fixture.Handler.RequestedUris[1].AbsolutePath);
            Assert.AreEqual(sourceUrl, document.SourceUrl);
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_ResponseAtSizeLimit_IsAccepted()
    {
        const string prefix = "<html><body><main>Content";
        const string suffix = "</main></body></html>";
        var paddingLength = MaximumResponseBytes
            - Encoding.UTF8.GetByteCount(prefix)
            - Encoding.UTF8.GetByteCount(suffix);
        var body = Encoding.UTF8.GetBytes(prefix + new string(' ', paddingLength) + suffix);
        Assert.AreEqual(MaximumResponseBytes, body.Length);

        var content = new StreamContent(new NonSeekableReadStream(new MemoryStream(body)));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        var fixture = CreateFixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using (fixture)
        {
            var document = await fixture.Service.ImportAsync("https://example.com/page");

            Assert.AreEqual("Content", document.Content);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_RedirectsToPrivateDestination_RejectsBeforeSecondRequest()
    {
        var fixture = CreateFixture(_ => Task.FromResult(
            CreateRedirectResponse(HttpStatusCode.Found, "http://127.0.0.1/private")));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/start"));

            Assert.AreEqual(1, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_HttpsRedirectToHttp_RejectsDowngrade()
    {
        var fixture = CreateFixture(_ => Task.FromResult(
            CreateRedirectResponse(HttpStatusCode.Found, "http://example.com/next")));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/start"));

            Assert.AreEqual(1, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_RedirectLoop_RejectsRepeatedDestination()
    {
        var fixture = CreateFixture(request => Task.FromResult(
            CreateRedirectResponse(HttpStatusCode.Found,
                request.RequestUri!.AbsolutePath == "/first" ? "/second" : "/first")));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/first"));

            Assert.AreEqual(2, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_FiveRedirects_AreAllowed()
    {
        var fixture = CreateFixture(request =>
        {
            var index = int.Parse(request.RequestUri!.AbsolutePath.TrimStart('/'));
            return Task.FromResult(index < 5
                ? CreateRedirectResponse(HttpStatusCode.TemporaryRedirect, $"/{index + 1}")
                : CreateHtmlResponse("application/xhtml+xml"));
        });
        using (fixture)
        {
            await fixture.Service.ImportAsync("https://example.com/0");

            Assert.AreEqual(6, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_MoreThanFiveRedirects_AreRejected()
    {
        var fixture = CreateFixture(request =>
        {
            var index = int.Parse(request.RequestUri!.AbsolutePath.TrimStart('/'));
            return Task.FromResult(CreateRedirectResponse(HttpStatusCode.TemporaryRedirect, $"/{index + 1}"));
        });
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/0"));

            Assert.AreEqual(6, fixture.Handler.RequestedUris.Count);
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    [DataRow("text/html")]
    [DataRow("application/xhtml+xml")]
    [DataRow("application/xml")]
    public async Task ImportAsync_SupportedContentType_IsAccepted(string contentType)
    {
        var fixture = CreateFixture(_ => Task.FromResult(CreateHtmlResponse(contentType)));
        using (fixture)
        {
            var document = await fixture.Service.ImportAsync("https://example.com/page");

            Assert.AreEqual("Readable content.", document.Content);
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_UnsupportedContentType_IsRejected()
    {
        var fixture = CreateFixture(_ => Task.FromResult(CreateHtmlResponse("application/pdf")));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/page"));

            Assert.AreEqual(0, await fixture.DbContext.Documents.CountAsync());
        }
    }

    [TestMethod]
    [TestCategory("PostgreSql")]
    public async Task ImportAsync_MissingContentType_IsRejectedWithExplicitError()
    {
        var fixture = CreateFixture(_ => Task.FromResult(CreateHtmlResponse(null)));
        using (fixture)
        {
            var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/page"));

            Assert.AreEqual("The remote page did not include a Content-Type header.", exception.Message);
            Assert.AreEqual(0, await fixture.DbContext.Documents.CountAsync());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_ResponseExceedingContentLengthLimit_IsRejected()
    {
        var content = new ByteArrayContent(new byte[MaximumResponseBytes + 1]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        var fixture = CreateFixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using (fixture)
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/page"));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ImportAsync_StreamingResponseWithoutContentLength_EnforcesLimit()
    {
        var body = new byte[MaximumResponseBytes + 1];
        Encoding.UTF8.GetBytes("<html><body><main>Content</main></body></html>").CopyTo(body, 0);
        var content = new StreamContent(new NonSeekableReadStream(new MemoryStream(body)));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        var fixture = CreateFixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using (fixture)
        {
            Assert.IsNull(content.Headers.ContentLength);

            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => fixture.Service.ImportAsync("https://example.com/page"));
        }
    }

    private static TestFixture CreateFixture(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    {
        var handler = new StubHttpMessageHandler(send);
        var httpClient = new HttpClient(handler);
        var dbContext = new ApplicationDbContext(TestDbContextOptions.Create());
        var user = new User { TenantId = "tenant-a" };
        var service = new WebContentService(
            dbContext,
            new FakeDocumentAccessService(user),
            new FakeAuditEventWriter(),
            new StubHttpClientFactory(httpClient),
            NullLogger<WebContentService>.Instance);

        return new TestFixture(dbContext, httpClient, handler, service);
    }

    private static HttpResponseMessage CreateHtmlResponse(string? contentType)
    {
        var content = new StringContent(
            "<html><head><title>Example</title></head><body><main>Readable content.</main></body></html>",
            Encoding.UTF8);
        if (contentType is not null)
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }
        else
        {
            content.Headers.Remove("Content-Type");
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage CreateRedirectResponse(HttpStatusCode statusCode, string location)
    {
        return new HttpResponseMessage(statusCode)
        {
            Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) }
        };
    }

    private sealed class TestFixture : IDisposable
    {
        public TestFixture(
            ApplicationDbContext dbContext,
            HttpClient httpClient,
            StubHttpMessageHandler handler,
            WebContentService service)
        {
            DbContext = dbContext;
            HttpClient = httpClient;
            Handler = handler;
            Service = service;
        }

        public ApplicationDbContext DbContext { get; }
        public HttpClient HttpClient { get; }
        public StubHttpMessageHandler Handler { get; }
        public WebContentService Service { get; }

        public void Dispose()
        {
            HttpClient.Dispose();
            DbContext.Dispose();
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            return _send(request);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeDocumentAccessService : IDocumentAccessService
    {
        private readonly User _user;

        public FakeDocumentAccessService(User user)
        {
            _user = user;
        }

        public Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_user);

        public Task<Document?> GetAccessibleDocumentAsync(
            Guid documentId,
            RagPlatform.Api.Authorization.DocumentPermission permission,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IQueryable<Document> ScopeDocuments(IQueryable<Document> documents, User user) => documents;
    }

    private sealed class FakeAuditEventWriter : IAuditEventWriter
    {
        public void Add(
            Guid? actorUserId,
            string tenantId,
            string eventType,
            string outcome,
            string? targetType = null,
            string? targetId = null,
            double? durationMilliseconds = null,
            int? inputCharacterCount = null,
            int? requestedTopK = null,
            int? resultCount = null,
            double? averageSimilarity = null,
            int? contextCharacterCount = null,
            int? promptCharacterCount = null,
            string? modelProvider = null,
            string? modelDeployment = null,
            string? modelOperation = null,
            int? inputTokenCount = null,
            int? outputTokenCount = null,
            int? generatedEmbeddingCount = null,
            int? reusedEmbeddingCount = null)
        {
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableReadStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _innerStream.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}