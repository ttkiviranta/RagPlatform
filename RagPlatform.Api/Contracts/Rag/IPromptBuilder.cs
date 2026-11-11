namespace RagPlatform.Api.Contracts.Rag;

public interface IPromptBuilder
{
    string BuildSystemPrompt();

    string BuildPrompt(string question, string context);
}
