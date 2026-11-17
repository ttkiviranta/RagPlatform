# Phase 14 Implementation Plan — Adaptive Retrieval and Fallback Provenance

**Status:** Proposed increment plan; not an implementation authorization.  
**Related proposal:** [Adaptive Retrieval and Fallback Strategy](proposal.md).  
**Current constraints:** Phase 12 and Phase 13 acceptance remain pending. PG-01 is blocked by Smart App Control on the current workstation, and no alternate execution environment is currently available. Do not modify workstation security settings.

## 1. Goal and implementation guardrails

Preserve corpus-wide vector RAG as the default. For one explicitly selected, authorized small file, optionally use its complete extracted text directly. When the selected authorized retrieval path completes successfully but has insufficient evidence, allow only the requested, policy-approved fallback and label the answer source. Never convert authorization, retrieval, database, or model-provider failures into fallback answers.

Implementation must remain disabled by default until the current Phase 12/13 acceptance gates pass. Each fallback increment must also remain behind a server-side disabled feature gate until Increment 4 supplies tenant policy controls.

### Contract decisions for this plan

- `documentId` optionally selects exactly one existing document the caller is authorized to access. It requests File First only when the document meets the eligibility limits below; otherwise retrieval is constrained to that document. Without `documentId`, retain corpus-wide vector RAG, including for small documents.
- `fallbackMode` selects a fallback **after** successful retrieval is classified as insufficient; it never bypasses retrieval or changes document scope. Values are `None`, `ModelKnowledge`, and `WebSearch`.
- `fallbackMode` is the canonical API field. `DocumentOnly`, `DocumentThenModelKnowledge`, and `DocumentThenWeb` are descriptive policy names only; they are not request values.
- Omitted `fallbackMode` is `None`. `None` preserves abstention when evidence is insufficient. A requested mode is effective only when implemented, enabled by the server, and permitted by tenant policy; the request cannot grant itself permission.
- Invalid enum values return `400` with a documented validation error. A valid but unavailable or disallowed mode returns a generic `403 FallbackModeNotAllowed` response without disclosing internal policy. Neither case silently selects another mode.
- The first implementation increment adds the `File` provenance contract, but no fallback is callable. The first fallback implementation is ModelKnowledge; WebSearch is a later increment.
- Each successful answer has one primary provenance source: `File`, `Document`, `ModelKnowledge`, `WebSearch`, or `None` for abstention. Do not mix sources in one answer. `None` is a successful abstention, not a system-error value; errors remain errors.
- Citation `sourceType` values are `File`, `Document`, and `Web`. File citations identify the selected document version/content hash without claiming a chunk ID; Document citations retain chunk/document linkage; Web citations map to server-approved provider results.

### Dependency gates

Before implementation is enabled for acceptance:

1. Run PG-01 on a supported, approved .NET execution environment, using separate disposable fresh and upgrade databases.
2. Complete Phase 12 database/worker acceptance and Phase 13 database-backed retrieval, authorization, citation, and conversation acceptance.
3. Freeze and approve evidence-sufficiency methods and baselines for both File First and Phase 13 vector retrieval; fallback triggers cannot be tuned after observing Phase 14 outcomes.
4. Before Increment 3, select a web-search provider and approve its data handling, licensing/terms, region, and query privacy. Phase 14 uses provider-supplied search results only: page fetching, crawling, and arbitrary URL retrieval are explicitly out of scope.

Design review and contract prototyping may proceed before these gates. Production-path implementation and enablement should not.

## 2. Increment summary

| Increment | Deliverable | New fallback callable? | Persistence migration |
| --- | --- | --- | --- |
| 1 | Provenance contract and explicitly scoped File First routing | No; `None` only | Yes: message source/reason and citation source metadata, with safe legacy handling |
| 2 | ModelKnowledge fallback (first fallback implementation) | Test-only behind a global disabled-by-default gate until Increment 4 | No additional schema if Increment 1 is complete |
| 3 | WebSearch fallback and validated external citations | Test-only behind a global disabled-by-default gate until Increment 4 | Yes: bounded web-citation metadata |
| 4 | Tenant policy, request authorization, operational controls, and telemetry | Yes, for explicitly enabled test tenants after all gates pass | Yes: PostgreSQL policy table by default, unless an approved authoritative tenant-policy store is identified before implementation |

## 3. Increment 1 — Provenance contract and File First routing

### Architecture changes

- Add one provenance model shared by File First, vector RAG, fallback response construction, citation validation, and conversation persistence.
- Define answer source values: `File`, `Document`, `ModelKnowledge`, `WebSearch`, and `None` (abstention). Define citation `sourceType` values as `File`, `Document`, and `Web`.
- Define fallback reasons as a closed enum; initial value: `NoSufficientDocumentEvidence`. Do not use a fallback reason for retrieval/provider failures; those remain failures.
- For a sufficient File First answer, set source to `File`; for sufficient vector retrieval, set source to `Document`; for no-evidence abstention, set source to `None` and the insufficient-evidence reason. Failures are not represented as `None`.
- File First is available only when the caller explicitly supplies one authorized `documentId`. Authorize before loading canonical extracted text and use the complete text as context without chunk/vector retrieval for that request. Do not infer the selected file from an ambiguous title or reveal whether an inaccessible document exists.
- File First eligibility requires both limits: canonical extracted text is at most **4,000 model-tokenizer tokens** and **24 KiB UTF-8**; whichever limit is reached first excludes the file. Count normalized extracted text, not uploaded binary size.
- The complete File First prompt (file text, system instructions, question, and reserved answer budget) must fit within **8,000 tokens** using the configured deployment's tokenizer. If tokenizer counting is unavailable or the full file does not fit, do not truncate; use the document-scoped vector route instead.
- For an explicitly selected file over either File First limit, run vector retrieval constrained to that document. Never widen the scope to the corpus. If scoped vector retrieval cannot be supported, return the established explicit validation response rather than sending an oversized file to the model.
- Without `documentId`, preserve corpus-wide vector retrieval and existing chunk/vector indexing, including for small documents. Do not skip chunking/embedding at ingestion in this increment.
- Do not enable either fallback in this increment. File/Document-grounded retrieval followed by abstention remains the default.
- Ensure historical conversation records are not falsely labeled. Records whose source cannot be established remain nullable/legacy-unknown; existing citation snapshots may be labeled `Document` only where their stored document/chunk linkage proves that provenance.

### API changes

- Add response fields to `POST /api/rag/query`:
  - `answerSource`
  - nullable `fallbackReason`
- These API fields are the primary Phase 14 deliverable. Rendering a human-readable source label is the responsibility of API clients; no new UI is included.
- Add optional request field `documentId` to select one authorized document for File First or document-scoped vector retrieval. Null/omitted means corpus-wide vector retrieval.
- Add a `sourceType` discriminator to citations while retaining existing document citation fields. File citations identify the exact document version/content hash and available page/section location, without requiring a chunk ID.
- Add optional request field `fallbackMode`, accepting `None`, `ModelKnowledge`, or `WebSearch`. In Increment 1, only `None` is available. Invalid values return `400`; a valid but unavailable/disallowed mode returns generic `403 FallbackModeNotAllowed`; neither changes the route or triggers another fallback.
- Keep fields additive for clients that ignore unknown response properties. Publish enum values and nullability in OpenAPI.

### Acceptance criteria

- Existing unscoped vector-RAG behavior and document citation validation are unchanged.
- A scoped, authorized document under both File First limits is supplied in full without chunk/vector retrieval and returns `answerSource=File` with valid file-level citation metadata.
- A scoped document exceeding either limit uses vector retrieval constrained to that document and returns `answerSource=Document`; scope is never widened.
- Requests without `documentId` retain corpus-wide vector retrieval, including semantic retrieval of small files.
- The tokenizer check includes all prompt components, enforces the 8,000-token ceiling, and never silently truncates File First content.
- A vector-RAG query with sufficient evidence returns `answerSource=Document`, valid document citations, and no fallback reason.
- A successful no-evidence/abstention response returns `answerSource=None` and the stable insufficient-evidence reason.
- Authentication, authorization, database, retrieval, and provider failures remain errors and are not represented as `None` or as a successful fallback.
- Legacy conversation data is not misrepresented as model- or web-grounded; existing clients continue to function.
- API contract tests cover omitted and supplied `documentId`, omitted `fallbackMode`, `None`, invalid enum values, and valid-but-unavailable fallback modes.

### Migration requirements

- Add nullable answer-source and fallback-reason fields to persisted assistant-message/provenance records as required by the existing model. Persist `File`, `Document`, and `None` outcomes distinctly.
- Add a citation source discriminator. Backfill existing citation rows to `Document` only where document/chunk foreign keys prove the source; leave ambiguous historical message provenance unknown/null. File citation snapshots must not require a fabricated chunk ID and must retain an immutable version ID or content hash identifying the exact text used, without storing another full-text copy.
- Add database constraints for allowed values and valid null combinations after data backfill: `File`/`Document` have no fallback reason; `ModelKnowledge`/`WebSearch` and successful `None` abstentions require the defined fallback reason. Keep legacy unknowns nullable. Enforce source/citation compatibility in application validation where cross-table constraints are impractical.
- Verify fresh and upgrade migrations on PostgreSQL/pgvector with the real EF provider. Do not mark the increment accepted on InMemory/SQLite-only results.

## 4. Increment 2 — ModelKnowledge fallback (first fallback implementation)

### Architecture changes

- Add a model-knowledge answer path using the existing Foundry chat abstraction; do not pass retrieved document text as supporting evidence when the route is `ModelKnowledge`.
- Invoke it only when the selected File First or vector retrieval completed successfully and was classified insufficient, and only when `fallbackMode=ModelKnowledge` is allowed by the temporary global test gate.
- Return an explicit unverified/model-knowledge notice. Do not emit document citations or model-invented citations.
- Do not fall back to model knowledge after retrieval, authorization, database, citation-validation, or Foundry service errors.
- Keep the gate disabled by default. Until Increment 4, enable only in an isolated test configuration; do not expose it as an unrestricted production option.

### API changes

- `POST /api/rag/query` accepts `fallbackMode=ModelKnowledge`; omitted or `None` continues File First/vector retrieval followed by abstention when evidence is insufficient. `fallbackMode` does not bypass either retrieval route.
- Successful fallback returns `answerSource=ModelKnowledge`, `fallbackReason=NoSufficientDocumentEvidence`, an explicit notice field or stable notice code, and an empty citations collection.
- Preserve the existing error contract for Foundry failures; do not return a success-shaped answer.

### Acceptance criteria

- Insufficient evidence from a successful File First or vector retrieval with the test gate enabled invokes the configured chat deployment once and labels the result `ModelKnowledge`.
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

- Add an `IWebSearchProvider` boundary and one approved provider implementation after provider, legal/terms, privacy, and region review. This is a later fallback increment; ModelKnowledge remains the first and only fallback implemented in Increment 2.
- Perform search server-side only. Submit only the minimized user query; never submit retrieved document text, tenant/user identifiers, or conversation history. Because the user's query itself may contain personal or confidential information, document this external data flow, obtain the required tenant/user approval, and apply an approved minimization/redaction policy before enabling WebSearch.
- Limit result count, snippet/content size, request time, and provider calls. Consume bounded provider-supplied results only. Phase 14 does not fetch result pages, crawl websites, follow result redirects, or retrieve arbitrary URLs; any such capability requires a separate phase/scope and threat-model approval.
- Treat provider-supplied search results/snippets and metadata as untrusted input. Keep them separate from system/developer instructions.
- Construct citation metadata server-side from provider results actually supplied to the model. Reject fabricated URLs, unsupported labels, and citations to results omitted by context limits.
- Keep the feature behind a global disabled-by-default test gate until Increment 4.

### API changes

- `POST /api/rag/query` accepts `fallbackMode=WebSearch` only when the test gate is enabled.
- Successful responses use `answerSource=WebSearch`, `fallbackReason=NoSufficientDocumentEvidence`, and citations discriminated as `sourceType=Web`.
- Web citation fields include provider result ID, title, canonical URL, publisher when supplied, and retrieval time. Do not return or persist arbitrary provider/model-generated URLs; validate safe schemes and render citations safely. URLs are citations only and are never fetched in Phase 14.
- Provider errors remain explicit failures; they do not silently invoke model-knowledge fallback.

### Acceptance criteria

- Web search is called only after successful authorized File First or vector retrieval is insufficient and the selected mode is permitted.
- Sufficient document evidence avoids a web call; `fallbackMode=None` never sends a query externally.
- Every returned web citation maps to a result included in the answer context; fabricated or omitted-result labels are rejected.
- Web queries contain no private corpus content or unnecessary user/tenant/history data.
- Oversized or unsupported provider results, unsafe citation URL schemes, timeouts, provider throttling, and malformed results are safely rejected/classified. Verify that result URLs are never fetched by the application; page-fetching, crawling, and URL-fetch/redirect SSRF controls are out of scope for this increment.
- Synthetic prompt-injection content in search results cannot override system policy, disclose secrets/other tenants, or forge citation metadata; test failures remain visible.
- A frozen web-answer evaluation checks factual support, citation quality, freshness metadata, abstention, latency, availability, and cost.

### Migration requirements

- Extend citation snapshots with nullable web-specific fields: provider, provider result ID, canonical URL, publisher, and retrieved-at timestamp. Add published-at only if the selected provider supplies and the product needs it.
- Preserve document citation fields and constraints; use source-type-specific validation so a web citation cannot masquerade as a document/chunk citation.
- Do not store full web pages by default. If excerpts are required for conversation rendering, approve their data/retention policy and impose a strict length limit.
- Add indexes only for demonstrated query patterns; verify fresh/upgrade migration paths and conversation deletion/retention behavior on PostgreSQL.

## 6. Increment 4 — Policy controls, tenant settings, and telemetry

### Architecture changes

- Replace temporary test gates with one policy service that evaluates server capability, tenant allowance, and requested `fallbackMode`; the most restrictive setting wins. Apply it consistently to ModelKnowledge and WebSearch.
- Default all tenant fallback permissions to off. A request can select an allowed mode but cannot enable a mode or change tenant policy.
- Provide a server-side global kill switch for all fallback and independent switches/quotas for model knowledge and web search.
- Tenant-policy storage assumption: use an already existing authoritative tenant-settings store only if it is approved for these controls and has an auditable update path. Otherwise, PostgreSQL `TenantRagPolicy` is the canonical store, keyed by the existing tenant identifier, with ModelKnowledge and WebSearch disabled by default for every tenant. Do not dual-write policy to PostgreSQL and configuration.
- Use an approved operator/admin configuration path for policy changes. Do not add a public policy-management endpoint without defining and enforcing a tenant-administrator policy; if self-service administration is required, treat it as a separately scoped administration dependency. If no approved update path exists, keep tenant permissions off.
- A server-side global kill switch and per-mode switches/quotas must override cached policy promptly; define cache invalidation and propagation before enablement.
- Emit content-free metrics/events for source mode, fallback reason, outcome, duration, result count, numeric token/search usage, and error class. Do not log questions, prompts, retrieved excerpts, answers, token text, or credentials.
- Define per-tenant rate/cost limits, timeouts, provider circuit breaking, and alert thresholds.

### API changes

- Enforce `fallbackMode` as a request to use an already-authorized mode, not a policy mutation.
- Return an explicit forbidden/disabled response when a caller requests a mode not permitted by tenant/server policy; do not disclose internal policy details.
- Add any policy administration API only if approved, tenant-scoped, audited, and protected by a dedicated administrator authorization policy. Otherwise keep settings in the existing approved configuration process.
- Document all modes, outcomes, error responses, notices, and citations in OpenAPI. Existing clients omitting `fallbackMode` retain no-fallback behavior; clients omitting `documentId` retain corpus-wide vector retrieval.

### Acceptance criteria

- Missing request mode and default tenant policy result in File/Document-grounded retrieval followed by abstention when evidence is insufficient.
- A tenant cannot request a mode it is not allowed to use; another tenant's configuration cannot affect the request.
- Global/per-mode kill switches immediately prevent fallback and leave File/Document-grounded retrieval available.
- Configuration changes have an authorized, auditable owner and take effect as documented.
- Metrics and alerts distinguish File, Document, ModelKnowledge, WebSearch, abstentions, and failures without sensitive content. Token-usage telemetry means numeric counts only, never token text or prompt content.
- Rate limits, timeouts, cost controls, and circuit-breaker behavior are verified for both providers.
- Cross-tenant/owner tests prove no unauthorized documents reach any model or search provider, response, persisted provenance, or telemetry.
- Compatibility, security, privacy, evaluation, migration, deletion, and rollback criteria pass in an isolated non-production environment.

### Migration requirements

- Under the storage assumption above, add a `TenantRagPolicy` table keyed by the existing tenant identifier with explicit ModelKnowledge and WebSearch permissions, both defaulting to false; do not use permissive defaults for existing tenants. If an approved authoritative store already exists, record the decision and use it as the sole source of truth instead of adding this table.
- Record policy-update audit data without storing user prompts or secrets. If the project has no approved admin/audit write path for policy changes, do not create an unauthenticated setting API; provision settings through the approved operator configuration process until administration is designed.
- Add an upgrade migration that preserves existing tenants with both fallbacks disabled, plus a safe downgrade/rollback plan that does not delete conversation data.
- Validate against PostgreSQL/pgvector with fresh and prior-schema databases after PG-01 is executable in an approved environment.

## 7. Cross-increment release gates

- Keep File/Document-grounded retrieval with all fallbacks disabled as the release default until all increments and Phase 12/13 acceptance gates pass.
- Use a frozen synthetic evaluation set covering sufficient evidence, no evidence, weak/ambiguous evidence, unauthorized scope, retrieval failures, and provider failures.
- Evaluate File First against vector RAG for answer support, latency, and numeric token cost; verify the exact file-token and full-prompt limits without truncation.
- Approve thresholds before running tests; measure false fallback, missed fallback, answer support, citation correctness, abstention, latency, cost, and privacy.
- Test all database migrations on the real PostgreSQL/pgvector provider, including conversation citation persistence, deletion, retention, and upgrade paths.
- Run live provider and end-to-end tests only with non-production Foundry/search resources and isolated data.
- Enable a restricted test tenant only after security and operations approval; expand only after monitored results meet the approved thresholds.

## 8. Delivery decision

This plan can guide design and API review now, but implementation and acceptance depend on resolving the execution bottleneck without changing the current workstation's security settings. The next prerequisite is an approved .NET execution environment, followed by PG-01 and the documented Phase 12/13 acceptance sequence. No increment should be represented as production-ready before those dependencies and its own acceptance criteria pass.
