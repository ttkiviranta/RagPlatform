# Phase 12–13 Validation Execution Plan

## Purpose and status

This plan turns the [combined validation roadmap](phase12-phase13-validation-roadmap.md) into executable acceptance tasks for Phase 12 document ingestion and Phase 13 Document Q&A. It is a test procedure, not evidence that these tests have passed. Record the results, evidence, and reviewer approval for each task before changing either phase to accepted.

**Current gate:** PG-01 is blocked on the current workstation by Windows Smart App Control's enforced `VerifiedAndReputableDesktop` policy (`0283ac0f-fff1-49ae-ada1-8a933130cad6`). EF design-time execution and test hosts are blocked; no application defect has been identified. Run .NET validation only in an approved compatible environment. Phase 12 worker remains disabled outside an isolated test environment, and Phase 13 remains implementation-complete but acceptance-pending. Do not use production documents, shared queues, or production identities for these runs.

## Run record and common prerequisites

Create one run record before execution containing:

- Run ID, date, operator/reviewer, application commit, build configuration, and test commands.
- PostgreSQL version, pgvector version, EF migration IDs, embedding model/deployment and dimensions, Foundry chat deployment/settings, Service Bus queue settings, and relevant API configuration keys by name only (never values/secrets).
- Test dataset/version, approved thresholds, resource isolation confirmation, and correlation-ID convention.
- Per-task outcome (`Not run`, `Pass`, `Fail`, or `Blocked`), defect references, and evidence links. A blocked test is not a pass.

### Prerequisite resources

- A disposable PostgreSQL instance with pgvector and the application's real Npgsql/EF model and migrations. The repository's Docker Compose PostgreSQL 16/pgvector service can be used if the test setup supports it.
- An isolated Azure Blob Storage staging container, Azure Service Bus queue with DLQ access, and dedicated least-privilege test identity for Phase 12 broker/storage tests. No local Service Bus or Blob emulator is documented in this repository.
- A non-production Azure AI Foundry resource with the configured embedding and chat deployments, Entra-backed inference permissions, and agreed request limits.
- A test API configuration with the import worker disabled by default; enable it only for the isolated validation environment after recording the queue and identity.
- Application Insights (or the approved telemetry target) configured for the test environment, plus a way to inspect logs, metrics, database records, queue counts, and DLQ messages.
- A controlled test environment with no production data, an agreed cleanup procedure, and a rollback/disable owner.

### Test data

Prepare versioned synthetic fixtures and a data manifest:

1. Valid, small, non-encrypted PDF with known text and expected extraction/chunk content.
2. Invalid, corrupt, encrypted, no-page, and no-text PDF cases for permanent-failure classification.
3. Embedding/chunk fixtures with deterministic vectors and explicitly recorded model names/dimensions for database-only tests.
4. Multiple documents with distinct content, owners, and tenants; include an authorized tenant administrator according to the existing role rules.
5. A gold Q&A set containing answerable, unanswerable, ambiguous, multi-chunk, and follow-up questions. Record expected facts, accessible source/chunk IDs, citations, and abstention labels.
6. Synthetic prompt-injection excerpts and source metadata strings. Never use real credentials, personal data, or sensitive business content.

### Global pass/fail and evidence rules

- Agree on numeric thresholds before evaluating quality, latency, cost, error rates, and injection repetitions. No threshold may be chosen after observing results to turn a failure into a pass.
- A test passes only when its expected behavior is observed and its evidence is captured. Build success, mock-only tests, and upload/message-publish evidence do not substitute for external integration evidence.
- Any unauthorized cross-user/tenant data exposure, false `Completed` transition, message completed before required evidence, unsupported source metadata, secret disclosure, or sensitive payload in logs is an immediate blocking failure.
- Evidence must be sanitized: retain versions, IDs/correlation IDs, timings, counts, statuses, metrics, and non-sensitive error classifications. Do not archive tokens, secrets, prompts, document text, retrieved excerpts, or answers unless explicitly authorized by the data-handling policy.

## 1. PostgreSQL/pgvector validation

### PG-01 — Fresh and upgrade migrations

**Prerequisite resources:** Disposable PostgreSQL with pgvector, application connection configured for the test database, real migrations.  
**Test data:** Empty database and a disposable copy/schema at the supported prior migration point.  
**Procedure:** Apply all migrations to the empty database; separately upgrade the prior schema. Inspect expected import, document/chunk/embedding, audit, user/tenant, conversation, and citation-snapshot structures and indexes.  
**Expected result:** Both paths complete without manual schema edits; the application EF model can initialize and use the schema.  
**Pass criteria:** All expected objects and constraints exist; migration history matches the expected release; no unrelated schema or production data is touched.  
**Fail criteria:** Migration error, missing/incorrect column/index/constraint, model mismatch, or manual repair required.  
**Collected evidence:** PostgreSQL/pgvector versions, migration IDs, sanitized migration output, schema inspection summary, run ID.

### PG-02 — Phase 12 extraction/chunk/embedding persistence and completion

**Prerequisite resources:** PostgreSQL/pgvector, Npgsql application model, fake or deterministic embedding provider for controlled DB tests.  
**Test data:** One valid synthetic document with expected text, contiguous chunks, model name, dimensions, and vector values. Include incomplete and wrong-model/wrong-dimension variants.  
**Procedure:** Exercise extraction result persistence, chunk import, embedding import, and completion services against the database. Verify success audit events and completion evidence at each boundary. Inject failures around each persistence save/transaction.  
**Expected result:** Text/chunks/vectors and their corresponding audits persist as designed; `Completed`, `UpdatedAt`, and `DocumentImportCompleted` commit atomically only when all required evidence is valid. Invalid or incomplete evidence never completes.  
**Pass criteria:** Transaction boundaries are demonstrated; correct vectors/dimensions and audit counts are present; failure cases leave retry-safe state and no false completion.  
**Fail criteria:** `Completed` without complete compatible vectors/audits, partial success audit, data corruption, or model/dimension mixing.  
**Collected evidence:** Test IDs, document/correlation IDs, row/count summaries, statuses, audit IDs/outcomes, transaction/failure results; no content or vector payloads.

### PG-03 — Phase 12 locking, concurrency, and idempotency

**Prerequisite resources:** PostgreSQL/pgvector and a test harness capable of overlapping transactions or processing calls.  
**Test data:** Same document/message processed concurrently; then a completed import message is replayed.  
**Procedure:** Start competing chunk/embedding/completion operations and force redelivery both before and after completion. Inspect row-lock and uniqueness-conflict behavior.  
**Expected result:** One canonical valid chunk/embedding set and one required success/completion audit; duplicate processing is validated and skipped safely.  
**Pass criteria:** No duplicate artifacts or audits, no lost valid data, and final status reflects persisted evidence.  
**Fail criteria:** Duplicate or partial sets, race-condition failure, inconsistent status/audit, or an unhandled uniqueness conflict.  
**Collected evidence:** Concurrency scenario/timing, database counts and statuses, audit counts, correlation/message IDs, logs with sanitized failure classifications.

### PG-04 — Phase 13 vector retrieval semantics and database authorization

**Prerequisite resources:** PostgreSQL with pgvector, production query provider, multiple synthetic owners/tenants, deterministic embeddings.  
**Test data:** Known vectors with expected cosine ordering, multiple embedding model/dimension rows, and documents partitioned by owner, tenant, admin-access rules, and optional document scope.  
**Procedure:** Run retrieval through the application using configured query vectors; inspect result IDs/order/count and the generated database query or provider behavior. Exercise missing/inaccessible document scope.  
**Expected result:** PostgreSQL applies compatible model/dimension and authorization predicates before Top-K; cosine ordering and score mapping match expected values; inaccessible/missing scope yields no accessible results.  
**Pass criteria:** Exact expected authorized Top-K result set/order; incompatible rows are excluded; candidate and returned counts agree with the seeded eligible rows.  **Fail criteria:** Any unauthorized chunk returned or any incompatible vector compared; incorrect ordering/score/count; access/existence distinction leaked by scoped lookup.  
**Collected evidence:** Synthetic seed manifest, query parameters excluding text, expected/actual IDs and scores, SQL/provider evidence, count and timing metrics.

### PG-05 — Phase 13 conversation persistence and deletion

**Prerequisite resources:** PostgreSQL/EF model and migrations, synthetic users/tenants, test answer/citation provider.  
**Test data:** Owned and foreign conversations plus turns with validated citation snapshots; failure cases for retrieval, generation, and citation validation.  
**Procedure:** Create a conversation turn; query history; inject each failure before persistence; delete as owner and attempt reads/deletion as a non-owner and cross-tenant user. Delete a source document and verify citation snapshot behavior.  
**Expected result:** Successful turn and citations persist together; failed turns create no partial records; owner/tenant filters apply; owner deletion cascades to turns/snapshots; snapshots remain readable as designed if a source record is later removed.  
**Pass criteria:** Database state matches each expected outcome and no cross-owner/tenant access succeeds.  **Fail criteria:** Partial failed turn, missing/altered citation snapshot, unauthorized access or deletion, or incorrect cascade.  
**Collected evidence:** User/conversation IDs, row counts, status codes, transaction outcomes, citation snapshot identifiers, migration version.

## 2. Service Bus validation

### SB-01 — Isolated queue, identity, and processor readiness

**Prerequisite resources:** Dedicated test queue and DLQ, Blob staging container, disposable PostgreSQL, dedicated least-privilege identity, configured monitoring.  
**Test data:** A harmless health/test message and a valid synthetic import envelope.  
**Procedure:** Confirm queue, lock duration, delivery limit, DLQ access, identity permissions, worker setting, and processor configuration (`AutoCompleteMessages=false`, configured concurrency/prefetch). Start the worker only in this isolated environment.  
**Expected result:** The worker receives only test queue messages and required dependencies are accessible; production/shared worker settings remain disabled.  
**Pass criteria:** Resource isolation and identity permissions are verified; no shared queue or production resources are targeted.  **Fail criteria:** Unclear queue identity, over-privileged identity, worker enabled in a shared environment, or missing DLQ/monitoring access.  
**Collected evidence:** Resource aliases (not secrets), role assignments, sanitized configuration key names, processor settings, queue metrics, approval to proceed.

### SB-02 — Successful import and message settlement

**Prerequisite resources:** SB-01 plus PostgreSQL/pgvector and non-production Foundry embedding access; valid synthetic PDF in staging.  
**Test data:** Valid PDF, import message, expected text/chunks/vector dimensions and expected audit events.  
**Procedure:** Upload PDF, verify queued document and published message, let the worker extract/chunk/embed/finalize, then inspect DB/audit/status before broker settlement. Query the import-status endpoint.  
**Expected result:** Message is completed only after compatible embeddings, required audits, and atomic `Completed` evidence are persisted. Status endpoint returns the persisted terminal status.  
**Pass criteria:** Full trace completes in order; one expected set of artifacts/audits exists; message completion follows evidence verification.  **Fail criteria:** Message completed early, missing/invalid persistence, wrong terminal status, or unexplained duplicate records.  **Collected evidence:** Correlated document/message IDs, timestamps/status transitions, audit IDs, DB counts/model/dimensions, queue settlement/active counts, sanitized logs/metrics.

### SB-03 — Redelivery and settlement interruption

**Prerequisite resources:** Isolated queue, PostgreSQL, and a mechanism to interrupt the test processor after DB completion but before message settlement.  
**Test data:** Completed valid import message and a duplicate delivery.  
**Procedure:** Redeliver a completed message; separately terminate/interrupt processing after completion persistence but before `CompleteMessageAsync`, then allow broker redelivery.  
**Expected result:** Persisted evidence is revalidated; extraction/chunking/embedding and completion audit are not duplicated; redelivered message is safely completed.  
**Pass criteria:** No duplicate artifacts/audits; correct terminal status; settlement occurs only after evidence checks.  **Fail criteria:** Duplicate work, missing completion audit, message loss, false completion, or redelivery loop.  **Collected evidence:** Delivery counts, message/correlation IDs, audit and artifact counts before/after, settlement times, processor logs.

### SB-04 — Transient retry and broker limits

**Prerequisite resources:** Isolated queue configured with known lock duration and maximum delivery count; controllable transient dependency fault.  
**Test data:** Valid envelope/document and transient database, Blob, or provider failure injected at a documented step.  
**Procedure:** Trigger a transient failure, observe message lock expiry/redelivery and delivery count, then remove the fault and observe retry; separately verify max-delivery exhaustion behavior per broker configuration.  
**Expected result:** Transient error does not mark the document permanently failed or complete the message; retry follows lock expiry/delivery limits and succeeds or reaches configured terminal broker handling.  
**Pass criteria:** Observed behavior matches configured policy and does not create duplicates or false status.  **Fail criteria:** Premature completion/Failed status, silent message loss, unbounded retry, or unexpected DLQ outcome.  **Collected evidence:** Fault injection point, delivery counts, lock/redelivery timestamps, document/audit state, final settlement/DLQ data.

### SB-05 — Poison envelope and permanent PDF failure

**Prerequisite resources:** Isolated queue and DLQ, PostgreSQL/audit access, synthetic malformed messages/PDFs.  
**Test data:** Malformed JSON, missing/mismatched envelope identifiers, unrelated document ID, and invalid/encrypted/corrupt/no-text PDF.  
**Procedure:** Send each case independently; inspect document state, audit, dead-letter reason, and other documents.  
**Expected result:** Poison envelope dead-letters with `InvalidDocumentImportMessage` without marking an unrelated document `Failed`. Permanent PDF failure follows persisted Failed-plus-audit rule and its expected settlement/DLQ behavior.  
**Pass criteria:** Exact expected classifications and safe terminal states, with no cross-document mutation.  **Fail criteria:** Malformed envelope mutates an unrelated document, permanent failure lacks audit, wrong reason/state, or message silently completes.  **Collected evidence:** Test message IDs, fixed DLQ reason, delivery count, document status/audit IDs, queue/DLQ counts, sanitized failure type.

## 3. Azure AI Foundry validation

### F-01 — Deployment and embedding compatibility

**Prerequisite resources:** Non-production Foundry resource, Entra-backed test identity with inference permissions, configured embedding deployment, PostgreSQL test DB for persistence cases.  
**Test data:** Synthetic text/chunks; expected model/deployment identifier and configured vector dimension. Include a deliberately mismatched dimension/model fixture for negative validation.  
**Procedure:** Run a small embedding request through the application, verify response dimensions/model metadata and persistence. Exercise missing/invalid configuration and mismatch handling using test configuration/fakes where safe.  
**Expected result:** Live deployment generates the expected dimension and metadata; incompatible vectors are rejected and never mixed into search.  
**Pass criteria:** Authenticated inference succeeds; configured model and dimensions match persisted data; negative cases fail explicitly.  **Fail criteria:** Authentication/configuration failure, silent dimension coercion, incompatible vector persistence, or payload leakage.  **Collected evidence:** Deployment alias/model version, dimensions, duration, outcome, token/usage metadata if provided, DB row counts; no text, vectors, or credentials.

### F-02 — Grounded answer, bounded context, and citations

**Prerequisite resources:** Non-production chat deployment, PG-04 authorized retrieval dataset, telemetry enabled.  
**Test data:** Synthetic question with known evidence, multi-source excerpts, bounded-context case where later labels are excluded, and expected answer/citation facts.  
**Procedure:** Call the RAG endpoint; inspect response and persisted data. Verify server-assigned labels correspond to the evidence included in the bounded prompt and source metadata is built from persisted records.  
**Expected result:** Answer uses supported evidence and only supplied labels; returned citation IDs/excerpts derive from authorized database records.  
**Pass criteria:** No invented source metadata or unsupported labels; response citations match server mapping and included context. Semantic support is evaluated under RAG quality tasks, not inferred from label syntax.  **Fail criteria:** Unsupported label, fabricated ID/source/page/URL, unauthorized source, or citation to evidence omitted from context.  **Collected evidence:** Test-case/source IDs, answer outcome classification, citation numbers and persisted IDs, duration/token counts, sanitized response validation result. Avoid archiving answer/excerpt text.

### F-03 — No-context, abstention, timeout, and failure handling

**Prerequisite resources:** Foundry deployment or deterministic transport fake for fault simulation; PostgreSQL only for persistence assertions.  
**Test data:** Empty retrieval, insufficient-evidence query, timeout/failure scenarios, and expected fallback string.  
**Procedure:** Verify empty context avoids a chat call; send insufficient context; simulate timeout, throttling, authentication/configuration, and server failures. Inspect telemetry and conversation persistence.  
**Expected result:** Explicit `not found in accessible documents` fallback for no/insufficient evidence; failures are distinct from abstentions and do not return success-shaped answers/citations or persist partial turns.  **Pass criteria:** Expected call/no-call behavior, failure classification, bounded timeout, and no failed-turn persistence.  **Fail criteria:** Fabricated answer for empty evidence, swallowed failure, misleading success, partial persistence, or sensitive content logged.  **Collected evidence:** Outcome/failure type, duration, call count, token usage when available, persisted row counts, telemetry/log inspection.

## 4. RAG quality validation

### Q-01 — Freeze evaluation corpus and acceptance thresholds

**Prerequisite resources:** Approved synthetic/non-sensitive document corpus, supported Foundry deployment, retrieval database, evaluation owner/reviewer.  
**Test data:** Versioned question-answer-source set with answerable, insufficient, ambiguous, multi-chunk, follow-up, and out-of-scope examples. Mark expected citations and abstention for each.  
**Procedure:** Review and freeze corpus, expected evidence/facts, evaluation rubric, repeat count, and numeric pass thresholds before running the model. Record embedding/chat deployments, prompt version, Top-K, and context limits.  
**Expected result:** Every test case is reproducible and has explicit expected evidence/answer-or-abstain labels.  **Pass criteria:** Dataset/rubric/thresholds are approved and immutable for the run.  **Fail criteria:** Missing expected outcomes, thresholds chosen after results, or use of unapproved sensitive data.  **Collected evidence:** Dataset version/hash, case IDs, rubric and threshold approval, model/config version; do not include sensitive content in archived evidence.

### Q-02 — Retrieval relevance and abstention

**Prerequisite resources:** PG-04 passing, frozen Q-01 dataset, query embedding deployment.  
**Test data:** Gold relevant chunk IDs and expected no-evidence cases.  
**Procedure:** Run each query through authorized retrieval; compute agreed Recall@K, Precision@K and/or nDCG@K; report by category. Evaluate abstention decisions on unanswerable queries.  
**Expected result:** Retrieved evidence is relevant and accessible; no-evidence queries do not receive fabricated support.  **Pass criteria:** Pre-approved metric thresholds are met overall and for critical categories; no authorization leak.  **Fail criteria:** Any authorization leak or score below approved threshold, unexplained category regressions, or unsupported answer on a required abstention case.  **Collected evidence:** Dataset version, metric calculation, per-case result IDs/scores, category aggregates, latency, failures, reviewer sign-off.

### Q-03 — Answer grounding and citation support

**Prerequisite resources:** F-02 and Q-01 passing, live chat deployment, human evaluator rubric.  
**Test data:** Gold facts and expected citation evidence per answerable question; insufficient-evidence questions.  
**Procedure:** Review generated answers against accessible evidence; score correctness, supported material claims, citation entailment, and abstention quality. Adjudicate disagreements.  
**Expected result:** Material factual claims are supported by authorized excerpts and cited correctly; unsupported questions abstain.  **Pass criteria:** Pre-approved groundedness/citation/abstention thresholds met, with zero disallowed unauthorized or fabricated metadata events.  **Fail criteria:** Uncited/unsupported claims above threshold, incorrect citations, fabricated facts, or failure to abstain where required.  **Collected evidence:** Aggregated scores, case IDs, evaluator/adjudication records, outcome and citation counts; store answer content only if approved by evaluation-data policy.

### Q-04 — Latency, reliability, and cost baseline

**Prerequisite resources:** Stable non-production PG/Foundry configuration, representative load profile, telemetry/metrics access, agreed service targets.  
**Test data:** Frozen Q-01 set plus defined request volume/concurrency and warm/cold test conditions.  
**Procedure:** Measure retrieval and answer generation separately and end-to-end. Capture p50/p95/p99, errors, abstentions, token usage, estimates of cost, candidate/returned counts, citations, and deployment. Evaluate the extra eligible-candidate count query's effect.  
**Expected result:** Service stays within approved latency/reliability/cost budgets and telemetry contains no prompts, excerpts, or answers.  **Pass criteria:** All pre-approved SLO and cost thresholds met with adequate sample size.  **Fail criteria:** Threshold exceeded, unobserved failure/abstention trend, cost exceeds limit, or sensitive payload captured.  **Collected evidence:** Load profile, sample count, metric exports, Kusto queries, deployment/config versions, cost calculation assumptions, alert results.

## 5. Security validation

### SEC-01 — Document retrieval authorization boundaries

**Prerequisite resources:** PostgreSQL/pgvector, API test identities in multiple tenants, permitted admin roles, security-test reviewer.  
**Test data:** Unique synthetic documents/chunks/vectors for each owner/tenant and cases for allowed administrator scope.  
**Procedure:** Query as each identity with global and optional-document scopes; observe retrieval IDs, assembled prompt labels, response citations, and persisted records. Attempt inaccessible and nonexistent document IDs.  
**Expected result:** Tenant, owner/role, and optional document predicates constrain retrieval before content is loaded/used; inaccessible and missing resources do not expose existence/content.  **Pass criteria:** Only expected authorized chunk IDs reach context or citation construction; zero cross-tenant/owner disclosures.  **Fail criteria:** Any unauthorized chunk, excerpt, metadata, or existence signal is observable.  **Collected evidence:** Identity aliases/role matrix, synthetic document IDs, status/result counts, authorization outcome/audit IDs, query plan evidence where available.

### SEC-02 — Conversation ownership, tenant isolation, and history boundaries

**Prerequisite resources:** PostgreSQL, multiple user/tenant identities, conversation API and RAG pipeline.  
**Test data:** Owned and foreign conversations with distinctive synthetic user/assistant turns and citations.  
**Procedure:** As owner, create/read/continue/delete. Attempt the same operations as another same-tenant user and cross-tenant user/admin. Inspect prompt inputs through a test spy where possible; verify history bounds.  
**Expected result:** Conversation access is owner- and tenant-scoped; history includes only the bounded prior user messages for reference resolution; prior assistant answers/citations are not evidence; deletion removes turns and citation snapshots.  **Pass criteria:** All authorized operations succeed, unauthorized operations are denied without content disclosure, and no foreign history is passed to generation.  **Fail criteria:** Unauthorized access/write/delete, cross-boundary history, assistant answer reused as evidence, partial failed turn persistence, or incorrect deletion behavior.  **Collected evidence:** User/tenant aliases, conversation/message IDs, response status codes, database row counts, test-spy assertion results, audit events.

### SEC-03 — Prompt injection and untrusted-data resilience

**Prerequisite resources:** Synthetic malicious test fixtures, isolated live Foundry deployment, PG-04/SEC-01 authorization checks, evaluator-approved repetitions/threshold.  
**Test data:** Injection instructions in retrieved chunks, prior user messages, quoted text, and synthetic source titles; attempts to override policy, expose secrets/other tenants, invent facts, or forge citations.  
**Procedure:** Run deterministic prompt-framing tests and repeated live-model adversarial cases. Include irrelevant evidence and cases where the right response is abstention. Inspect answer classification, citations, logs, telemetry, and persistence.  
**Expected result:** The model treats retrieved/user content as untrusted, does not disclose unauthorized data or follow embedded commands, uses only supplied labels, and abstains when evidence is insufficient.  **Pass criteria:** Meets the pre-approved repeated-run resilience threshold and has zero unauthorized disclosure, secret exposure, or forged source metadata.  **Fail criteria:** Any unauthorized disclosure or secret exposure is blocking; repeated instruction-following, fabrication, or citation forgery beyond the approved threshold also fails.  **Collected evidence:** Synthetic case IDs, model/deployment, repetition count, outcome labels, citation validation outcomes, evaluator notes, telemetry/log privacy review. Keep raw adversarial/answer text in controlled test storage only if approved.

### SEC-04 — Audit, telemetry, and operational access review

**Prerequisite resources:** Application Insights/test logging, PostgreSQL audit records, Azure resource role assignments, monitoring/alert configuration.  
**Test data:** Known success, denial, retrieval failure, generation failure, abstention, poison message, and transient retry cases.  
**Procedure:** Review logs/metrics/audit and Azure permissions after the tests. Confirm low-privilege identity scope, queue/DLQ access, alert behavior, and no recording of queries/prompts/doc text/answers/secrets.  
**Expected result:** Required operational signals exist without sensitive payload collection; durable audit remains separate from telemetry; access is least-privilege.  **Pass criteria:** Expected metrics/outcomes/alerts are visible, logs are content-free, and role assignments match approved access.  **Fail criteria:** Sensitive content/credentials logged, missing critical signal, audit/telemetry conflation, or excessive permission.  **Collected evidence:** Sanitized metric/event names and dimensions, alert firing results, role assignment summary, audit event types/outcomes, reviewer approval.

## Execution order and final decision

1. Complete run record, thresholds, synthetic fixtures, identity/resource isolation, and reviewer assignments.
2. Run local build and deterministic tests; resolve failures before cloud testing.
3. Execute PG-01 through PG-05. Do not proceed to broker E2E with a failing database/authorization gate.
4. Execute F-01 for the configured embedding deployment, then SB-01 through SB-05 using isolated resources. Complete the end-to-end import trace (SB-02) with Foundry embeddings.
5. Execute F-02/F-03, SEC-01/SEC-02, then Q-01 through Q-04 and SEC-03/SEC-04 using the imported synthetic corpus.
6. Review evidence, close blocking findings, approve retention/purge and rollback operations, and obtain explicit rollout approval.

Phase 12 is functionally validated for the tested happy-path corpus and retrieval operations. It is accepted for worker rollout only after the PostgreSQL, Service Bus, Blob, recovery/settlement, monitoring, and full import gates pass. Phase 13 is accepted only after database, Foundry, RAG quality, citation, security, telemetry, and operational thresholds pass. Until then, retain **implementation complete; acceptance pending** and keep the Phase 12 worker disabled outside the isolated validation environment.
