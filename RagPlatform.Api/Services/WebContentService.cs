using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Web;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class WebContentService : IWebContentService
{
    private const int MaximumResponseBytes = 5 * 1024 * 1024;
    private const int MaximumRedirects = 5;
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "application/xhtml+xml",
        "application/xml"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebContentService> _logger;

    public WebContentService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IAuditEventWriter auditEventWriter,
        IHttpClientFactory httpClientFactory,
        ILogger<WebContentService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _auditEventWriter = auditEventWriter;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Document> ImportAsync(string url, CancellationToken cancellationToken = default, IReadOnlyCollection<string?>? tags = null)
    {
        var tagNames = DocumentTagNames.Normalize(tags);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        var trimmedUrl = url.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid HTTP or HTTPS URL is required.", nameof(url));
        }

        WebContentDestinationPolicy.ValidateUri(uri, nameof(url));

        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("WebContentClient");

        var currentUri = uri;
        var visitedUris = new HashSet<string>(StringComparer.Ordinal)
        {
            GetRedirectLoopKey(uri)
        };

        for (var redirectCount = 0; ; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (redirectCount >= MaximumRedirects)
                {
                    throw new HttpRequestException($"The remote page exceeded the {MaximumRedirects}-redirect limit.");
                }

                if (response.Headers.Location is null)
                {
                    throw new HttpRequestException("The remote page returned a redirect without a destination.");
                }

                Uri redirectUri;
                try
                {
                    redirectUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    WebContentDestinationPolicy.ValidateUri(redirectUri, nameof(url));
                }
                catch (ArgumentException exception)
                {
                    throw new HttpRequestException("The remote page redirected to a disallowed destination.", exception);
                }
                catch (UriFormatException exception)
                {
                    throw new HttpRequestException("The remote page returned an invalid redirect destination.", exception);
                }

                if (currentUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme == Uri.UriSchemeHttp)
                {
                    throw new HttpRequestException("HTTPS-to-HTTP redirects are not allowed.");
                }

                if (!visitedUris.Add(GetRedirectLoopKey(redirectUri)))
                {
                    throw new HttpRequestException("The remote page returned a redirect loop.");
                }

                currentUri = redirectUri;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Unable to fetch the page. Status code: {(int)response.StatusCode}.");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null)
            {
                throw new HttpRequestException("The remote page did not include a Content-Type header.");
            }

            if (!SupportedContentTypes.Contains(contentType))
            {
                throw new HttpRequestException("The remote page returned an unsupported content type.");
            }

            var responseBytes = await ReadResponseBytesAsync(response.Content, cancellationToken);
            var html = DecodeResponse(responseBytes, response.Content.Headers.ContentType?.CharSet);
            var document = BuildDocument(uri, html);
            document.OwnerUserId = user.Id;
            document.TenantId = user.TenantId;
            document.Tags = DocumentTagNames.Create(tagNames);

            _dbContext.Documents.Add(document);
            _auditEventWriter.Add(user.Id, user.TenantId, AuditEventTypes.DocumentCreated, "Succeeded", "Document", document.Id.ToString());
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Imported web content from {SourceUrl} into document {DocumentId}.",
                uri,
                document.Id);

            return document;
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MultipleChoices
            or HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static string GetRedirectLoopKey(Uri uri)
    {
        return new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri;
    }

    private static async Task<byte[]> ReadResponseBytesAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new HttpRequestException($"The remote page exceeded the {MaximumResponseBytes}-byte response limit.");
        }

        await using var responseStream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var readBuffer = new byte[81920];

        while (true)
        {
            var bytesRemaining = MaximumResponseBytes - (int)buffer.Length;
            var bytesToRead = Math.Min(readBuffer.Length, bytesRemaining + 1);
            var bytesRead = await responseStream.ReadAsync(readBuffer.AsMemory(0, bytesToRead), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (bytesRead > bytesRemaining)
            {
                throw new HttpRequestException($"The remote page exceeded the {MaximumResponseBytes}-byte response limit.");
            }

            await buffer.WriteAsync(readBuffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static string DecodeResponse(byte[] responseBytes, string? characterSet)
    {
        Encoding encoding;
        try
        {
            encoding = string.IsNullOrWhiteSpace(characterSet)
                ? Encoding.UTF8
                : Encoding.GetEncoding(characterSet.Trim('"'));
        }
        catch (ArgumentException exception)
        {
            throw new HttpRequestException("The remote page declared an unsupported character encoding.", exception);
        }

        using var stream = new MemoryStream(responseBytes, writable: false);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Document BuildDocument(Uri uri, string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        var title = ExtractTitle(htmlDocument);
        var content = ExtractMainContent(htmlDocument);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("No readable content was found for the supplied URL.");
        }

        var cleanedTitle = string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(uri) : title;

        return new Document
        {
            Title = cleanedTitle,
            Content = content,
            FileName = BuildFileName(cleanedTitle, uri),
            ContentType = "text/html",
            FileSize = Encoding.UTF8.GetByteCount(content),
            SourceUrl = uri.ToString(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ExtractTitle(HtmlDocument htmlDocument)
    {
        var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:title' or @name='twitter:title']")
            ?? htmlDocument.DocumentNode.SelectSingleNode("//title");

        if (titleNode is null)
        {
            return string.Empty;
        }

        var candidate = titleNode.GetAttributeValue("content", string.Empty);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = titleNode.InnerText;
        }

        return CleanText(candidate);
    }

    private static string ExtractMainContent(HtmlDocument htmlDocument)
    {
        var contentNode = htmlDocument.DocumentNode.SelectSingleNode("//main")
            ?? htmlDocument.DocumentNode.SelectSingleNode("//article")
            ?? htmlDocument.DocumentNode.SelectSingleNode("//body");

        if (contentNode is null)
        {
            return string.Empty;
        }

        var removableNodes = contentNode.SelectNodes(".//script|.//style|.//noscript|.//svg|.//iframe|.//header|.//footer|.//nav");
        if (removableNodes is not null)
        {
            foreach (var node in removableNodes)
            {
                node.Remove();
            }
        }

        var text = contentNode.InnerText;
        text = Regex.Replace(text, @"\r\n?", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n");

        return CleanText(text);
    }

    private static string BuildDefaultTitle(Uri uri)
    {
        return string.IsNullOrWhiteSpace(uri.Host) ? "Imported web page" : uri.Host;
    }

    private static string BuildFileName(string title, Uri uri)
    {
        var baseName = string.IsNullOrWhiteSpace(title) ? uri.Host : title;
        var sanitized = Regex.Replace(baseName, @"[<>:/\\|?*]+", "-");
        sanitized = sanitized.Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = uri.Host;
        }

        if (sanitized.Length > 120)
        {
            sanitized = sanitized.Substring(0, 120).TrimEnd();
        }

        return $"{sanitized}.html";
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, @"\s+", " ");
        cleaned = cleaned.Replace("\u00A0", " ");
        cleaned = Regex.Replace(cleaned, @"\n\s*\n", "\n\n");

        return cleaned.Trim();
    }
}