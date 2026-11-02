using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RagPlatform.Api.Contracts.Web;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class WebContentService : IWebContentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebContentService> _logger;

    public WebContentService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<WebContentService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Document> ImportAsync(string url, CancellationToken cancellationToken = default)
    {
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

        var client = _httpClientFactory.CreateClient("WebContentClient");

        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Unable to fetch the page. Status code: {(int)response.StatusCode}.");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = BuildDocument(uri, html);

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Imported web content from {SourceUrl} into document {DocumentId}.",
            uri,
            document.Id);

        return document;
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