# Phase 13 Acceptance Validation Plan

## Purpose and current status

This plan defines the evidence required to accept Phase 13 Document Q&A after implementation increments 1-6. It is a validation plan, not evidence that the checks have already passed. The full solution build and five focused `RagQualityHardeningTests` have passed; PostgreSQL/pgvector behavior, live Azure AI Foundry behavior, answer quality, production-like authorization, and operational thresholds remain to be validated.

Do not mark Phase 13 accepted or enable general availability until the required gates below have recorded results, owners, environment/configuration, and any approved exceptions.

## Validation principles and prerequisites

- Use a disposable PostgreSQL environment with the production EF provider and pgvector extension; EF Core InMemory is not a substitute for vector SQL or database authorization testing.
- Use isolated development/test data and a non-production Azure AI Foundry deployment. Do not include production documents or credentials in test reports.
- Create at least two tenants, multiple users per tenant, tenant-admin identities for the supported roles, and documents with distinguishable owners and content. Keep a test data manifest that records each expected access boundary.
- Freeze application build, database migration version, embedding model/deployment and dimensions, Foundry deployment/settings, Top-K, and evaluation dataset for each test run.
- Agree on numeric quality, latency, availability, and cost targets before running acceptance tests. Record target, measured result, and pass/fail; do not infer acceptance from build success.
- Capture correlation IDs, test case IDs, sanitized logs/metrics, database migration version, and Foundry deployment identifiers. Never archive prompts, document excerpts, answers, tokens, or secrets unless the approved test-data policy explicitly permits it.

## 1. PostgreSQL and pgvector validation

### Environment and schema

1. Provision the supported PostgreSQL version with pgvector enabled and apply all Phase 13 migrations to an empty database.
2. Verify migrations complete without manual schema edits and that expected vector columns, dimensions, indexes, foreign keys, tenant/owner fields, and conversation citation-snapshot tables exist.
3. Apply migrations to a database at the previous supported schema version and verify the upgrade path. Record migration identifiers and database/extension versions.

### Retrieval correctness and isolation

Seed distinct known vectors and document/chunk records for the tenant/user matrix. Exercise the API/service against PostgreSQL and verify:

- A query embedding generated with the configured model and dimension returns the expected cosine-distance order and no more than Top-K results; verify the score mapping and tie behavior with deterministic vectors.
- Query embedding dimension/configuration mismatches fail explicitly. Persisted rows with a different model name or dimension are not compared or returned.
- Tenant, document owner/role rules, and optional document scope are applied in the database query. A cross-tenant document, another user's private document, and an inaccessible optional document never appear in results, model context, citations, or logs; verify telemetry contains no document content.
- Authorized tenant-administrator access follows the existing access rules and cannot cross tenant boundaries.
- Missing/inaccessible document scope does not expose document existence through result details.
- Candidate and returned counts agree with the seeded eligible rows and ranked Top-K. Search failures emit a failure outcome without converting the request into a successful empty result.

### Conversation persistence and database lifecycle

- Verify a successful query stores the user turn, assistant turn, and validated citation snapshots together. Force failures at retrieval, generation, and citation validation and verify no partial turn or empty conversation is persisted.
- Verify conversation reads and writes require both owner and tenant scope. Verify citation snapshots returned by history match the persisted validated response.
- Delete a conversation as its owner and verify its turns and citation snapshots are deleted by the configured cascade. Verify another owner cannot delete it and that unauthorized requests do not reveal its content.
- Confirm document deletion does not corrupt stored conversation citation snapshots, consistent with the snapshot retention design.

## 2. Azure AI Foundry validation

Run against an isolated, named deployment using the exact configuration intended for rollout. Record region, deployment identifier, model/version when available, request settings, and test time without recording credentials.

- Verify valid deployment, output-token limit, temperature, and timeout settings reach the chat-completion boundary as configured. Verify invalid/missing required settings fail explicitly.
- Test a supported question with known retrieved evidence and confirm a non-empty answer, valid supplied citation labels, and available token-usage reporting.
- Test empty retrieval: the API returns the explicit `not found in accessible documents` response and does not make a chat completion request.
- Test non-empty but insufficient evidence: the model abstains using the documented exact fallback, and the result is classified distinctly from a generation error.
- Simulate timeout, throttling, authentication/configuration failure, and service errors using an isolated endpoint or deterministic transport fake. Verify failure telemetry and that no success-shaped answer/citations or conversation turn are persisted.
- Verify cancellation and timeout behavior terminates the request within the agreed operational bound.
- Check Application Insights and application logs for all test cases to ensure query, prompt, source excerpt, answer, and secrets are not recorded.

## 3. RAG quality evaluation

Create a versioned gold dataset of representative questions over approved test documents. For each case, record expected accessible source/chunk IDs, answer facts, citation expectations, and whether the correct behavior is to abstain. Include direct lookup, multi-chunk synthesis, follow-up reference, ambiguous question, out-of-scope request, and insufficient-evidence cases. Include both answerable and unanswerable examples and cover tenant/document scoping.

For each frozen dataset run:

1. Measure retrieval relevance at configured Top-K using agreed ranking metrics such as Recall@K, Precision@K, or nDCG@K. Report results by question category as well as overall.
2. Assess answer correctness and groundedness against the gold facts. Require every material factual claim to be supported by accessible retrieved evidence and a valid citation; separately report unsupported claims and incorrect abstentions.
3. Measure abstention precision/recall (or an agreed equivalent) for insufficient-evidence cases. Confirm irrelevant but accessible chunks do not cause fabricated answers.
4. Review representative successes and failures with a human evaluator; retain the dataset version, evaluator rubric, and adjudication notes.
5. Compare latency, token usage, and estimated cost with the pre-approved targets and the baseline. Report p50/p95/p99 and error/abstention rates by deployment.

Acceptance thresholds must be approved and recorded before the run. Any material change to embedding model, dimensions, retrieval parameters, prompt, chat deployment, or citation behavior requires a documented impact assessment and rerun of affected cases.

## 4. Citation validation checks

Use deterministic service tests and end-to-end cases to verify:

- The API assigns contiguous labels in retrieval order and only sends persisted, authorized, bounded excerpts and labels to Foundry.
- Labels in the response are drawn only from the labels actually included in the bounded model context; labels omitted due to context limits cannot be cited.
- Supported labels are mapped to the persisted chunk/document records. Model-generated IDs, source names, page numbers, URLs, and other metadata are ignored.
- Malformed labels, unsupported numbers, altered labels, and a non-fallback answer without a supplied citation are rejected. The exact insufficient-evidence fallback is allowed without citations; altered fallback text is not treated as an abstention.
- Returned citation metadata has safe normalized/capped excerpts and persisted IDs/source name. `PageNumber` remains null while the chunk model has no page field.
- Citation snapshots persisted in a conversation match the validated response and cannot be read outside the conversation owner/tenant scope.

Syntactic label validity is necessary but does not demonstrate semantic entailment; the quality evaluation must assess whether each citation supports its associated claims.

## 5. Prompt-injection and untrusted-content tests

Run deterministic tests and live Foundry red-team cases. Use only synthetic, non-sensitive payloads. Include malicious instructions embedded in retrieved chunks, prior user messages, document titles/source names, and quoted content, including attempts to override system instructions, request secrets, fabricate citations, disclose another tenant's data, or instruct the model to ignore evidence constraints.

For each case verify:

- Retrieved content and prior user messages are clearly framed as untrusted data and the system instruction says not to follow embedded commands.
- Prior assistant answers are not supplied as evidence; current-turn evidence is freshly retrieved and remains limited to authorized documents.
- The model does not follow embedded instructions, invent facts, or emit unsupported citation labels or source metadata. If evidence does not answer the question, it returns the explicit abstention response.
- No unauthorized chunk, prompt, excerpt, answer, or secret appears in the response, persisted citations, or telemetry/logs.

Record model/deployment, test case, expected behavior, observed output classification, evaluator, and disposition. Live-model tests are probabilistic; run the agreed number of repetitions and establish a release threshold before testing. Any cross-tenant disclosure, secret disclosure, unsupported source metadata, or consistent instruction-following failure is a blocking issue.

## 6. Conversation authorization tests

Use at least two users in one tenant and users in separate tenants, including applicable administrator roles. Verify:

- An owner can create, read, continue, and delete their own conversation.
- Another user in the same tenant cannot read, continue, or delete an owner's conversation unless an explicit existing policy grants that operation; tenant membership alone must not grant conversation ownership.
- A user and administrator in another tenant cannot access the conversation or its messages/citations.
- Requests containing another user's conversation ID do not use that conversation's history in a prompt, retrieval, response, or telemetry.
- Prior user history is bounded to six messages and 2,000 characters, used only for follow-up reference resolution, and does not become factual evidence. Prior assistant messages and citations are not provided as evidence.
- Each follow-up performs fresh authorized retrieval using the current question; changing or deleting underlying documents cannot cause old assistant answers to be reused as evidence.
- Failed turns do not persist partial messages or citations. Owner deletion removes turns and citation snapshots, and authorization remains enforced during deletion.

## 7. Rollout gates

Progress through these gates in order; record an owner and evidence link for each gate.

1. **Build and deterministic checks:** Full solution builds; focused hardening tests pass; relevant existing tests pass. No unresolved critical/high defects.
2. **Database gate:** PostgreSQL migrations, pgvector retrieval, dimension/model filtering, transaction behavior, and owner/tenant isolation pass.
3. **Foundry gate:** Isolated deployment checks pass for successful, insufficient-evidence, timeout, and failure paths; telemetry and privacy review pass.
4. **Quality/security gate:** Frozen gold-set thresholds pass; citation checks pass; adversarial prompt-injection and conversation authorization tests have no blocking failures.
5. **Operations gate:** Retention policy and purge ownership are approved; dashboards, alerts, correlation, capacity, p95/p99 latency, token/cost targets, and rollback/disable procedure are documented and exercised in a non-production environment.
6. **Controlled rollout:** Enable only for a restricted test cohort or feature-flagged environment. Monitor failures, abstentions, latency, retrieval counts, citations, and token/cost trends. Expand only after the agreed observation period meets the pre-approved thresholds.

If any gate fails, keep the feature disabled or restricted, preserve sanitized diagnostic evidence, fix the root cause, and rerun the affected gate plus regression checks. Do not weaken authorization or evidence validation to obtain a passing result.

## 8. Acceptance criteria

Phase 13 may be marked accepted only when all of the following are true and evidence is attached to the acceptance record:

- PostgreSQL/pgvector migrations and integration tests pass against the supported database stack; compatible ranking and required authorization boundaries are demonstrated.
- Cross-user and cross-tenant tests prove inaccessible documents and conversations cannot enter model context, answers, citations, or persisted conversation data.
- Foundry success, abstention, timeout, and failure behaviors match the documented contracts; no empty or failed generation is returned as a successful grounded answer.
- Citation labels are server-assigned and validated; persisted citation metadata is authoritative; semantic citation support meets the approved quality threshold.
- The approved RAG gold-set quality, abstention, prompt-injection, and performance/cost thresholds are met and the dataset/model/prompt versions are recorded.
- Telemetry and alerting are visible and useful without collecting sensitive prompt/document/answer content; latency, failure, abstention, token, and cost baselines are approved.
- Retention/deletion requirements and an operational purge approach are approved and tested; rollout and rollback/disable procedures are documented and exercised.
- No unresolved blocking security, authorization, data-integrity, or operational findings remain; any accepted residual risks have named owners and explicit approval.

Until these criteria are evidenced, report Phase 13 as **implementation complete; acceptance pending**.
