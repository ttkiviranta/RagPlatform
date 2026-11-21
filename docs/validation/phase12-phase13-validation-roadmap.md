# Phase 12 and Phase 13 Combined Validation Roadmap

## Executive status

Both phases are **implementation complete**. Phase 12 has successful functional import/retrieval evidence, but neither phase has sufficient recorded formal acceptance evidence for production rollout. Phase 12's document-import worker must remain disabled until PostgreSQL/pgvector and isolated Service Bus validation passes. Phase 13 requires database, live Foundry, authorization, answer-quality, security, and operational evaluation before broad release.

The repository records a successful full solution build and five passing focused Phase 13 hardening tests. Functional retrieval validation covers vector, full-text, and hybrid RRF search over the five-PDF corpus. Phase 12 implementation builds and a PDF upload/message-publish validation are recorded. Those checks do not prove database-backed processing, live message settlement, answer quality, or production readiness. PG-01 is currently blocked by the workstation's enforced Windows Smart App Control `VerifiedAndReputableDesktop` policy (`0283ac0f-fff1-49ae-ada1-8a933130cad6`), which blocks EF design-time execution and other locally generated .NET applications. No RagPlatform defect has been identified; use an approved .NET execution environment to resume runtime validation.

## 1. Remaining Phase 12 validation

1. **Local regression gate:** Build the release revision; run the Phase 12 import/settlement/registration tests and relevant suite. Record discovered and executed test counts and failures. Confirm the worker remains disabled by default.
2. **PostgreSQL/pgvector integration:** Apply fresh and upgrade migrations using Npgsql and pgvector. Verify extracted content, chunk and embedding persistence, configured model/dimension compatibility, success audits, transaction atomicity, locked completion, and status endpoint behavior.
3. **Concurrency and recovery:** Exercise row locks, uniqueness constraints, duplicate/concurrent messages, redelivery after completion, and interruption between database completion and broker settlement. Verify no duplicate artifacts or false completion.
4. **Isolated Service Bus behavior:** Prove successful settlement, completed-message redelivery, transient retry through lock expiry/delivery limits, malformed-envelope DLQ, permanent-PDF failure handling, and settlement interruption/redelivery.
5. **Blob and operational behavior:** Verify staged-PDF lifecycle, cleanup after successful extraction, failed/abandoned-Blob handling, least-privilege identity, queue/DLQ monitoring, alerting, and rollback/disable procedure.
6. **Full import acceptance trace:** Run upload → queued document and Blob staging → publish → processing → extraction → chunks → embeddings → `Completed` plus audit → message completion → status query. Preserve sanitized correlated evidence.

## 2. Remaining Phase 13 validation

1. **Database-backed retrieval and conversations:** Verify pgvector ranking, Top-K/score behavior, compatible model/dimensions, tenant/document authorization within the query, optional document scope, conversation ownership, atomic turn/citation writes, deletion cascade, and isolation on PostgreSQL.
2. **Live Azure AI Foundry:** Exercise configured embeddings/chat deployment and settings, successful grounded answer, explicit empty/insufficient-evidence abstention, timeout/failure handling, returned token usage, and no persistence on failed generation or citation validation.
3. **Citation correctness:** Verify labels only refer to supplied prompt context and metadata comes from authorized persisted records; test malformed/unsupported/omitted labels. Evaluate semantic support separately because syntactic label validation does not prove entailment.
4. **Authorization and security:** Test cross-user/cross-tenant document and conversation access; prove unauthorized data never reaches model context, response, citations, or persisted conversation. Run synthetic live prompt-injection evaluations; deterministic prompt tests alone do not prove model resistance.
5. **Quality, performance, and cost:** Run a versioned question/evidence gold set for retrieval relevance, answer correctness/grounding, abstention, and citation support. Measure p50/p95/p99 latency, failure/abstention rates, token usage, and cost against targets approved before the run.
6. **Telemetry and operations:** Review Application Insights metrics/events and logs, verify failure and abstention alerting, establish baselines, and confirm sensitive prompt/document/answer content is excluded.
7. **Retention and rollout:** Approve a tenant/legal conversation retention period and purge ownership/process; test deletion/purge scope. Exercise a controlled feature-flagged or restricted-cohort rollout and disable/rollback procedure.

## 3. Where each validation can run

| Validation | Local only | PostgreSQL + pgvector | Azure AI Foundry | Service Bus / other Azure |
|---|---|---|---|---|
| Solution build, deterministic service tests, prompt framing, fallback and telemetry classification | Yes, on a compatible approved .NET environment; this workstation blocks locally generated .NET execution | Not required | Fakes suffice for deterministic paths | Not required |
| Phase 12 parsing/chunking/settlement-policy unit tests with fakes | Yes, on a compatible approved .NET environment; this workstation blocks testhost/vstest | Not required for unit-level behavior | Not required | Fakes do not validate broker settlement |
| Applying migrations, pgvector SQL translation/ranking, vector dimensions, transaction/locking/idempotency | No; local Docker can host the dependency | **Required** | No | No |
| Database-enforced tenant/document retrieval and conversation isolation, atomic citation persistence/cascade | No; local Docker can host the dependency | **Required** | No | No |
| Live embedding generation used in the ingestion pipeline and live chat/answer behavior | Fakes can test orchestration only | Usually also needed for persistence/E2E | **Required for live provider validation** | No, except complete import E2E also needs Service Bus |
| True queue locks, settlement, delivery count, redelivery, retry and dead-letter behavior | No; local fakes are not equivalent | A disposable DB is needed for processor E2E | Foundry needed for embedding-enabled full import | **Isolated Service Bus required** |
| Actual PDF upload/staging and staging-Blob deletion in the deployed storage integration | Unit fakes can test logic | No | No | **Azure Blob Storage required** |
| Full Phase 12 upload-to-message-completion trace | No | **Required** | **Required for configured embedding step** | **Blob Storage and isolated Service Bus required** |
| Phase 13 end-to-end Q&A over imported corpus | No | **Required** | **Required** | Not intrinsically required if documents are already imported; required only when validating the combined upload-to-answer journey |

The repository's `docker-compose.yml` provides a local PostgreSQL 16/pgvector service, so database integration can use that dependency when run from a compatible approved .NET environment. The current workstation's App Control policy blocks the application/test hosts, so Docker availability alone does not make tests executable there. Database tests require a real PostgreSQL/pgvector instance. No local Service Bus or Blob emulator is documented as part of this repository, so use isolated Azure test resources for those integration checks. Live-model tests require a configured non-production Foundry deployment and credentials/identity with least privilege. Resource provisioning, test-data preparation, and acceptance-threshold definition can proceed independently, but are not passes for the blocked runtime gates.

## 4. Recommended execution order

### Gate 0 — Freeze and prepare

- Select a release commit and record application version, migrations, embedding model/deployment and dimensions, Foundry deployment/settings, queue settings, and test-data version.
- Prepare synthetic PDFs/documents, a multi-user/multi-tenant authorization matrix, correlation IDs, disposable resources, and pre-approved quality/performance/cost thresholds.
- Confirm worker is disabled outside the isolated test environment; agree on an owner and evidence location for each gate.

### Gate 1 — Local deterministic checks

- Build the full solution and execute relevant local tests, including the five focused `RagQualityHardeningTests` and Phase 12 unit tests.
- Verify exact discovered/passed counts; do not treat successful compilation or tests using fakes as external integration evidence.
- Review configuration defaults, structured logging, telemetry dimensions, and rollback/disable instructions.

### Gate 2 — PostgreSQL/pgvector

- Start the repository's PostgreSQL/pgvector container or another disposable supported instance.
- Apply fresh and upgrade migrations; run Phase 12 persistence/locking/idempotency tests and Phase 13 retrieval/conversation isolation tests.
- Resolve database, authorization, and transaction issues before involving broker retries or live model variability.

### Gate 3 — Isolated Azure infrastructure

- Provision isolated Blob Storage and Service Bus resources, dedicated identity, queue/DLQ access, monitoring, and cleanup policy. Keep worker enabled only in that isolated environment.
- Validate Service Bus settlement and retry behavior with controlled test messages and the PostgreSQL test database. Confirm staging-Blob lifecycle behavior.

### Gate 4 — Phase 12 end-to-end

- With a non-production Foundry embedding deployment, execute the complete PDF import path through broker completion and import-status verification.
- Repeat redelivery, transient, poison-envelope, permanent-PDF, and database-to-settlement interruption cases. Review correlated DB, audit, queue, DLQ, and telemetry evidence.
- Keep the worker disabled in shared/production environments unless every Phase 12 acceptance criterion passes and the rollout owner approves.

### Gate 5 — Phase 13 functional and security validation

- Use the imported test corpus and execute retrieval/citation/conversation tests, including multi-user and cross-tenant cases.
- Validate the live Foundry chat path for success, abstention, timeout/failure, citation rejection, token usage, and safe persistence behavior.
- Run prompt-injection and output/citation checks with synthetic malicious content; any unauthorized disclosure is a stop condition.

### Gate 6 — Quality, operations, and controlled rollout

- Run the frozen gold-set evaluation and compare metrics against thresholds set in Gate 0. Establish latency, token, and cost baselines; inspect telemetry privacy and alerts.
- Approve retention and purge operations, capacity/monitoring, support ownership, and rollback/disable procedures.
- Start with a restricted cohort or feature gate, observe for an agreed period, and expand only if error, quality, latency, and cost gates remain within targets.

If a gate fails, stop progression, retain sanitized evidence, fix the root cause, and repeat the failed gate plus relevant regression checks. Do not weaken authorization, citation checks, or completion evidence to obtain a pass.

## 5. Production rollout readiness estimate

**Current recommendation: NO-GO for production rollout; suitable only for controlled development/validation.**

- **Phase 12 worker:** Functional PDF, PostgreSQL/pgvector, embedding, and retrieval validation is complete, but shared/production enablement remains not ready until formal transaction/isolation, broker settlement/retry/DLQ, monitoring, and rollback evidence is reviewed; the worker is intentionally disabled by default.
- **Phase 13 Q&A:** Vector, full-text, hybrid RRF, and semantic retrieval are functionally validated. Database authorization/persistence behavior, live Foundry chat behavior, semantic answer/citation quality, live prompt-injection resilience, performance/cost baselines, and retention operations remain unvalidated.
- **Combined solution:** End-to-end production readiness is blocked by both phases' runtime evidence and operational gates. Do not infer production readiness from successful builds, unit tests, or upload/message-publish evidence alone.

Readiness can be reassessed only after all applicable gates pass, evidence is reviewed, residual risks have named owners and approval, and a controlled rollout demonstrates stable operation.
