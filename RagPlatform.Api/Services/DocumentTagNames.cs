using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

internal static class DocumentTagNames
{
    public static IReadOnlyList<string> Normalize(IReadOnlyCollection<string?>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        if (tags.Count > 50)
        {
            throw new ArgumentException("A document can have at most 50 tags.", "tags");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            var name = tag?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
            {
                throw new ArgumentException("Each tag must contain 1 to 100 characters and no control characters.", "tags");
            }

            if (!names.Add(name))
            {
                throw new ArgumentException("Duplicate tags are not allowed.", "tags");
            }
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    public static ICollection<DocumentTag> Create(IReadOnlyList<string> names) =>
        names.Select(name => new DocumentTag { Name = name }).ToList();
}
