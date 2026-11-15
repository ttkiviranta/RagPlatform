# Phase 13 Increment 2 - Authorized Vector Retrieval

## Scope

This increment implements query embedding generation and pgvector ranking for the existing vector-search API. It does not generate an answer, call a chat model, populate citations, or change ingestion code.

## Request and result contract

`POST /api/search/vector` accepts the query and Top-K fields already present in `VectorSearchRequest`, plus optional `DocumentId` to limit search to a single document. Results remain ranked `VectorSearchResult` entries (document ID, chunk ID, snippet, and similarity score). No answer or citation contract is populated by this route.

## Embedding compatibility

The service uses the configured `IEmbeddingProvider` and exact configured `ModelName` for the query embedding and database filter. It checks that `Foundry:EmbeddingDimensions` is positive and that the generated query vector has exactly that dimension; an invalid configuration or mismatched provider result fails the request explicitly.

Persisted rows are eligible only when `ModelName` exactly matches the configured provider and persisted `VectorDimensions` equals the configured dimension. Other model deployments and mismatched-dimension rows are excluded from the ranking query; vectors are never compared across models or dimensions. Ingestion already validates persisted vector dimensions before writing them.

## Authorization enforcement

`DocumentAccessService.ScopeDocuments` builds an `IQueryable` predicate for the current active user that includes the exact tenant and existing role rules: users see their own documents, while permitted tenant administrators can see documents in their tenant. The vector query restricts embedding rows using a subquery of those authorized document IDs, so filtering is translated into the same PostgreSQL query that performs ranking; unauthorized chunks are not loaded and filtered afterward.

When `DocumentId` is supplied, the query adds that document ID to the same database predicate. The scope does not bypass the tenant/ownership rule; a missing or inaccessible document simply contributes no results, avoiding an existence/access distinction in the retrieval response.

## Ranking logic

The service uses pgvector's cosine distance operator (`CosineDistance`) in PostgreSQL. Results are ordered by ascending distance and limited with `Take(topK)` before transfer to the API. The existing score field is derived as `clamp(1 - distance, 0, 1)` for compatibility with the current response shape, and aggregate similarity continues to feed existing audit/telemetry metadata. Database order, not an in-memory sort, determines the Top-K ranking.

## Validation and limitations

- API project build is required for this increment.
- The repository's EF Core InMemory provider cannot map `Pgvector.Vector`; it cannot validate this database query. PostgreSQL with pgvector is required to exercise SQL translation, vector operators, authorization filters, and persisted vector dimensions end to end.
- No chat generation or citation population occurs in the vector-search operation.