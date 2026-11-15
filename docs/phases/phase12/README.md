# Phase 12 - Document Import and Knowledge Ingestion

## Status

Tasks 1-9 are implemented, documented, committed, and pushed. Operational acceptance remains pending: PostgreSQL/pgvector integration validation and isolated Service Bus end-to-end validation are not complete. The worker must remain disabled until these rollout gates pass.

## Implementation summary

1. Blob Storage and Service Bus options, configuration, and dependency injection.
2. Import fields and EF mappings/migration.
3. PDF validation, private Blob staging, and queued document creation.
4. Typed Service Bus message published after the document transaction commits.
5. Opt-in in-process `BackgroundService`/`ServiceBusProcessor` consumer.
6. PdfPig text extraction, success/failure audit and telemetry, and staged-blob cleanup after successful persistence.
7. Validated chunk creation and reuse under a PostgreSQL document row lock; chunk records and success audit commit together.
8. Configured-model embedding generation/reuse and pgvector persistence; generation occurs before the persistence transaction, with locked recheck and uniqueness protection. Embeddings and the success audit commit together.
9. Evidence-checked `Completed` transition and audit, settlement completion, poison-envelope classification, and import-status endpoint.

## Architecture decisions

- Keep the worker in `RagPlatform.Api`; no separate worker project or outbox pattern is used in this MVP.
- Use Blob Storage only for temporary staging. The original PDF is not retained as a document asset after successful extraction.
- The typed message carries `DocumentId`, `TenantId`, `OwnerUserId`, `StagingBlobKey`, and `CreatedAtUtc`.
- Use PostgreSQL `SELECT ... FOR UPDATE`, transaction boundaries, and unique constraints to protect chunk/embedding creation and completion against redelivery/concurrent writes.
- Validate stored content, chunks, configured-model embeddings/dimensions, and success audit evidence before setting `Completed`.
- Persist `Completed`, `UpdatedAt`, and `DocumentImportCompleted` atomically; complete the Service Bus message only after revalidating evidence.
- Completed redelivery validates and skips prior processing. Malformed envelopes dead-letter with fixed safe reason `InvalidDocumentImportMessage` without marking an unrelated document `Failed`.
- Permanent PDF errors retain the persisted-Failed-plus-audit rule. Transient errors remain unsettled for retry using lock expiry and broker delivery limits.

## Migrations, Azure resources, and commits

- Migrations: `20261112090000_Phase12DocumentImportFoundation` and `20261113142000_AddEmbeddingBatchAuditMetadata`.
- Documented staging container: `document-staging`; documented Service Bus queue: `document-import`.
- The README includes a successful upload/message-publish validation reference (`documentId` `53bb6d68-427e-4806-a39e-ad1d46a50710`; active queue message count `1`). This is publish evidence only, not a completed import acceptance test.
- No actual Azure subscription, resource group, storage account, Service Bus namespace, or Foundry resource names are documented.
- Plan/architecture history: `0259f4d`, `53985c8`, `58f54f0`.
- Implementation commits: `8dc5ab7` (messaging), `9e13ee5` (consumer), `aa71f6d` (extraction), `9b0bc99` (chunking), `7c641bd` (embedding), `d988f38` (Task 9).

## Validation status and known limitations

- Builds are documented as successful for the implementation tasks.
- EF Core InMemory cannot map `Pgvector.Vector`; affected tests fail during model initialization before assertions. Those failures do not validate PostgreSQL behavior. SQLite is not a substitute for Npgsql/pgvector semantics.
- PostgreSQL/pgvector integration tests and isolated Service Bus end-to-end completion, redelivery, retry, and dead-letter validation remain pending.
- The processor uses manual settlement, `AutoCompleteMessages = false`, `MaxConcurrentCalls = 1`, and `PrefetchCount = 0`. Transient messages are not explicitly abandoned; retry depends on lock expiry and delivery limits and requires isolated testing.
- Staging-aware document deletion and abandoned-blob cleanup remain unfinished. Failed PDF imports may retain staging blobs; a Blob lifecycle rule is identified as a cleanup option.
- Worker configuration is `ServiceBus:DocumentImportWorkerEnabled=false` by default. Do not enable it on a shared or production-like queue before the validation gates pass.

## Acceptance evidence still required

Use the [Phase 12 worker rollout gate](../../rollout/phase12-worker.md) and [validation status](../../validation/acceptance-status.md). Required evidence includes real PostgreSQL/pgvector persistence and transaction tests, plus isolated-queue success, redelivery, retry, poison-envelope, and permanent-PDF failure results.