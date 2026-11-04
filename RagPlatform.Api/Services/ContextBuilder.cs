using System.Text;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Services;

public sealed class ContextBuilder : IContextBuilder
{
    public string BuildContext(IReadOnlyList<VectorSearchResult> results, int maxCharacters = 4000)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        var orderedResults = results
            .Where(result => !string.IsNullOrWhiteSpace(result.ContentSnippet))
            .GroupBy(result => result.ChunkId)
            .Select(group => group.OrderByDescending(item => item.SimilarityScore).First())
            .OrderByDescending(result => result.SimilarityScore)
            .ToList();

        if (orderedResults.Count == 0)
        {
            return string.Empty;
        }

        var contextBuilder = new StringBuilder();
        foreach (var result in orderedResults)
        {
            var snippet = Normalize(result.ContentSnippet);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                continue;
            }

            var sourceText = $"[DocumentId: {result.DocumentId}] [ChunkId: {result.ChunkId}] [Similarity: {result.SimilarityScore:F4}]\n{snippet}\n\n";
            if (contextBuilder.Length + sourceText.Length > maxCharacters)
            {
                var remainingLimit = Math.Max(0, maxCharacters - contextBuilder.Length);
                if (remainingLimit <= 0)
                {
                    break;
                }

                contextBuilder.Append(sourceText.Substring(0, Math.Min(sourceText.Length, remainingLimit)).TrimEnd());
                break;
            }

            contextBuilder.Append(sourceText);
        }

        return contextBuilder.ToString().Trim();
    }

    private static string Normalize(string content)
    {
        var normalized = content
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace("\t", " ");

        var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts);
    }
}
