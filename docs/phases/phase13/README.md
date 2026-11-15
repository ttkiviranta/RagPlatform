# Phase 13 - Document Q&A

## Goal and planned workflow

Provide authenticated document Q&A over the imported corpus. The planned request flow is: authenticate and resolve user scope; validate the query and owned conversation; embed the question with the configured compatible embedding model; retrieve only accessible chunks; rank and budget context; call Azure AI Foundry chat; validate server-created citations; and return an answer with sources. Persist conversation turns and citations only under owner-controlled access.

## Architecture decisions recorded in the planning discussion

- Keep retrieval and authorization in the API, not as model-controlled tools.
- Apply tenant, owner, document-ACL, and optional document-scope filters in the database query before returning chunks.
- Start with pgvector similarity search; add hybrid search or reranking only when evaluation demonstrates a benefit.
- Assign citation labels and source metadata server-side; never trust model-generated document IDs or locations. Abstain when retrieved evidence is insufficient.
- Keep conversation history application-managed, owner-scoped, bounded, and freshly authorized on each turn.
- Begin with a deterministic single-agent pipeline; agent orchestration is not required for the initial Q&A feature.
- Measure retrieval quality, grounded-answer quality, latency, and cost before accepting the feature.

## Increment 1 - contracts and authorization

The repository README records Increment 1 as complete: query/answer/citation/conversation contracts and named active-user policies for Q&A and conversation access. The existing query/conversation request and response DTOs are reused; a typed citation DTO is added to the response contracts. Conversation ownership remains enforced by existing services, and document-derived retrieval must remain document-authorized.

The associated API build succeeded in the working tree at archive time. The DTO/policy source edits were uncommitted and are not included in the documentation archive commit. This increment does not establish citation population, conversation citation persistence, retrieval implementation, or full Phase 13 acceptance.

## Remaining increments and acceptance

- Implement authorized vector retrieval and prove cross-user/tenant isolation.
- Add Foundry grounded answer generation with safe timeouts/errors and evidence-based abstention.
- Populate validated citations from persisted chunk/document metadata.
- Persist citations with owner-protected conversation turns and apply retention/deletion rules.
- Evaluate retrieval, answer grounding, prompt-injection resistance, latency, reliability, and cost against explicit targets.

## Missing documentation

No standalone Phase 13 plan, ADR, model-selection evaluation, test plan, rollout report, completion report, or retrospective was found in the repository. The archive summarizes the planning discussion and the README's Increment 1 note; future acceptance results must be added when produced.