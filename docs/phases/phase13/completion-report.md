# Phase 13 Completion Report - Document Q&A

## Executive summary

Phase 13 implementation increments 1-6 are complete. They establish authenticated document Q&A using authorized pgvector retrieval, grounded Azure AI Foundry generation, server-validated citations, owner/tenant-scoped conversation persistence, and quality telemetry with deterministic safety/evidence tests.

**Implementation status:** Complete for increments 1-6.
**Acceptance status:** Pending runtime, quality, security, and operational validation. Implementation completion should not be treated as production readiness or as proof of answer quality.

## Summary of increments 1-6

| Increment | Delivered |
| --- | --- |
| 1. Contracts and authorization foundation | Query, answer, citation, and conversation DTOs, plus active-user authorization requirements/policies for Q&A and conversation access. |
| 2. Authorized vector retrieval | Query embeddings from the configured embedding provider; model/dimension compatibility checks; tenant/document-access and optional document-scope filtering in the database query; pgvector cosine ranking and Top-K chunk results. |
| 3. Grounded answer generation | `IAnswerGenerationService` and Azure AI Foundry-backed implementation; bounded retrieved context; configurable deployment, token limit, timeout, and temperature; explicit insufficient-evidence response. |
| 4. Validated citations | Server-assigned citation labels; citation source metadata rebuilt from persisted, authorized chunk/document records; rejection of unsupported or malformed labels and non-fallback answers lacking a supplied label. |
| 5. Conversation persistence | Owner- and tenant-scoped conversations, turns, and citation snapshots; fresh retrieval on each turn; bounded prior user-message history; owner-authorized deletion with cascading removal of turns and snapshots. |
| 6. Quality telemetry and hardening | Retrieval and answer-generation telemetry, failure/outcome tracking, privacy-conscious structured logs, operational guidance, and five focused prompt/evidence/failure-telemetry tests. |

## Architecture summary

The API coordinates a deterministic retrieval-augmented generation flow. After authentication and conversation access checks, each query is embedded using the configured embedding model. The vector-search service applies authorization and embedding compatibility filters in the database query, ranks candidates using pgvector cosine distance, and returns Top-K chunks. The citation service reloads eligible chunk/document records under the document-access scope, assigns citation labels in retrieval order, and builds safe source excerpts. The answer-generation abstraction sends bounded context and the question to the configured Azure AI Foundry deployment. The API validates the returned citation labels, returns the answer and validated citations, and persists successful turns and citation snapshots when a conversation is used.

Retrieval and authorization remain application-controlled; the model does not choose documents or invoke retrieval tools. Each turn retrieves current evidence again. Prior assistant answers and prior citations are not evidence for a new answer.

## Authorization model

- Q&A and conversation operations require an authenticated active user under the configured policies.
- Document retrieval applies the current user's tenant and existing document-access rules as part of the database query. The documented rules include access to the user's own documents and permitted tenant-administrator access within that tenant.
- An optional document ID adds another database predicate and does not bypass tenant or ownership rules. Inaccessible or missing documents do not contribute retrieval results.
- Conversation reads, writes, and deletes are scoped to both the authenticated owner and tenant. A conversation outside that scope is not exposed as accessible.
- Citation metadata is reloaded from persisted records under document access checks; model-generated IDs and source metadata are not trusted.

## Retrieval architecture

The query embedding uses the configured provider and exact configured model name. The service validates configured and generated dimensions and excludes stored embeddings with incompatible model names or dimensions. PostgreSQL/pgvector performs cosine-distance ordering and applies Top-K before results are transferred to the API. The current score representation is derived from distance as `clamp(1 - distance, 0, 1)`. Optional document scope, tenant scope, and document access are applied before ranking results are returned.

The API returns ranked chunk results from vector search. The RAG answer flow defaults to retrieved chunks from vector search and can select hybrid RRF retrieval through `Rag:UseHybridSearch`; hybrid failures or empty results fall back to vector retrieval before the bounded context is prepared. Full-text and hybrid search services are implemented and exposed, and both were validated against the evaluation corpus. Optional document tags constrain retrieval before ranking. A subsequent increment added disabled-by-default Foundry reranking for hybrid RAG via `Rag:UseReranking`, with fallback to the original hybrid ranking; see the [reranking evaluation guide](../../validation/reranking-evaluation.md).

## Answer-generation architecture

`IAnswerGenerationService` separates answer generation from the RAG orchestration. `FoundryAnswerGenerationService` builds a bounded context (up to 4,000 characters) from the authorized retrieved evidence and delegates chat completion through the Foundry abstraction. The deployment name, output-token limit, timeout, and temperature are configurable.

The grounded system prompt limits factual answers to supplied context, treats retrieved material and prior user messages as untrusted data rather than instructions, prohibits unsupported facts, and requires the explicit `not found in accessible documents` response when evidence is insufficient. With model-knowledge fallback disabled (the default), empty usable context returns that response without calling Foundry. A later optional `Rag:AllowModelKnowledgeFallback` setting can instead generate a labeled, uncited model-knowledge answer after successful retrieval with no usable context; see the [README](../../../README.md#api-surface). Grounded answer labels are checked against the labels supplied in the bounded prompt before a successful response is built.

## Citation validation model

Citation numbers such as `[1]`, `[2]`, and `[3]` are assigned by the server to persisted, authorized evidence before the Foundry call. The model is instructed to use only those supplied labels. The API rejects malformed or unsupported labels and rejects a non-fallback answer that cites no supplied label. Citation IDs, source name, and safe excerpt come from persisted chunk/document records, not model output. Page number is currently null because the chunk model does not store page data.

Validation is syntactic: a supported label does not prove that the cited excerpt semantically supports every answer claim. That requires separate answer-grounding evaluation.

## Conversation persistence model

A RAG request may use an existing authorized conversation or create a new one. Successful user/assistant turns and validated citation snapshots are saved together. On each turn, retrieval is fresh and uses the current question. Up to six prior user messages and 2,000 characters are provided only to help resolve follow-up references; prior assistant answers are excluded from model history and are not retrieval evidence.

Conversation deletion is owner-authorized and cascades to turns and citation snapshots. No automatic retention period or scheduled purge worker is configured. Retention remains until owner deletion or an approved operational purge, so a tenant/legal retention policy and purge design must be decided before acceptance for production use.

## Telemetry and hardening

Retrieval telemetry includes duration, compatible/authorized candidate count, returned count, average similarity, model, and failure information. Candidate count is measured with an additional count query using the same authorized, model-compatible filters before ranking; this adds database work and should be monitored.

Answer-generation telemetry includes duration, exact retrieved-chunk count, validated citation count, Foundry provider/deployment, token counts when supplied by Foundry, success status, and distinct no-context, insufficient-evidence, generation-failure, and citation-validation-failure outcomes. Structured logs avoid questions, prompts, retrieved excerpts, source names, and answer text.

Operational guidance recommends monitoring p95/p99 latency, error and abstention baselines, candidate-to-returned distributions, citation metrics, and token/cost trends by deployment. Alert thresholds should be based on representative measurements, and Application Insights telemetry should be reviewed to confirm sensitive payloads are not collected.

The five focused `RagQualityHardeningTests` cover injection framing and prior-assistant exclusion, no-context fallback, insufficient-evidence abstention, Foundry failure telemetry, and citation-validation failure telemetry. These deterministic tests do not establish live-model resistance or answer quality.

## Validation status

- The full solution build succeeded for Increment 6.
- All five focused `RagQualityHardeningTests` passed, covering injection framing and prior-assistant exclusion, no-context fallback, insufficient-evidence abstention, Foundry failure telemetry, and citation-validation failure telemetry.
- The implementation notes and [acceptance-validation plan](acceptance-validation-plan.md) record validation requirements, operational examples, and remaining evidence gaps.

These results confirm compilation and the focused deterministic behaviors only. They are not database, live-model, or production acceptance evidence.

## Known limitations

- Hybrid retrieval and RRF are implemented and exposed. RAG retrieval can use hybrid mode through `Rag:UseHybridSearch`, but defaults to vector retrieval and falls back to vector on hybrid failure or empty results.
- Optional hybrid reranking is implemented but disabled by default; its quality, latency, and cost still require live evaluation. Relevance tuning remains evaluation-driven work.
- Citation labels are validated syntactically; semantic entailment is not guaranteed. Page numbers are unavailable because the chunk model has no page field.
- No automatic conversation retention period or purge worker is configured. Data remains until owner deletion or an approved operational purge.
- Candidate counting adds an additional database query and may increase retrieval cost/latency.
- Deterministic prompt tests do not prove live-model prompt-injection resistance or grounding quality.

## Remaining validation needs

1. **PostgreSQL and pgvector:** Run integration tests against the real Npgsql/EF model and pgvector extension. Verify query translation, model/dimension filtering, ranking, and tenant/document access isolation.
2. **Conversation database behavior:** Apply migrations and verify atomic turn/citation writes, deletion cascades, and owner/tenant isolation against PostgreSQL.
3. **Azure AI Foundry:** Validate deployment configuration, timeout/failure handling, token usage, response parsing, and end-to-end query behavior with an isolated deployment.
4. **Answer quality and security:** Evaluate representative questions for retrieval relevance, grounding, abstention, citation support, and adversarial prompt-injection behavior. The deterministic prompt tests do not establish live-model resistance.
5. **Performance and cost:** Measure representative p95/p99 retrieval and generation latency, token usage, and cost. Assess the extra candidate-count query and establish acceptable operational thresholds.
6. **Retention and operations:** Approve a tenant/legal retention period, define exceptions such as legal holds if applicable, and implement or operationalize a scoped purge process before relying on automatic expiry.
7. **Citation coverage:** Validate semantic support for citations using an approved answer-quality evaluation set.

## Recommendation for the next phase

Treat Phase 13 as **implementation-complete but not yet accepted**. Prioritize a dedicated acceptance and rollout-validation effort: complete PostgreSQL/pgvector and conversation-isolation integration tests, validate an isolated Foundry path, run quality and adversarial evaluations, set performance/cost baselines, and approve retention operations. Start the next feature phase after these gates have documented evidence and Phase 13 acceptance criteria are met.
