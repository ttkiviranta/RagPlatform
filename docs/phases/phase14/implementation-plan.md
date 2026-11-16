# Phase 14 Implementation Plan — Fallback Answers and Provenance

**Status:** Proposed increment plan; not an implementation authorization.  
**Related proposal:** [Document-first answers with explicit fallback provenance](proposal.md).  
**Current constraints:** Phase 12 and Phase 13 acceptance remain pending. PG-01 is blocked by Smart App Control on the current workstation, and no alternate execution environment is currently available. Do not modify workstation security settings.

## 1. Goal and implementation guardrails

Preserve document-grounded RAG as the primary source. When authorized document retrieval completes successfully but has insufficient evidence, allow only a policy-approved fallback and label the answer source. Never convert authorization, retrieval, database, or model-provider failures into fallback answers.

Implementation must remain disabled by default until the current Phase 12/13 acceptance gates pass. Each fallback increment must also remain behind a server-side disabled feature gate until Increment 4 supplies tenant policy controls.

### Contract decisions for this plan

The proposal uses both `DocumentThenWeb`/`DocumentThenModelKnowledge` wording and a `fallbackMode` enum. This plan resolves that ambiguity:

- `fallbackMode` selects the fallback to use **after the document-first path**; it does not select or bypass document retrieval.
- Values: `None`, `ModelKnowledge`, and `WebSearch`.
- Missing `fallbackMode` means `None`, preserving current behavior for existing clients.
- A non-`None` mode is effective only when the server feature gate and the caller's tenant policy both permit it.
- The first release returns one provenance source per answer. It does not merge document and fallback evidence in one answer.

### Dependency gates

Before implementation is enabled for acceptance:

1. Run PG-01 on a supported, approved .NET execution environment, using separate disposable fresh and upgrade databases.
2. Complete Phase 12 database/worker acceptance and Phase 13 database-backed retrieval, authorization, citation, and conversation acceptance.
3. Freeze and approve the Phase 13 evidence-sufficiency method and baseline; the fallback trigger cannot be tuned after observing Phase 14 outcomes.
4. Before Increment 3, select a web-search provider and approve its data handling, licensing/terms, region, and query privacy.

Design review and contract prototyping may proceed before these gates. Production-path implementation and enablement should not.

## 2. Increment summary

| Increment | Deliverable | New fallback callable? | Persistence migration |
| --- | --- | --- | --- |
| 1 | Answer source, fallback reason, and provenance model/contract | No; `None` only | Yes: core message and citation provenance fields, with safe legacy handling |
| 2 | Model-knowledge fallback | Test-only behind global disabled-by-default gate until Increment 4 | No additional schema if Increment 1 is complete |
| 3 | Web-search fallback and validated external citations | Test-only behind global disabled-by-default gate until Increment 4 | Yes: web citation metadata |
| 4 | Tenant policy, request authorization, operational controls, and telemetry | Yes, for explicitly enabled test tenants after all gates pass | Yes if tenant policy is stored in PostgreSQL; otherwise document and validate the approved configuration store |

## 3. Increment 1 — Answer source, fallback reason, and provenance model

### Architecture changes

- Add a provenance model shared by RAG response construction, citation validation, and conversation persistence.
- Define source values: `Document`, `ModelKnowledge`, `WebSearch`, and `None` (abstention).
- Define fallback reasons as a closed enum; initial value: `NoSufficientDocumentEvidence`. Do not use a fallback reason for retrieval/provider failures; those remain failures.
- For successful document answers, set source to `Document`; for current no-evidence abstention, set source to `None` and the insufficient-evidence reason.
- Preserve the existing route: document-only behavior remains the only enabled route in this increment.
- Ensure historical conversation records are not falsely labeled. Records whose source cannot be established remain nullable/legacy-unknown; existing citation snapshots may be labeled `Document` only where their stored document/chunk linkage proves that provenance.

### API changes

- Add response fields to `POST /api/rag/query`:
  - `answerSource`
  - nullable `fallbackReason`
- Add a `sourceType` discriminator to citations while retaining existing document citation fields.
- Add optional request field `fallbackMode`, accepting `None`, `ModelKnowledge`, or `WebSearch`. For Increment 1, only `None` is enabled; unsupported or disabled values fail explicitly and do not alter response behavior.
- Keep fields additive for clients that ignore unknown response properties. Publish enum values and nullability in OpenAPI.

### Acceptance criteria

- Existing document-grounded query behavior and citation validation are unchanged.
- A query with sufficient document evidence returns `answerSource=Document`, valid document citations, and no fallback reason.
- A successful no-evidence/abstention response returns `answerSource=None` and the stable insufficient-evidence reason.
- Authentication, authorization, database, retrieval, and provider failures remain errors and are not represented as `None` or as a successful fallback.
- Legacy conversation data is not misrepresented as model- or web-grounded; existing clients continue to function.
- API contract tests cover omitted `fallbackMode`, `None`, invalid enum values, and disabled fallback modes.

### Migration requirements

- Add nullable source and fallback-reason fields to persisted assistant-message/provenance records as required by the existing model.
- Add a citation source discriminator. Backfill existing citation rows to `Document` only where document/chunk foreign keys prove the source; leave ambiguous historical message provenance unknown/null.
- Add database constraints for allowed values and valid null combinations after data backfill.
- Verify fresh and upgrade migrations on PostgreSQL/pgvector with the real EF provider. Do not mark the increment accepted on InMemory/SQLite-only results.

## 4. Increment 2 — Model-knowledge fallback

### Architecture changes

- Add a model-knowledge answer path using the existing Foundry chat abstraction; do not pass retrieved document text as supporting evidence when the route is `ModelKnowledge`.
- Invoke it only when document retrieval completed successfully and was classified insufficient, and only when `fallbackMode=ModelKnowledge` is allowed by the temporary global test gate.
- Return an explicit unverified/model-knowledge notice. Do not emit document citations or model-invented citations.
- Do not fall back to model knowledge after retrieval, authorization, database, citation-validation, or Foundry service errors.
- Keep the gate disabled by default. Until Increment 4, enable only in an isolated test configuration; do not expose it as an unrestricted production option.

### API changes

- `POST /api/rag/query` accepts `fallbackMode=ModelKnowledge`; omitted or `None` continues document-only behavior.
- Successful fallback returns `answerSource=ModelKnowledge`, `fallbackReason=NoSufficientDocumentEvidence`, an explicit notice field or stable notice code, and an empty citations collection.
- Preserve the existing error contract for Foundry failures; do not return a success-shaped answer.

### Acceptance criteria

- Insufficient document evidence with the test gate enabled invokes the configured chat deployment once and labels the result `ModelKnowledge`.
- Sufficient document evidence does not invoke model-knowledge fallback even if requested.
- The fallback prompt does not imply the corpus supports the answer and contains no private document excerpts, unrelated conversation history, tenant identifiers, or unauthorized content.
- Responses contain the agreed caveat and no citations or source metadata invented by the model.
- Authentication/authorization failures, retrieval failures, timeouts, throttling, and Foundry errors do not trigger another answer path.
- Deterministic tests verify routing and failure behavior; live non-production Foundry tests measure answer behavior, abstention, latency, and token usage against pre-approved thresholds.

### Migration requirements

- No new migration is expected if Increment 1's message provenance fields cover `ModelKnowledge` and `fallbackReason`.
- Confirm message persistence stores the selected source and notice/fallback outcome atomically with the assistant turn.
- If token usage or deployment metadata is not already stored, add it only if required by the approved telemetry/data model; do not store prompt or answer text in telemetry.

## 5. Increment 3 — Web-search fallback

### Architecture changes

- Add an `IWebSearchProvider` boundary and one approved provider implementation after provider, legal/terms, privacy, and region review.
- Perform search server-side only. Submit a minimized user query; never submit retrieved document text, tenant/user identifiers, or conversation history.
- Limit result count, content size, content types, redirects, request time, and provider calls. Do not implement arbitrary URL fetching or unrestricted model-directed browsing.
- Treat search results and page content as untrusted input. Keep them separate from system/developer instructions.
- Construct citation metadata server-side from provider results actually supplied to the model. Reject fabricated URLs, unsupported labels, and citations to results omitted by context limits.
- Keep the feature behind a global disabled-by-default test gate until Increment 4.

### API changes

- `POST /api/rag/query` accepts `fallbackMode=WebSearch` only when the test gate is enabled.
- Successful responses use `answerSource=WebSearch`, `fallbackReason=NoSufficientDocumentEvidence`, and citations discriminated as `sourceType=Web`.
- Web citation fields include provider result ID, title, canonical URL, publisher when supplied, and retrieval time. Do not return or persist arbitrary provider/model-generated URLs.
- Provider errors remain explicit failures; they do not silently invoke model-knowledge fallback.

### Acceptance criteria

- Web search is called only after successful authorized document retrieval is insufficient and the selected mode is permitted.
- Sufficient document evidence avoids a web call; document-only mode never sends a query externally.
- Every returned web citation maps to a result included in the answer context; fabricated or omitted-result labels are rejected.
- Web queries contain no private corpus content or unnecessary user/tenant/history data.
- SSRF-style destinations, unsafe redirects, oversized or unsupported responses, timeouts, provider throttling, and malformed results are safely rejected/classified.
- Synthetic prompt-injection content in search results cannot override system policy, disclose secrets/other tenants, or forge citation metadata; test failures remain visible.
- A frozen web-answer evaluation checks factual support, citation quality, freshness metadata, abstention, latency, availability, and cost.

### Migration requirements

- Extend citation snapshots with nullable web-specific fields: provider, provider result ID, canonical URL, publisher, and retrieved-at timestamp. Add published-at only if the selected provider supplies and the product needs it.
- Preserve document citation fields and constraints; use source-type-specific validation so a web citation cannot masquerade as a document/chunk citation.
- Do not store full web pages by default. If excerpts are required for conversation rendering, approve their data/retention policy and impose a strict length limit.
- Add indexes only for demonstrated query patterns; verify fresh/upgrade migration paths and conversation deletion/retention behavior on PostgreSQL.

## 6. Increment 4 — Policy controls, tenant settings, and telemetry

### Architecture changes

- Replace temporary test gates with one policy service that evaluates server capability, tenant allowance, and requested `fallbackMode`; the most restrictive setting wins.
- Default all tenant fallback permissions to off. A request can select an allowed mode but cannot enable a mode or change tenant policy.
- Provide a server-side global kill switch for all fallback and independent switches/quotas for model knowledge and web search.
- Use an approved operator/admin configuration path for tenant settings. Do not add a public policy-management endpoint without defining and enforcing a tenant-administrator policy; if self-service administration is required, treat it as a separately scoped administration dependency.
- Emit content-free metrics/events for source mode, fallback reason, outcome, duration, result count, token/search usage, and error class. Do not log questions, prompts, retrieved excerpts, answers, tokens, or credentials.
- Define per-tenant rate/cost limits, timeouts, provider circuit breaking, and alert thresholds.

### API changes

- Enforce `fallbackMode` as a request to use an already-authorized mode, not a policy mutation.
- Return an explicit forbidden/disabled response when a caller requests a mode not permitted by tenant/server policy; do not disclose internal policy details.
- Add any policy administration API only if approved, tenant-scoped, audited, and protected by a dedicated administrator authorization policy. Otherwise keep settings in the existing approved configuration process.
- Document all modes, outcomes, error responses, notices, and citations in OpenAPI. Existing clients omitting the field remain document-only.

### Acceptance criteria

- Missing request mode and default tenant policy result in document-only behavior.
- A tenant cannot request a mode it is not allowed to use; another tenant's configuration cannot affect the request.
- Global/per-mode kill switches immediately prevent fallback and leave document-only behavior available.
- Configuration changes have an authorized, auditable owner and take effect as documented.
- Metrics and alerts distinguish document answers, model-knowledge answers, web answers, abstentions, and failures without sensitive content.
- Rate limits, timeouts, cost controls, and circuit-breaker behavior are verified for both providers.
- Cross-tenant/owner tests prove no unauthorized documents reach any model or search provider, response, persisted provenance, or telemetry.
- Compatibility, security, privacy, evaluation, migration, deletion, and rollback criteria pass in an isolated non-production environment.

### Migration requirements

- Prefer an existing authoritative tenant-settings store if one is present and approved. If not, add a `TenantRagPolicy` table keyed by tenant ID with explicit model-knowledge and web-search permissions, both defaulting to false; do not use permissive defaults for existing tenants.
- Record policy-update audit data without storing user prompts or secrets. If the project has no approved admin/audit write path for policy changes, do not create an unauthenticated setting API; provision settings through the approved operator configuration process until administration is designed.
- Add an upgrade migration that preserves existing tenants with both fallbacks disabled, plus a safe downgrade/rollback plan that does not delete conversation data.
- Validate against PostgreSQL/pgvector with fresh and prior-schema databases after PG-01 is executable in an approved environment.

## 7. Cross-increment release gates

- Keep the existing document-only path as the release default until all increments and Phase 12/13 acceptance gates pass.
- Use a frozen synthetic evaluation set covering sufficient evidence, no evidence, weak/ambiguous evidence, unauthorized scope, retrieval failures, and provider failures.
- Approve thresholds before running tests; measure false fallback, missed fallback, answer support, citation correctness, abstention, latency, cost, and privacy.
- Test all database migrations on the real PostgreSQL/pgvector provider, including conversation citation persistence, deletion, retention, and upgrade paths.
- Run live provider and end-to-end tests only with non-production Foundry/search resources and isolated data.
- Enable a restricted test tenant only after security and operations approval; expand only after monitored results meet the approved thresholds.

## 8. Delivery decision

This plan can guide design and API review now, but implementation and acceptance depend on resolving the execution bottleneck without changing the current workstation's security settings. The next prerequisite is an approved .NET execution environment, followed by PG-01 and the documented Phase 12/13 acceptance sequence. No increment should be represented as production-ready before those dependencies and its own acceptance criteria pass.
