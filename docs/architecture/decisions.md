# Consolidated Architecture Decisions

This file indexes cross-phase decisions documented in the repository. Phase-specific details and source references are in `docs/phases/`.

## Application and persistence

- ASP.NET Core Web API is the backend application boundary; the current MVP keeps the import worker in the API project.
- PostgreSQL and EF Core migrations are the operational data store and schema evolution mechanism.
- pgvector stores and searches document embeddings. RagPlatform owns document, chunk, embedding, and vector data.

## Identity, authorization, and audit

- Microsoft Entra ID authenticates API clients; local application users, roles, ownership, and tenant boundaries authorize resources.
- Document access is owner-scoped, with tenant roles for elevated document management. Conversation access is strictly owner-only and does not inherit tenant-wide document permissions.
- Authorization filters are enforced server-side. Q&A must only retrieve authorized chunks; a model prompt is not an authorization boundary.
- Durable audit events are distinct from Application Insights/OpenTelemetry. Audit and telemetry omit document, prompt, message, and model-response content.

## Foundry boundary

- Azure AI Foundry provides embedding and chat inference only; RagPlatform retains documents, vectors, retrieval, and audit state.
- Use Entra-backed `DefaultAzureCredential`; no model API key is stored in source-controlled configuration.
- Embeddings are model/deployment-specific; search only compares vectors from the configured compatible deployment.

## Ingestion and settlement

- Uploads stage PDFs temporarily in Blob Storage and publish typed messages to Service Bus after the database transaction commits.
- The in-process worker performs extraction, chunking, and embedding persistence. It uses PostgreSQL transactions/row locks and uniqueness constraints to support redelivery.
- A message is completed only after persisted completion status, audit, and evidence are validated. Transient errors remain retryable; poison envelopes and permanent PDF failures have distinct dead-letter rules.
- No outbox or separate worker project is part of the current MVP. The worker remains disabled until database and isolated-queue validation passes.

## Q&A direction

- Phase 13's intended first release is a deterministic application-owned retrieve-and-answer pipeline, not an autonomous agent.
- Query embedding must be compatible with indexed document embeddings; retrieval applies authorization before ranking.
- Citation labels and metadata must be generated and validated by the API. Conversation history remains owner-scoped and bounded.
- Hybrid retrieval, reranking, caching, streaming, and agents are conditional future enhancements, not assumed initial scope.

## Decisions not fully documented

No formal ADR numbering, decision owners, alternatives matrix, or review/expiry process exists in the repository. The statements above are extracted from README architecture sections and the Phase 13 planning discussion; they are not substitutes for signed ADRs.