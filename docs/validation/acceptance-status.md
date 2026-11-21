# Validation Status and Evidence Gaps

## Status overview

| Phase | Repository status | Evidence recorded | Missing or incomplete evidence |
| --- | --- | --- | --- |
| 01-09 | Marked complete in `README.md` | Implementation summaries; Phase 9 linked to later RAG validation | Phase-specific acceptance transcripts and detailed test plans are not in the repository |
| 10A | Complete/validated | Entra PKCE, token validation, protected API calls | Archived raw test output is absent |
| 10B | Complete/validated | Two-user document owner-isolation workflow | Archived raw test output is absent |
| 10C | Complete/validated | User A conversation access; User B denied/404; Postman workflow | Archived raw test output is absent |
| 10D | Complete/validated | Audit events persisted with expected outcomes/targets | Query API, retention/purge, and governance reporting are out of scope |
| 11 | Complete/validated | Foundry auth, embedding/vector flow, grounded chat, scoping, and audit | Current deployment identifiers and model evaluation data are not archived |
| 12 | Implementation complete; functional validation complete; rollout acceptance pending | Successful PDF upload/processing, chunk/embedding persistence, PostgreSQL/pgvector retrieval, vector/full-text/hybrid/semantic retrieval, and the five-PDF corpus are documented | Formal fresh/upgrade migration, transaction/locking/isolation integration evidence and isolated Service Bus recovery/settlement/DLQ/operational evidence remain |
| 13 | Implementation complete; acceptance pending | Full solution build; five focused prompt/evidence/failure-telemetry tests pass; retrieval and answer telemetry implemented | Database-backed validation is gated by PG-01; live Foundry, prompt-injection, quality/performance, retention, and rollout evidence also remain |

See the [combined Phase 12-13 validation roadmap](phase12-phase13-validation-roadmap.md) for execution environments, validation order, and production-readiness assessment.

## Current execution blocker

PG-01 is **Blocked**, not failed: the enforced Windows Smart App Control policy is `VerifiedAndReputableDesktop` (`0283ac0f-fff1-49ae-ada1-8a933130cad6`). Code Integrity blocked EF design-time loading, `testhost`, `vstest`, and an independent .NET 10 Hello World application. `SmartAppControlState=On`. The failure occurs before migration execution and there is no evidence of a RagPlatform application defect. See the [PG-01 blocker assessment](blockers/pg01-ef-tooling-blocker.md).

The current workstation cannot be used to execute .NET acceptance tests reliably. A successful build is not runtime or acceptance evidence. PG-02 through PG-05 and database-dependent Phase 13 checks must not be reported as passed until PG-01 runs successfully in an approved .NET execution environment.

## Work that can continue before PG-01

- Prepare the run record, synthetic fixtures, evaluation corpus, acceptance thresholds, reviewer assignments, and evidence templates.
- Provision and review isolated test resources, identities, permissions, queue/DLQ settings, and monitoring. SB-01 infrastructure preparation can proceed, but worker execution and broker end-to-end acceptance cannot be claimed.
- Review source, configuration, and rollout/rollback procedures without treating static review as runtime validation.
- Run independent provider or infrastructure smoke checks only from an approved environment; they do not replace the application-backed acceptance tasks.

The highest-value next activity is to select an approved Windows development VM or runner that permits the required locally compiled .NET workload under its authorized policy, then execute PG-01 first. Do not change application code or weaken the current workstation policy to advance the gate.

**Phase disposition remains: implementation complete; acceptance pending.** Phase 12 rollout remains pending and the worker stays disabled outside isolated validation. Phase 13 remains acceptance-pending; neither phase should be marked accepted or production-ready.

## Phase 12 required acceptance evidence

1. Run integration tests against PostgreSQL with pgvector enabled and the real Npgsql EF model/migrations.
2. Prove valid import evidence persists chunks, model-compatible vectors, success audits, and atomic `Completed` status/audit.
3. Prove incomplete or mismatched evidence cannot complete an import, and redelivery/concurrent processing does not duplicate records/audits.
4. Run an isolated Service Bus queue through successful completion, completed redelivery, transient retry, poison-envelope DLQ, and permanent-PDF DLQ cases.
5. Preserve correlation IDs, document/message identifiers, audit records, queue/DLQ counts, and relevant telemetry as evidence.

EF Core InMemory does not map `Pgvector.Vector`; the documented test failures occur before assertions. SQLite does not provide equivalent pgvector/Npgsql validation.

## Phase 13 required acceptance evidence

- A representative question set with retrieval relevance and grounded-answer evaluation.
- Cross-user and cross-tenant tests proving unauthorized chunks never enter the prompt or citation output.
- Tests for missing evidence/abstention, invalid citation labels, and prompt-injection text in retrieved documents.
- Owner authorization and retention/deletion tests for conversation history and citations.
- Foundry failure/timeout tests and measured latency, token usage, and cost against agreed targets.
- Review retrieval/answer telemetry in Application Insights, establish p95/p99 baselines, and validate failure/abstention alerts without collecting prompt or document content.

## Evidence quality note

The README's functional-validation section records the five-PDF retrieval corpus and is retained as repository-reported evidence. It is not substituted for formal acceptance evidence. No formal CI/acceptance report, database transaction test output, queue recovery/DLQ test logs, or signed retrospective was found in the repository.
