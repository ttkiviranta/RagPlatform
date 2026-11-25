# Phase 13 Live Evaluation Pilot Runbook

**Status:** Procedure for an initial live pilot; not an acceptance result  
**Scope:** Live RAG answer and citation review over the five-PDF evaluation corpus

This pilot extends the [Phase 13 answer-quality validation plan](phase13-answer-quality-validation-plan.md). Phase 12 functional validation established that the five PDFs were ingested and that vector, full-text, hybrid, and semantic retrieval returned expected documents for the recorded queries. It did not establish live answer correctness or citation entailment. The deterministic evaluation harness tests answer-generation boundaries and PostgreSQL citation/persistence behavior using fakes; it is not a live Foundry evaluation.

## 1. Prerequisites and safeguards

- Use an approved non-production execution host, a dedicated migrated PostgreSQL/pgvector database, isolated Azure AI Foundry deployments, and synthetic or approved non-sensitive test data. Do not use production documents, shared queues, or production identities.
- Select an active application test user authorized to read the evaluation corpus. The API caller's Entra access token and the API process's Foundry workload credential are separate identities; confirm each independently. Do not record tokens or credentials.
- Confirm that all five documents below are present and accessible to the test user, and that their chunks and embeddings exist in the selected database:
  - `identity-security.pdf`
  - `azure-servicebus.pdf`
  - `postgresql-pgvector.pdf`
  - `employment-policy.pdf`
  - `hybrid-search-demo.pdf`
- Verify the stored embedding deployment name and both stored and actual vector dimensions match the configuration. The recorded validation used `text-embedding-ada-002` and 1536 dimensions. A different deployment alias can produce zero vector results even when its model has the same dimensions; do not relabel old embeddings without validating compatibility.
- Confirm the selected chat deployment supports the application's OpenAI-compatible `chat/completions` request, including system/user messages, `max_tokens`, and the configured temperature. Verify quota, network access, timeout, spending limits, and ownership before use.
- The API uses `DefaultAzureCredential` and requests the `https://ai.azure.com/.default` scope. Determine the credential actually selected by the test host and confirm its least-privilege Foundry inference data-plane access for the selected resource and API. Do not assume Azure CLI or Visual Studio is the selected identity.
- Agree on the evaluation owner, reviewer, adjudicator, pilot stopping rules, budget, allowed telemetry, storage location, retention period, and cleanup owner. Inspect logging/telemetry configuration for accidental collection of questions, prompts, excerpts, answers, and secrets.
- Do not use `RAGPLATFORM_TEST_POSTGRES_CONNECTION` for the pilot database: the PostgreSQL test fixture truncates application tables. If deterministic database tests are rerun, use a separate disposable test database.
- Blob Storage and Service Bus are not required for this pilot if the corpus is already imported. Keep the document-import worker disabled unless a separate isolated ingestion run has been approved.

## 2. Runtime configuration

Configuration can be supplied by environment variables or another approved .NET configuration source such as User Secrets. Double underscores map to nested configuration keys. Values below are configuration requirements, not proof that a resource is available.

| Environment variable | Pilot requirement |
|---|---|
| `ConnectionStrings__DefaultConnection` | Required: connection to the isolated, migrated pilot database. Treat the connection string as sensitive. |
| `Foundry__Endpoint` | Required: absolute HTTPS OpenAI-compatible endpoint ending in `/openai/v1`, without credentials, query, or fragment. |
| `Foundry__ChatDeployment` | Required: approved, enabled, compatible chat deployment name. |
| `Foundry__EmbeddingDeployment` | Required: approved embedding deployment name matching the stored embedding model/deployment name. |
| `Foundry__EmbeddingDimensions` | Set to the deployment and stored vector dimension; the recorded corpus used `1536`. |
| `Foundry__MaxOutputTokens` | Record the effective value; default `800`, must be greater than zero. |
| `Foundry__ChatTimeoutSeconds` | Record the effective value; default `60`, allowed range `1`-`60`. |
| `Foundry__ChatTemperature` | Record the effective value; default `0.2`, allowed range `0`-`2`, subject to model support. |
| `Rag__UseHybridSearch` | Set `false` for the baseline; set `true` only for the comparison run. |
| `Rag__UseReranking` | Set `false` for both runs to compare vector and hybrid RRF without reranking. |
| `Rag__AllowModelKnowledgeFallback` | Set `false` for both runs to preserve grounded abstention. |
| `ServiceBus__DocumentImportWorkerEnabled` | Set `false` for this pilot. |
| `AzureAd__Authority` and `AzureAd__Audience` | Supply approved test values when the checked-in/default configuration is not the selected test application. Confirm issuer, audience and active application user. `AzureAd__TenantId` and `AzureAd__ClientId` are also used as configuration fallbacks where applicable. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional, when an approved isolated Application Insights resource is used and `ApplicationInsights__ConnectionString` is not set. Protect the value and verify retention/privacy settings. |
| `ASPNETCORE_URLS` | Optional: set an approved listener URL when launching the DLL without a launch profile; do not expose an unauthenticated or production listener. |

Use the actual effective values and permitted configuration source; defaults in `appsettings.json` are not evidence of live resources. Configure secrets without placing them in shell history, screenshots, source control, or run reports. Do not paste a real token into this runbook or an issue.

## 3. Retrieval settings and limits

1. Run the initial seven cases with `Rag__UseHybridSearch=false` and `topK=5`.
2. Restart the API with `Rag__UseHybridSearch=true` and repeat **AQ-005 and AQ-006** using the same corpus, identity, Top-K, and model settings. Record this as a separate hybrid comparison, not a replacement for the vector baseline. Keep `Rag__UseReranking=false`; a separate [reranking evaluation](reranking-evaluation.md) compares RRF with reranking.
3. Hybrid retrieval falls back to vector when it fails or returns no results. Check the sanitized application warnings for `Hybrid search failed` or `Hybrid search returned no results`; record the actual fallback if observed. If the actual path cannot be established, mark it **unknown**, not hybrid success. Investigate before scoring a hybrid comparison.
4. Full-text-only is a standalone search endpoint, not a RAG mode. `/api/rag/query` accepts `query`, `topK` (1-20), optional `conversationId`, and optional `tags`; it does not accept a document ID. Omit tags for the unfiltered pilot corpus.
5. The citation service reloads authorized persisted chunks and produces normalized excerpts; excerpts are capped at 500 characters and the assembled answer context is bounded to 4,000 characters. Citation labels are assigned at runtime. Do not assume a particular document always receives `[1]`.

## 4. Exact execution procedure

### 4.1 Prepare and freeze the run

1. Record run ID, UTC time, evaluator/reviewer names or roles, environment alias, application commit, migrations, pgvector version, corpus file hashes and imported dataset identifier. Record embedding and chat deployment aliases and model versions, dimension, prompt revision/hash, retrieval setting, Top-K, context limit, temperature, output-token limit, and timeout.
2. Confirm migrations and the document/chunk/embedding checks in section 1. Check that the selected user's tenant/owner scope includes the intended corpus. Verify known gold facts against the actual source passages before starting; resolve ambiguous or unsupported expectations with the reviewer and record the approved rubric.
3. Record links to the existing deterministic CI results for this application commit. Do not rerun database integration tests against the pilot database. Prior deterministic passes and prior retrieval validation are prerequisites/context, not live-answer results.
4. Configure the API and Foundry values in section 2. Confirm the selected inference credential, permissions, endpoint reachability, and chat/embedding deployment compatibility without exposing secrets. Confirm the intended cost and request budget.
5. Start the API on the approved host. For a built local Debug DLL, from the repository root in PowerShell, one supported launch is:

   ```powershell
   Set-Location .\RagPlatform.Api
   dotnet .\bin\Debug\net10.0\RagPlatform.Api.dll
   ```

   Use the approved host's deployment/start procedure and configuration when different. The repository's launch profiles specify `https://localhost:7085` and/or `http://localhost:5172`, but a direct DLL launch need not use those profiles. Use the actual listening URL printed at startup or the approved `ASPNETCORE_URLS` value; do not assume port 5000. Use the supported local HTTPS configuration when transmitting a bearer token. Open the corresponding `/swagger` UI only if enabled in the selected environment.
6. Authenticate using an approved short-lived Microsoft Entra access token for the API audience and an active test user. Confirm the user can access the approved corpus. Keep the token out of logs, reports, and screenshots.

### 4.2 Run each case

1. In Swagger, select **POST `/api/rag/query`**, choose **Try it out**, and submit the following shape with the exact question from section 5:

   ```json
   {
	 "query": "What does a dead-letter queue do?",
	 "topK": 5
   }
   ```

   Omit `conversationId` for every independent case and repetition, so prior history cannot influence the result. A successful request creates a new conversation, including an abstention; arrange approved cleanup and retention for these records. Do not add a `documentId` property.
2. Execute AQ-001, AQ-003, AQ-004, AQ-005, and AQ-006 once each. Execute AB-001 and AB-005 **three independent times each** with the same frozen settings. Record every attempt, including errors and skipped attempts; do not silently retry or replace failures. With no infrastructure errors, this baseline comprises 11 requests.
3. For each attempt, record UTC time, case ID, repetition, mode, HTTP status, elapsed time if measured, response classification, and permitted correlation/audit identifier. Inspect the returned `answer`, `conversationId`, and `citations`. For each citation, check the runtime `citationNumber`, `documentId`, `chunkId`, `sourceName`, `excerpt`, and nullable `pageNumber` against authorized persisted data.
4. For answerable cases, assess each material answer claim against the associated cited excerpt and the verified gold passage. Do not mark a case passed merely because a document name or a syntactically valid label appears. A label may be valid even when its excerpt does not support a claim.
5. For abstention cases, the expected answer is `not found in accessible documents` and the citation list must be empty. Confirm that the question is truly unanswerable in the accessible corpus. Empty usable context produces `NoContext` without a chat call; nonempty insufficient context is a separate live-model behavior. Use available telemetry or controlled observation to distinguish them; if unavailable, record **unknown**. A successful abstention can be persisted as a normal exchange.
6. A non-2xx response, deployment/authentication error, timeout, unavailable evidence, or unreviewed output is **not a pass**. Record it as `error` or `needs-review`, preserve sanitized diagnostics, and investigate. Do not infer the result from a search result or from the deterministic tests.
7. Restart the API with hybrid enabled and repeat AQ-005 and AQ-006 once each. Record actual fallback status as described in section 3. Restore the approved setting afterward. This adds two requests, for 13 planned requests total.

For an optional search-only diagnostic, use the appropriate standalone search endpoint with the same question and Top-K. Its output can help locate candidates, but it is a separate request and does not prove which chunks entered the RAG request's actual model context.

## 5. Pilot cases and expected evidence

All expectations below require review against the actual corpus and the bounded excerpt used in the run.

| Case | Exact question | Expected document and facts | Expected outcome |
|---|---|---|---|
| AQ-001 | What does a dead-letter queue do? | `azure-servicebus.pdf`: permanently unprocessable messages or messages exceeding delivery limits are retained for inspection and controlled replay. | Supported answer with citation to the relevant Service Bus passage. |
| AQ-003 | What is HNSW and how should it be used in pgvector retrieval? | `postgresql-pgvector.pdf`: approximate nearest-neighbor indexing can reduce latency as data grows, with recall, memory, construction, and query-efficiency tradeoffs. | Supported answer with relevant pgvector citation(s). |
| AQ-004 | What determines an employee's notice period? | `employment-policy.pdf`: agreement, length of service, jurisdiction, and mandatory statutory rules matter; no single fixed period is specified. | Supported answer; no invented duration or jurisdiction-specific rule. |
| AQ-005 | How does Reciprocal Rank Fusion combine rankings? | `hybrid-search-demo.pdf`: add `1 / (k + rank)` contributions for available sources; an absent source contributes zero. The plan describes one-based ranks, `k=60`, and source flags. | Supported answer with relevant RRF passage(s); require only details actually present in the cited context. |
| AQ-006 | How does the system stop one organization from reading another organization's files? | `identity-security.pdf`: tenant filtering and server-side authorization; ownership checks for ordinary users and appropriately scoped administrative roles. | Supported answer with relevant identity/security citation(s); no unsupported claims. |
| AB-001 | What is the exact serial number in the approved corpus? | No relevant fact expected in accessible corpus; verify before scoring. | Exact insufficient-evidence response and no citations. |
| AB-005 | What was the founder's birthday? | No relevant fact expected in accessible corpus; verify before scoring. | Exact insufficient-evidence response and no citations. |

## 6. Evidence to capture

Record, without secrets or unapproved content:

- Run metadata from section 4.1, the actual retrieval mode/fallback or `unknown`, and the source of each observation.
- Case ID, repetition, UTC time, HTTP status, answer/abstention/error classification, expected and observed document/chunk IDs, citation numbers, and reviewer/adjudicator verdicts.
- Retrieval/generation/end-to-end latency and input/output tokens **when actually available** from approved telemetry or audit records. Record unavailable metrics as `unknown`; calculate cost only from observed usage and approved pricing. Do not assume the API response includes token counts, candidate rankings, or actual retrieval mode.
- Whether sufficient evidence was present in the **actual bounded context**. The response exposes only cited sources, not all retrieved candidates or the exact model prompt. If approved controlled observation cannot establish this, mark context inclusion/entailment `needs-review` or `unknown`; a separate search call is diagnostic only. Do not add raw prompts to ordinary logs to fill the gap.
- Sanitized correlation/audit IDs, failures, telemetry/privacy review, evaluator and adjudicator decisions, and any approved exception. Do not archive tokens, raw prompts, excerpts or answers unless an explicitly approved test-data and retention policy permits it.
- Where permitted, verify that the persisted conversation and citation snapshots match the response. Persisted snapshots contain excerpts and therefore require the same access and retention controls as test content.

## 7. Per-attempt pass/fail recording template

Copy one row per case, mode, and repetition. Use an approved controlled record rather than committing results here.

| Field | Record |
|---|---|
| Run ID / case / repetition | |
| UTC time / evaluator / reviewer | |
| Commit / corpus hash / environment alias | |
| Requested mode / actual mode or fallback | |
| Chat and embedding deployment/version / Top-K | |
| HTTP status / elapsed time / correlation ID | |
| Expected source / expected behavior | |
| Observed source and chunk IDs / citation labels | |
| Actual bounded-context evidence | Verified / unknown; observation source |
| Answer classification | Supported answer / abstention / error / other |
| Claim correctness and citation entailment | Pass / fail / needs-review / not applicable |
| Abstention and citations | Pass / fail / needs-review / not applicable |
| Token usage / cost | Observed values and source, or unknown |
| Verdict | Pass / fail / error / needs-review / skipped |
| Finding / adjudicator / follow-up | |

**Pilot verdicts:** Pass an answerable attempt only when its required facts are correct and its material claims are supported by authorized, actually supplied cited evidence. Pass an abstention attempt only when the reviewed corpus does not support an answer and the required fallback and empty citations are observed. Record unsupported claims, invalid citations, unexpected answers, or unauthorized disclosure as failures; infrastructure errors are `error`, not quality passes. Missing context evidence or pending human review is `needs-review`, not pass. Any unauthorized disclosure or secret exposure stops the pilot and must be escalated under the approved incident procedure.

## 8. Manual reviewer checklist and closeout

- [ ] The corpus, application commit, identity, deployment versions, prompt/settings, and mode were frozen and recorded.
- [ ] The test question and expected facts were checked against the approved corpus, not inferred from a search result alone.
- [ ] Each answerable case addresses the question without invented details; each material claim is supported by its associated authorized citation in the actual supplied context.
- [ ] Citation labels match the response's server-assigned citations, and document/chunk metadata and excerpts match persisted authorized records. `pageNumber` may be null.
- [ ] Each abstention repetition is genuinely unanswerable, uses the required fallback, and returns no citations; distinguish `NoContext` from nonempty insufficient evidence when observable.
- [ ] Hybrid comparisons disclose fallback or unknown actual mode; separate search diagnostics are not treated as proof of the RAG context.
- [ ] Errors, skips, unknown measurements, and pending reviews are recorded explicitly; nothing is silently counted as a pass.
- [ ] No credential or unauthorized content is exposed in the response, logs, telemetry, snapshots, or evidence record; retention and cleanup are assigned.
- [ ] Findings are adjudicated and the pilot summary includes attempted/passed/failed/error/needs-review/skipped counts and limitations.

A successful pilot demonstrates only the reviewed cases in the recorded environment. It does **not** establish statistical quality thresholds, live prompt-injection or cross-tenant acceptance, operations readiness, or Phase 13 rollout approval. Continue with the broader answer-quality and acceptance plans before changing the phase disposition.
