using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Contracts.Rag;

public interface IContextBuilder
{
    string BuildContext(IReadOnlyList<VectorSearchResult> results, int maxCharacters = 4000);
}
