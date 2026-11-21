# Phase 12 Acceptance Validation Plan

## Purpose and current status

This plan captures the previously requested Phase 12 acceptance-validation scope using the available conversation summary and repository records. It specifies the minimum evidence required before enabling the document import worker. It is a plan, not proof that validation has passed.

Tasks 1-9 are implemented. Functional validation has confirmed successful PDF upload and worker processing, extraction, chunking, Azure AI Foundry embeddings, PostgreSQL/pgvector persistence, and vector/full-text/hybrid retrieval across five evaluation PDFs. The worker must remain disabled (`ServiceBus:DocumentImportWorkerEnabled=false`) until formal PostgreSQL transaction/locking acceptance, isolated Service Bus recovery/settlement evidence, and operational rollout gates pass and are reviewed.

## 1. Minimal validation before worker enablement

Before processing any shared or production-like queue:

- Build the intended release commit and run a test suite that discovers and executes meaningful tests. Record exact commands, test counts, failures, and known environment blockers.
- Use a disposable PostgreSQL instance with pgvector, the supported Npgsql EF provider, and real application migrations. Do not treat EF Core InMemory or SQLite as substitutes for pgvector validation.
- Verify database state transitions, row locking, transaction atomicity, audit persistence, configured-model embedding compatibility, and idempotency on redelivery/concurrent work.
- Use a dedicated test database, Blob Storage container, Service Bus namespace or isolated queue, and managed identity/service principal with least-privilege permissions. Verify lock duration, delivery limits, and authorized DLQ access.
- Confirm telemetry/alerts for processing failures, queue age, delivery counts, and dead letters. Retain correlation IDs and resource/document/message identifiers while excluding document content from logs.
- Keep the worker disabled in all non-test environments until the database and queue acceptance evidence is approved by the rollout owner.

## 2. PostgreSQL and pgvector integration-test strategy

### Environment and schema

1. Provision disposable PostgreSQL with the required pgvector extension and apply migrations to an empty database.
2. Verify the migration path from the supported prior schema, including `20261112090000_Phase12DocumentImportFoundation` and `20261113142000_AddEmbeddingBatchAuditMetadata`.
3. Confirm expected import status/metadata, chunks, embeddings, vector dimensions, uniqueness constraints, audit records, and indexes exist. Record database, pgvector, and migration versions.

### Persistence and atomicity cases

- Persist valid extracted content, a contiguous chunk set, configured-model vectors of the expected dimensions, and corresponding success audit records.
- Verify chunks and `DocumentChunksGenerated` audit are committed atomically; verify embeddings and `DocumentEmbeddingsGenerated` audit are committed atomically.
- Verify completion re-checks required content/chunk/embedding/audit evidence under the document lock. `Completed`, `UpdatedAt`, and `DocumentImportCompleted` must commit in one transaction.
- Inject failures before and during each persistence stage; verify partial writes are rolled back or remain in the documented retry-safe state, and the document cannot transition to `Completed` without complete evidence.
- Verify invalid/mismatched model names or vector dimensions cannot satisfy completion evidence and vectors are not mixed across embedding models/dimensions.

### Concurrency and idempotency cases

- Process the same document concurrently and redeliver a completed message. Verify one canonical chunk/embedding set, no duplicate audit/completion artifacts, and a valid final status.
- Verify PostgreSQL row-lock behavior and unique constraints under concurrent attempts; record any expected lock wait/retry behavior.
- Delete or make staging content unavailable at controlled points and verify the resulting status, audit, and retry outcome match policy without false completion.

## 3. Isolated Service Bus validation plan

Use a dedicated queue with manual settlement, `AutoCompleteMessages=false`, the configured concurrency/prefetch settings, a dedicated identity, and a disposable database/storage environment. Capture message ID/correlation ID, delivery count, settlement outcome, document status, audit identifiers, and DLQ reason without capturing PDF content.

Verify the following scenarios:

1. **Successful processing:** valid message reaches `Completed` only after all persistence and audit checks pass; broker message is completed afterward.
2. **Completed redelivery:** existing completed evidence is revalidated, processing is skipped safely, and no duplicates are created before settlement completion.
3. **Transient failure:** induce a dependency/database/transient processing error; message remains retryable through lock expiry and broker delivery limits, with no premature completion or false permanent failure.
4. **Poison envelope:** malformed or invalid message is dead-lettered with reason `InvalidDocumentImportMessage`; no unrelated document is marked failed.
5. **Permanent PDF failure:** invalid/permanently unreadable PDF results in the documented Failed state and audit, then the expected DLQ/settlement behavior.
6. **Settlement/persistence boundary:** interrupt after completion evidence is committed but before broker settlement; redelivery must validate and skip without duplicate completion records.
7. **Operational visibility:** confirm queue age, delivery count, processing failures, and DLQ monitoring and verify alerts trigger on test conditions.

Run only against isolated resources. Do not experiment with shared or production queues.

## 4. End-to-end acceptance sequence

Execute one traceable test case from API upload through broker settlement:

1. Upload a valid synthetic PDF; verify authorization, queued import status, private staging Blob, and document record.
2. Verify the typed Service Bus message is published only after the document transaction commits and contains the expected document, tenant, owner, staging key, and creation time metadata.
3. Consume the message; verify the document transitions to `Processing` and the consumer uses the expected correlation/document identity.
4. Extract PDF text; verify persisted extracted content and the extraction success audit before deleting the staging Blob.
5. Generate and persist the expected contiguous chunks; verify chunk metadata and `DocumentChunksGenerated` audit.
6. Generate embeddings with the configured deployment/model; verify every expected chunk has a compatible vector and correct dimensions persisted in pgvector with `DocumentEmbeddingsGenerated` audit.
7. Re-check completion evidence under the database lock; verify `Completed`, `UpdatedAt`, and `DocumentImportCompleted` are persisted atomically.
8. Verify the worker completes the Service Bus message only after the completed status, audit, and evidence checks succeed.
9. Redeliver the message and verify safe skip/idempotency and no duplicate chunks, embeddings, or completion audits.
10. Query import status and verify the API reports the persisted terminal status. Preserve sanitized trace evidence for every stage.

Repeat with controlled failure cases from the isolated queue plan; a single success trace does not validate retry, dead-letter, or concurrency behavior.

## 5. Rollback and failure containment

- If any pre-enable gate fails, keep or set `ServiceBus:DocumentImportWorkerEnabled=false`, stop the isolated processor, and preserve queue/DLQ contents plus sanitized identifiers and telemetry for diagnosis.
- Fix the root cause and repeat the failed scenario and relevant regression tests using disposable resources. Do not manually set a document to `Completed` or delete queued messages to make a test pass.
- If a problem occurs after a controlled enablement, disable the worker first and revert the application to the last approved version. Preserve queued messages for safe redelivery.
- Do not roll back production schema or delete production data during application rollback without a separately approved data recovery procedure.
- Re-enable only after the required gates pass, evidence is reviewed, operational monitoring is ready, and the rollout owner explicitly approves.

## 6. Production-readiness assessment

**Not production-ready for worker enablement until formal acceptance is complete.** Functional validation demonstrates the successful happy path for the imported corpus, but it does not by itself demonstrate fresh/upgrade migration acceptance, transactional completion under injected failures, redelivery idempotency, isolated Service Bus retry/DLQ/settlement behavior, or monitored rollout readiness. Existing InMemory failures occur before meaningful vector assertions and cannot substitute for database integration tests.

Additional operational limitations to address or explicitly accept include lock-expiry-based transient retry, cleanup for failed/abandoned staging Blobs, monitoring/alerting readiness, and controlled rollout/rollback ownership.

## 7. Acceptance criteria and evidence record

Phase 12 can be marked accepted for worker rollout only when all are met:

- Fresh and upgrade migrations pass on the supported PostgreSQL/pgvector stack.
- Integration tests verify content/chunk/embedding/audit persistence, model/dimension compatibility, transaction boundaries, completion evidence, and row-lock/unique-constraint idempotency.
- The full end-to-end upload-to-message-completion sequence passes on isolated resources.
- Redelivery, transient retry, poison-envelope DLQ, permanent-PDF failure, and settlement interruption scenarios pass with expected statuses, audit events, and no duplicate artifacts.
- Import-status responses reflect persisted states, including failure and completion.
- Monitoring and alerting are demonstrated; logs do not include document content; storage/queue/database identities use the intended permissions.
- Rollback/disable procedure is exercised and the rollout owner approves the target environment.

Attach a sanitized evidence record containing build revision, test commands/counts, database and extension versions, migration identifiers, test resource isolation, message/document correlation identifiers, status/audit outcomes, queue/DLQ counts, relevant telemetry, and reviewer approval. Do not include credentials or document text.

Until these criteria pass, report Phase 12 as **implementation complete; rollout validation pending**, and keep the worker disabled.
