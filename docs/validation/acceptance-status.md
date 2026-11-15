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
| 12 | Implementation complete; rollout pending | Successful builds and upload/message-publish evidence are documented | PostgreSQL/pgvector integration and isolated Service Bus E2E validation |
| 13 | In progress; increments 1-6 implemented | Full solution build; five focused prompt/evidence/failure-telemetry tests pass; retrieval and answer telemetry implemented | PostgreSQL/pgvector and live Foundry validation, live prompt-injection evaluation, quality/performance baselines, retention policy, and rollout evidence remain |

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

The README's completion markers and summaries are retained as repository-reported status. They are not upgraded here into new test results. No CI test reports, database integration test output, queue test logs, or formal retrospective files were found in the repository.