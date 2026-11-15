# Phase 13 Increment 3 - Grounded Answer Generation

## Scope

This increment adds a text-only answer-generation abstraction and an Azure AI Foundry-backed implementation for retrieved chunks. It does not add citations or conversation history to the model prompt, and it does not change document ingestion.

## Design and implementation

- `IAnswerGenerationService.GenerateAnswerAsync` accepts the question and ranked vector-search results and returns a plain answer string.
- `FoundryAnswerGenerationService` builds context through the existing `IContextBuilder`, capped at 4,000 characters, then builds one user prompt and calls the existing chat-completion boundary. It returns only the trimmed answer text.
- Empty or wholly unusable retrieval results return `not found in accessible documents` without calling Foundry.
- `FoundryChatCompletionService` validates the configured deployment and token limit, enforces a configured request timeout, and applies configured temperature. The existing Foundry inference client sends these settings to the OpenAI-compatible chat-completions endpoint.
- Chat remains behind the existing `IFoundryChatCompletionService` transport abstraction; `IAnswerGenerationService` is the use-case boundary used by the RAG pipeline.

## Prompt and evidence handling

The system prompt directs the model to answer only from the supplied retrieved context, treats retrieved material as untrusted reference data rather than instructions, prohibits outside knowledge and unsupported inference, and requires the exact fallback phrase when evidence is empty or insufficient. The answer-generation implementation independently returns that fallback when no usable context is available, so empty retrieval does not trigger a model call.

Prompt instructions reduce risk but cannot prove grounding for non-empty contexts. Model output still requires evaluation; citation validation and stronger evidence-level checks remain later work. No source/citation metadata is returned by the answer-generation abstraction.

## Configuration

| Setting | Meaning | Default |
| --- | --- | --- |
| `Foundry:ChatDeployment` | Foundry chat deployment name | Required |
| `Foundry:MaxOutputTokens` | Maximum generated output tokens | `800` |
| `Foundry:ChatTimeoutSeconds` | Chat request timeout, valid range 1-60 seconds | `60` |
| `Foundry:ChatTemperature` | Chat sampling temperature, valid range 0-2 | `0.2` |

## Persistence boundary

RAG queries are stateless in this increment: the RAG query contract does not accept a conversation ID, conversation history is not sent to the model, and query exchanges are not persisted. The separate conversation CRUD endpoints remain unchanged; a future Phase 13 increment can explicitly add authorized conversation integration.

## Validation and limitations

The API build is required to validate integration. Live Foundry calls, answer quality, abstention quality for non-empty context, and latency/token-cost behavior require separate integration/evaluation evidence. No ingestion components are modified.