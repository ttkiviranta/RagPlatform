using RagPlatform.Api.Contracts.Rag;

namespace RagPlatform.Api.Services;

public sealed class PromptBuilder : IPromptBuilder
{
    public string BuildPrompt(string question, string context)
    {
        var normalizedQuestion = string.IsNullOrWhiteSpace(question) ? string.Empty : question.Trim();
        var normalizedContext = string.IsNullOrWhiteSpace(context) ? string.Empty : context.Trim();

        var contextSection = string.IsNullOrWhiteSpace(normalizedContext)
            ? "No relevant context was retrieved."
            : normalizedContext;

        return $"You are an assistant that answers questions using only the supplied context.\n\nContext:\n{contextSection}\n\nQuestion:\n{normalizedQuestion}\n\nAnswer:";
    }
}
