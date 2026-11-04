namespace RagPlatform.Api.Contracts.Rag;

public interface IPromptBuilder
{
    string BuildPrompt(string question, string context);
}
