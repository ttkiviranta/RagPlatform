# Phase 14 Proposal — Document-First Answers with Explicit Fallback Provenance

**Status:** Proposal for review; not an approved implementation plan.  
**Date:** 2026-11-16  
**Current dependency status:** Phase 12 and Phase 13 are implemented but acceptance-pending. PG-01 remains blocked on the local workstation by Smart App Control. No application code is changed by this proposal.

See the proposed [Phase 14 implementation plan](implementation-plan.md) for incremental delivery, contracts, migrations, and acceptance criteria.

## 1. Executive proposal

Improve the answer experience when the authorized document corpus does not contain sufficient evidence, while preserving document-grounded RAG as the primary and most trusted source. Add an optional **File First** path for a caller who explicitly selects a small document, without replacing corpus-wide vector retrieval.

**Preferred architecture:** a document-first, policy-controlled hybrid:

1. Authenticate and authorize the caller and any explicitly selected document.
2. For an explicitly selected, eligible small file, answer from its complete extracted text without invoking chunk/vector retrieval. For ordinary corpus questions, retain the existing authorized vector-RAG path.
3. Evaluate evidence sufficiency using a calibrated, testable policy; return a grounded answer with citations for the selected retrieval path when sufficient.
4. If evidence is insufficient, use only the fallback mode permitted by tenant/application policy: web search, model knowledge, or no fallback.
5. Identify provenance in the response and UI. In the first release, do not silently blend file, document, web, and model-knowledge evidence in one answer.

Keep the existing behavior (`DocumentOnly`) as the compatibility default. A tenant can explicitly enable `DocumentThenWeb` or `DocumentThenModelKnowledge` after privacy, security, and quality review. For factual answers requiring verifiable sources, prefer web results from a configured search provider; use model-knowledge fallback only as an explicit, clearly caveated option. Never fabricate citations for model knowledge.

## 2. Roadmap alignment and dependencies

The earlier root roadmap listed Copilot Studio integration, GraphRAG, administration, and AI engineering/operations as the Phase 14–17 themes, without detailed acceptance scope. This proposal recommends making **Adaptive Retrieval and Fallback Strategy** the new Phase 14 focus, moving Copilot Studio to Phase 15, retaining GraphRAG and administration as Phases 16–17, and adding Phase 18 for AI Engineering and Operations. This preserves all previously listed themes and adds a phase instead of silently dropping or merging work. The proposed sequence is documented in the root [README](../../../README.md) and [documentation index](../../README.md), but remains subject to project-owner approval; it is not an approved scope or delivery commitment.

Phase 12/13 acceptance remains pending. Design and contract review can proceed now, but production integration and acceptance should be gated on:

- PG-01 migration validation in an approved .NET execution environment.
- Phase 12 database/pgvector persistence and isolated import/settlement acceptance if answers depend on the imported corpus.
- Phase 13 database-backed retrieval, authorization, citation, and conversation acceptance.
- An approved corpus-quality baseline and evidence-sufficiency threshold before fallback routing is enabled.

Until these gates pass, use mocks or isolated prototypes only; do not represent fallback behavior as accepted end-to-end RAG behavior.

## 3. User behavior and scope

### In scope

- Preserve the current document-only answer path when relevant, authorized evidence exists.
- Add an optional File First path for a single explicitly selected small document; retain chunk/vector indexing and retrieval for unscoped corpus questions.
- Distinguish “no accessible document evidence” from retrieval/provider failures internally; do not use fallback to mask an outage or authorization error.
- Add policy-controlled fallback to either:
  - **Model knowledge:** answer from the configured model without claiming document grounding or citations.
  - **Web search:** search through a configured server-side provider, ground the answer in returned results, and provide validated web citations.
- Return machine-readable provenance and fallback reason; present a clear human-readable source label.
- Persist provenance with successful conversation turns and citation snapshots when conversation persistence is enabled.
- Add telemetry for selected path, outcome, duration, and source counts without logging query/document/answer content.

### Out of scope for the first release

- Autonomous browsing, arbitrary user-supplied URL fetching, or model-directed unrestricted tools.
- Silent mixing of document, search, and model-knowledge evidence in one answer.
- Treating model confidence as proof of truth or evidence sufficiency.
- GraphRAG, a new knowledge graph, broad agent/tool orchestration, or a new chat UI.
- Changing the current document authorization boundary or enabling fallback for unauthorized/missing document scopes in a way that reveals document existence.

## 4. Options compared

| Option | Purpose | Business value | Technical value | Effort | Phase 12/13 acceptance dependency |
| --- | --- | --- | --- | --- | --- |
| **File First** | For an explicitly selected small file, provide its complete extracted text directly as answer context, bypassing chunk/vector retrieval for that request. | Simple, faithful summaries and questions about a known short file; avoids chunk-boundary loss. | Low query complexity but has a strict context ceiling; file selection and authorization must be explicit. It is not a substitute for corpus-wide semantic search. | Medium | Requires Phase 12 extraction/readiness and Phase 13 authorization, citation, and conversation acceptance. |
| **Vector RAG (Document)** | Search authorized embedded chunks across the corpus and answer from relevant evidence; otherwise abstain or consider fallback. | Best fit for broad, semantic questions across many files. | Handles larger corpora and targeted context; depends on embeddings, vector index, ranking, chunk quality, and citation validation. | Existing | Phase 13 acceptance required to establish database-backed retrieval, authorization, citations, and quality. |
| **RAG + model knowledge** | On insufficient document evidence, call the chat model without presenting document context as support. | Better coverage for general facts such as “What is the capital of Finland?” | Moderate complexity; output is not independently sourced, may be stale or hallucinated, and has no verifiable citations. | Medium | Strong Phase 13 dependency for reliable evidence gating and authorization. Can be prototyped separately, but must be evaluated after the RAG baseline. |
| **RAG + web search** | On insufficient document evidence, search approved public sources and generate an answer from those results. | Better freshness and verifiability; can provide links and publishers. | Adds a provider, data egress, source sanitization, injection, SSRF, latency, cost, and availability concerns. | High | Strong Phase 13 dependency; also requires approval of query privacy, provider terms, source policy, and web citation persistence. |
| **Hybrid: RAG first, policy-selected fallback** | Prefer documents; when insufficient, use tenant-approved web search or model knowledge, with explicit provenance. | Best balance of private-corpus answers and broader coverage; clear user choice and trust boundaries. | Highest routing and evaluation complexity, but supports extensibility without conflating sources. | High | Requires accepted Phase 12/13 foundations before broad enablement. Fallback modes can be developed behind a disabled policy and tested independently. |

## 5. Proposed architecture

### Request flow

1. **Authentication and scope:** establish caller, tenant, roles, and any document scope before retrieval.
2. **File First eligibility:** when the request names one accessible document by its existing document ID, load its canonical extracted text. If it meets the small-file limits below, use that complete text directly and skip chunk/vector retrieval for this request. Do not infer a file selection from an ambiguous title or expose whether an inaccessible document exists.
3. **Authorized vector retrieval:** when no file is explicitly selected, use corpus-wide pgvector retrieval with current authorization filters. If a selected file is too large for File First, use pgvector constrained to that file; never widen an explicitly selected scope. Never pass unauthorized candidate content to the answer or fallback stage.
4. **Evidence decision:** classify the selected retrieval result as `Sufficient`, `Insufficient`, or `RetrievalFailure`. Define sufficiency from frozen Phase 13 evaluation data; do not tune thresholds after seeing acceptance results.
5. **Grounded answer:** for sufficient File First evidence, answer from the complete selected file and cite it with provenance `File`. For sufficient vector evidence, answer from bounded authorized chunks and cite them with provenance `Document`.
6. **Fallback decision:** for `Insufficient`, evaluate tenant policy and the request's allowed fallback choice. If disabled, return the existing insufficient-evidence response. If enabled, invoke exactly one fallback path.
7. **Model-knowledge fallback:** invoke the configured chat deployment without representing the corpus as supporting evidence. Include a clear caveat and return no document citations.
8. **Web-search fallback:** call a server-side `IWebSearchProvider` using only the minimized user query; select bounded results, treat result content as untrusted, and pass those results to the model as explicitly untrusted web evidence. Server-map citations only to results actually supplied to the model.
9. **Failures:** do not invoke fallback for authentication/authorization denials, database/retrieval errors, timeout, provider failures, or invalid citation output. Return the existing explicit error behavior and failure telemetry.
10. **Persistence and telemetry:** persist the answer, source type, fallback reason, and source snapshots atomically with a successful conversation turn. Record outcome metadata only; do not log prompt, query, excerpts, or answer text.

### File First eligibility and thresholds

File First is a scoped optimization, not a global replacement for vector search. A small file is not necessarily relevant to an arbitrary corpus question, and bypassing chunking/vector indexing for every small upload would reduce semantic recall. Keep the existing chunk/vector path and indexing policy for corpus-wide questions.

Initial thresholds for an isolated evaluation (not production defaults until validated):

- **One selected file per request.** The caller explicitly supplies an authorized document ID; multiple-file synthesis is out of scope for the first release.
- **Small-file ceiling:** no more than **4,000 tokens** of canonical extracted text and no more than **24 KiB UTF-8** of that text; whichever limit is reached first excludes the file from File First.
- **Prompt ceiling:** reserve at most **8,000 tokens** total for the selected file, system instructions, question, and answer budget. Count with the configured model's tokenizer before invocation; reject the File First route rather than silently truncating the file if the budget cannot be met.
- Count normalized extracted text, not the uploaded PDF/DOCX/binary size. Apply independent existing upload/extraction limits before this routing decision.
- If a selected file exceeds either small-file limit, use vector retrieval constrained to that document, if supported; otherwise return the established validation response. Never send an oversized full file as a fallback to File First.
- Revisit these starting values using representative short-file summaries, question-answer quality, latency, and token-cost measurements before enabling the route beyond a test tenant.

This proposal bypasses **chunk/vector retrieval at query time** for eligible, explicitly selected files. It does not recommend skipping chunking/embedding at ingestion by default: doing so would make small files unavailable to semantic corpus-wide vector queries. Any future File-only ingestion mode needs its own recall evaluation and explicit product scope.

### Trust and source separation

- A response has one primary source mode in the MVP: `File`, `Document`, `WebSearch`, `ModelKnowledge`, or `None` (abstention). `File` means the complete extracted text of one explicitly selected file was supplied as context; `Document` means vector-retrieved document chunks were supplied. Do not label an answer `Hybrid` if its claims cannot be traced to individual source types.
- File citations reference the authorized document and file metadata, with page/section location when extraction provides it; they do not claim chunk-level provenance. Document citations reference authorized persisted chunk/document records.
- Web citations reference provider result IDs and canonical URLs, titles, publishers where available, and retrieval time. The server—not the model—constructs citation metadata.
- Model-knowledge answers have no citations and carry a prominent “model knowledge; not verified against your documents” notice.
- If future requirements need source mixing, add claim-level provenance and a separate evaluation gate rather than combining sources invisibly.

## 6. API and data contracts

Keep `POST /api/rag/query` as the entry point. Add optional, backward-compatible request selection; a missing field preserves current document-only behavior. The caller may request only modes enabled by the tenant/server policy.

### Proposed request addition

```json
{
  "query": "What is the capital of Finland?",
	"topK": 5,
  "documentId": "selected-document-id",
  "fallbackMode": "WebSearch"
}
```

`documentId` is optional and identifies one existing document that the caller is authorized to access; it is not a file path or storage URL. When supplied and eligible, it requests File First; an oversized document uses vector retrieval constrained to that document. When omitted, the request uses corpus-wide vector RAG as today. `fallbackMode` is an enum: `None`, `ModelKnowledge`, or `WebSearch`. It is not a URL, provider endpoint, or free-form tool instruction. The effective mode is the intersection of the request, tenant policy, and server capabilities. Default: `None` for existing clients and tenants until explicitly enabled.

### Proposed response additions

```json
{
  "answer": "Helsinki is the capital of Finland.",
  "answerSource": "WebSearch",
  "fallbackReason": "NoSufficientDocumentEvidence",
  "citations": [
	{
	  "label": 1,
	  "sourceType": "Web",
	  "title": "Example reference title",
	  "url": "https://example.org/reference",
	  "publisher": "Example publisher",
	  "retrievedAtUtc": "2026-11-16T12:00:00Z"
	}
  ]
}
```

For a `File` answer, return `answerSource: "File"` and a citation identifying the selected document/file, without a chunk ID unless the citation contract separately has one. For a `Document` answer, retain existing document citation fields and identify the source type. `answerSource` values are `File`, `Document`, `ModelKnowledge`, `WebSearch`, and `None`; citation `sourceType` distinguishes `File`, `Document`, and `Web`. For `ModelKnowledge`, return `answerSource: "ModelKnowledge"`, an explicit caveat, and no fabricated citations. For abstention, return `answerSource: "None"`, a stable outcome/fallback reason, and no citations.

### Persistence and compatibility

- Extend citation/provenance snapshots with a source discriminator and the minimum safe metadata required to render the source. File snapshots identify the document/file without requiring a chunk ID; vector Document snapshots remain tied to persisted document/chunk IDs; web snapshots should not store full pages by default.
- Add a migration for the new metadata and preserve existing conversation-history behavior. Review retention/deletion semantics for external web citation snapshots.
- Keep existing clients working when they omit `fallbackMode`; document any new response properties and enum values in OpenAPI.
- Avoid persisting a partial conversation turn when fallback generation, citation validation, or provenance persistence fails.

## 7. Security, privacy, and operations

- **Authorization first:** run existing tenant/owner/document checks before using corpus context. A fallback must not reveal that inaccessible documents exist or their contents.
- **File scope:** validate access to `documentId` before loading extracted text. Apply existing tenant/owner rules and the same content retention/deletion policy; do not resolve caller-supplied paths or storage URLs.
- **No error masking:** fallback is only for a successful retrieval classified as insufficient; never convert an authorization, database, Foundry, or search-provider failure into a plausible answer.
- **Web query privacy:** web search sends information outside the platform. Minimize the query; do not send retrieved document text, tenant/user identifiers, conversation history, or private metadata. Require tenant-level approval/enablement and document the provider's data handling and retention terms.
- **Safe web access:** use a trusted server-side search provider, not arbitrary URL fetches. Enforce destination/redirect validation, private-network and metadata-service protections, timeouts, response-size/content-type limits, and rate limits.
- **Untrusted content:** treat web pages and uploaded documents as untrusted instructions. Keep them out of system/developer instructions; test prompt injection and citation forgery against both source types.
- **Citation integrity:** accept only server-issued result labels/IDs supplied to the model; reject unsupported labels and fabricated URLs. A valid citation link is not proof of semantic entailment; evaluate support separately.
- **Model knowledge:** mark it as unverified, avoid authoritative/legal/medical/financial claims without configured restrictions, and do not manufacture a source list.
- **Cost and reliability:** apply per-tenant quotas, request/token limits, provider timeouts, circuit breaking, and usage metrics. Record source mode, fallback reason, result counts, latency, and outcome without content.
- **Feature control:** fallback remains disabled by default and can be enabled per approved tenant/environment. Provide a server-side kill switch and keep document-only behavior available.

## 8. Acceptance criteria

Phase 14 is accepted only when the following pass on the accepted Phase 12/13 foundation and an isolated non-production environment:

1. **Document-first routing:** sufficient authorized document evidence produces a document-grounded answer with only valid document citations and does not call web search or model-knowledge fallback.
2. **File First:** an authorized, explicitly selected file under both thresholds is supplied in full without chunk/vector retrieval and returns `answerSource=File` with valid file-level citation metadata; an oversized file routes to scoped vector retrieval and returns `Document` provenance.
3. **Default corpus retrieval:** requests without `documentId` retain authorized vector-RAG behavior, including semantic retrieval of small files; no global File First shortcut silently reduces corpus recall.
4. **Insufficient evidence:** with fallback disabled, the existing explicit not-found/abstention behavior remains unchanged.
5. **Web fallback:** when enabled and selected, a no/insufficient-evidence query uses the configured provider, returns only citations bound to search results supplied to the model, identifies `WebSearch`, and records the fallback reason.
6. **Model-knowledge fallback:** when enabled and selected, returns `ModelKnowledge` provenance and the required caveat, with no invented document or web citations.
7. **Failure separation:** retrieval, authorization, database, Foundry, or search-provider errors do not silently trigger another source mode or return a success-shaped answer.
8. **Authorization isolation:** cross-tenant/owner tests show unauthorized full-file text or chunks never reach model context, fallback query, response, citations, conversation storage, or logs.
9. **Privacy and injection:** synthetic malicious document/web content cannot override system policy, disclose secrets/other tenants, or forge source metadata; web queries contain no private corpus content or unnecessary identity/history data.
10. **Conversation integrity:** successful answer and provenance snapshots persist atomically; failed requests create no partial turn; owner/tenant access and deletion behavior remain correct.
11. **Quality and user clarity:** a frozen evaluation set measures File First routing eligibility, vector retrieval sufficiency, answer correctness, citation support, abstention, and user-visible provenance; thresholds are pre-approved and met.
12. **Operations:** telemetry is content-free; source-mode latency, failures, abstentions, token/search cost, quotas, alerts, and kill-switch behavior are demonstrated. Record p50/p95/p99 and agreed cost/error thresholds.
13. **Compatibility:** existing clients that omit `documentId` and `fallbackMode` retain document-only vector behavior and the documented response contract.

## 9. Recommended delivery sequence

1. Review and approve the proposed roadmap re-sequencing: Adaptive Retrieval and Fallback Strategy as Phase 14, Copilot Studio as Phase 15, GraphRAG as Phase 16, Administration as Phase 17, and AI Engineering and Operations as Phase 18.
2. Complete Phase 12/13 acceptance planning and obtain an approved execution environment; run PG-01 first. Do not weaken the local workstation policy.
3. Establish the Phase 13 quality baseline and evidence-sufficiency criteria before adding fallback routing.
4. Deliver the backward-compatible provenance contract and deterministic routing tests, including the explicitly scoped File First path and its size/token gates.
5. Add policy-controlled model-knowledge fallback and evaluate it separately from File and Document answers.
6. Add web search only after privacy/provider review, safe-fetch design, citation snapshots, and threat-model approval.
7. Enable the feature only for an isolated test tenant; expand after all security, quality, cost, and operational gates pass.

## 10. Recommendation

Subject to project-owner approval of the roadmap re-sequencing, adopt the **hybrid, document-first, policy-controlled** architecture as Phase 14, with a narrowly scoped File First path for explicitly selected small files. Use `File` only when the complete extracted content fits the stated limits; keep `Document` vector RAG as the default for corpus-wide semantic questions. Do not globally skip chunking/embedding for small uploads without separate recall evidence. Prefer web search for an explicitly enabled fallback when external sources and verifiable links are desired; offer model knowledge as a separate opt-in mode with a clear unverified notice. Do not silently mix sources or trigger fallback on system errors.

Because Phase 12/13 acceptance is pending and PG-01 has no approved alternate execution environment yet, approve the proposal and API/security design now, but defer production-path implementation and enablement until the existing RAG, authorization, citation, and database acceptance gates can be completed.

The linked [implementation plan](implementation-plan.md) predates this File First extension. If this extension is approved, update that plan's provenance contract, routing increment, persistence requirements, and acceptance gates before implementation begins.
