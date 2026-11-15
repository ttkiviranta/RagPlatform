# Phase 12 Worker Rollout Gate

## Current gate

Keep `ServiceBus:DocumentImportWorkerEnabled=false`. The worker is opt-in and must not be enabled on a shared or production-like queue until PostgreSQL/pgvector and isolated Service Bus validation passes.

## Required pre-enable checks

- Build the release commit and execute a runnable test suite with meaningful discovered tests.
- Run database integration tests using PostgreSQL with pgvector, real migrations, Npgsql, and the application EF model.
- Verify database locking, atomic status/audit persistence, configured-model embedding completeness, and idempotent redelivery.
- Use a dedicated test queue, database, storage container, and identity. Verify identity permissions, queue lock duration, delivery limits, and DLQ access.
- Confirm monitoring for processing errors, queue age, delivery count, and dead letters; capture correlation IDs and document IDs without logging document content.

## Isolated end-to-end sequence

1. Upload a test PDF; verify `Queued`, staged Blob, and a published typed message after the database commit.
2. Consume the message; verify the document moves to `Processing`.
3. Verify extraction content and success audit persist before staging Blob deletion.
4. Verify a valid contiguous chunk set and `DocumentChunksGenerated` audit.
5. Verify configured-model embeddings, dimensions, pgvector rows, and `DocumentEmbeddingsGenerated` audit.
6. Verify locked completion re-checks evidence and atomically persists `Completed`, `UpdatedAt`, and `DocumentImportCompleted`.
7. Verify the worker completes the Service Bus message only after status, audit, and evidence checks.
8. Redeliver and verify processing is skipped and no duplicate completion audit or artifacts are produced.
9. Inject transient failure, poison envelopes, and permanent PDF failure; verify retry/DLQ behavior and that unrelated documents are not marked failed.

## Failure containment and rollback

- On any failed gate, stop/disable the isolated worker and preserve queue/DLQ contents plus failed identifiers and telemetry.
- Keep the production worker disabled. Fix the issue and rerun against disposable resources.
- For an incident after rollout, disable the worker first and revert the application to the last approved version. Preserve queued messages for safe redelivery; do not purge queues or manually force completion status.
- Do not roll back production schema or delete production data as part of application rollback without a separately approved data-recovery procedure.

## Enablement criteria

Enable only after all database and isolated-queue scenarios pass, the evidence is reviewed, operational monitoring is ready, and the rollout owner explicitly approves the target environment. Roll out with a small monitored batch before broad processing. Any integrity mismatch, repeated retry, unexpected DLQ growth, or authorization issue is a stop condition.