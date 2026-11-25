# Phase 13 - Document Q&A

## Goal and planned workflow

Provide authenticated document Q&A over the imported corpus. The planned request flow is: authenticate and resolve user scope; validate the query and owned conversation; embed the question with the configured compatible embedding model; retrieve only accessible chunks; rank and budget context; call Azure AI Foundry chat; validate server-created citations; and return an answer with sources. Persist conversation turns and citations only under owner-controlled access.

## Architecture decisions recorded in the planning discussion

- Keep retrieval and authorization in the API, not as model-controlled tools.
- Apply tenant, owner, document-ACL, and optional document-scope filters in the database query before returning chunks. Optional tag filters also apply before ranking and require all requested tags.
- Start with pgvector similarity search; hybrid search and RRF are implemented and exposed. RAG retrieval defaults to vector search and can enable hybrid RRF through `Rag:UseHybridSearch`, with vector fallback on failure or empty hybrid results. Optional `Rag:UseReranking` adds Foundry reranking to hybrid RAG and defaults to false; its quality remains subject to [evaluation](../../validation/reranking-evaluation.md).
- Assign citation labels and source metadata server-side; never trust model-generated document IDs or locations. Abstain when retrieved evidence is insufficient.
- Keep conversation history application-managed, owner-scoped, bounded, and freshly authorized on each turn.
- Begin with a deterministic single-agent pipeline; agent orchestration is not required for the initial Q&A feature.
- Measure retrieval quality, grounded-answer quality, latency, and cost before accepting the feature.

## Increment 1 - contracts and authorization

The repository README records Increment 1 as complete: query/answer/citation/conversation contracts and named active-user policies for Q&A and conversation access. The existing query/conversation request and response DTOs are reused; a typed citation DTO is added to the response contracts. Conversation ownership remains enforced by existing services, and document-derived retrieval must remain document-authorized.

The associated API build succeeded in the working tree at archive time. The DTO/policy source edits were uncommitted and are not included in the documentation archive commit. This increment does not establish citation population, conversation citation persistence, retrieval implementation, or full Phase 13 acceptance.

## Increment 2 - authorized vector retrieval

Increment 2 adds an optional document scope to the vector-search request and performs model- and dimension-compatible pgvector cosine ranking in PostgreSQL. Tenant, owner/role access, and optional document filtering are part of the same query. Functional vector retrieval was subsequently validated against the five-document evaluation corpus. Query vector dimensions are checked before search; model name, persisted dimension metadata, and actual vector dimensions are checked before a stored vector can be ranked. This increment does not add answer generation or citation population. See [Increment 2 design and implementation notes](increment02-authorized-vector-retrieval.md).

The API build is the compile validation for this increment. Query execution, pgvector SQL translation, and access-filter behavior still require PostgreSQL/pgvector integration tests; EF Core InMemory cannot validate those behaviors.

## Increment 3 - grounded answer generation

Increment 3 adds `IAnswerGenerationService`, backed by `FoundryAnswerGenerationService`, which builds context capped at 4,000 characters and returns answer text only. It delegates chat transport to the existing Foundry completion service, now configured for deployment, output-token limit, timeout, and temperature. The deterministic system prompt limits answers to retrieved context, treats it as untrusted data, and requires the explicit `not found in accessible documents` response when evidence is insufficient. Empty retrieval returns that response without a model call.

No citations or conversation history are passed to the model. RAG queries are stateless and do not persist exchanges in this increment; the separate conversation endpoints are unchanged. See [Increment 3 design and implementation notes](increment03-grounded-answer-generation.md). Build success does not establish live Foundry or answer-quality validation.

## Increment 4 - validated citations

Increment 4 reloads retrieved chunk/document data from PostgreSQL under the existing document-access scope, assigns `[1]`, `[2]`, … labels in retrieval order, and sends bounded persisted excerpts to Foundry. The answer prompt requires supplied labels only. The API rejects unsupported labels and non-fallback answers that cite no supplied label, then returns only validated citations with persisted IDs/source name and a capped excerpt. Page numbers are null because the current chunk model stores no page field. Model-generated source metadata is never trusted. See [Increment 4 implementation notes](increment04-validated-citations.md).

RAG query execution remains stateless; no conversation history or exchange persistence is added. Build validation does not replace PostgreSQL/Foundry integration or semantic-grounding evaluation.

## Increment 5 - conversation persistence

Increment 5 connects each successful RAG request to the owner-scoped conversation store. `ConversationId` is optional in the request; omitting it creates a conversation with the first question as its title. The response includes the resulting ID. User/assistant turns and validated citation snapshots are saved together, and conversation-message reads return the persisted citations.

Conversation queries enforce both owner and tenant boundaries. Every turn performs fresh authorized retrieval using the current question; prior assistant answers are not included in the prompt or used as evidence. At most six prior user messages and 2,000 characters are provided only to interpret follow-up references.

Owner-authorized conversation deletion cascades to turns and citation snapshots. No automatic retention period or purge worker is configured; its policy and operational design are documented in [Increment 5 implementation notes](increment05-conversation-persistence.md). PostgreSQL and isolation validation remain outstanding.

## Increment 6 - quality telemetry and hardening

Increment 6 adds retrieval telemetry for duration, candidate/returned chunk counts, similarity, and failure type. Answer-generation telemetry records duration, returned chunks, validated citation count, Foundry provider/deployment, token usage when provided, and distinct no-context, insufficient-evidence, generation-failure, and citation-validation-failure outcomes. Structured logs report stage and counts without query, prompt, source excerpt, or answer content. See [Increment 6 implementation notes](increment06-quality-and-hardening.md).

Deterministic tests cover untrusted-context prompt framing, omission of prior assistant answers, no-context abstention, low-evidence abstention, and failure telemetry. The full solution build succeeds and all five focused hardening tests pass. Live-model resilience and database/runtime telemetry still need integration evidence.

## Remaining increments and acceptance

- Select and operationalize a tenant/legal retention period and scheduled purge policy.
- Evaluate retrieval, answer grounding, live prompt-injection resistance, latency, reliability, and cost against explicit targets.

## Missing documentation

No standalone Phase 13 plan, ADR, model-selection evaluation, test plan, rollout report, completion report, or retrospective was found in the repository. The archive summarizes the planning discussion and the README's Increment 1 note; future acceptance results must be added when produced.