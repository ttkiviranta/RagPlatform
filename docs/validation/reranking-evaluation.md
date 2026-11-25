# Reranking evaluation

This opt-in experiment compares hybrid RRF with the existing Foundry reranker. It reuses the authorized hybrid search API, the existing reranking service and the Phase 13 pilot's five-PDF corpus and questions. It is not a Phase 13 acceptance result, an end-to-end RAG answer test, or evidence that reranking improves quality.

## Design

For each case, the runner requests hybrid search at `topK` (the production baseline), then at `min(2 * topK, 40)`, with identical tags and caller identity. It sends the expanded results through `FoundryRerankingService` and retains the first `topK`. This mirrors the successful retrieval path in `RagService`; the larger hybrid request can produce a different RRF order, so its prefix is not used as the baseline. The endpoint applies the application's tenant, owner and tag filters. A failed or empty baseline is recorded, not silently counted as a hybrid result; the runner does not simulate RAG's vector fallback. An expansion or reranker failure is recorded as a fallback, not as a successful comparison. The runner invokes the reranker directly; neither `Rag:UseHybridSearch` nor `Rag:UseReranking` needs to be enabled on the API for this comparison.

The report includes ordered chunk and document IDs, shared chunk count, count of changed positions, and the rank of the first human-judged relevant **document** in each top-K. A lower first-relevant rank or a new hit is an improvement; a higher rank or lost hit is a regression. Both misses are unchanged. Cases with an empty relevance list are unjudged and have no quality verdict. The summary includes hit rate and mean reciprocal rank for successful, judged pairs only; misses count as zero. It reports the paired denominator and unpaired cases separately. These coarse document-level labels do not establish that a specific chunk supports an answer; review the passages manually. Ordered chunks allow further inspection. An improvement count alone is not proof of overall quality improvement.

Baseline latency is the first HTTP search. Enabled-path latency is the sum of that search, the second HTTP search and the reranker call; added latency is the difference. These are client-side approximations, not measured API end-to-end RAG latency: they include HTTP overhead but exclude answer generation, citations and some local orchestration. The first request may warm caches for the second; perform repeated runs and interpret small samples cautiously. Input/output tokens are the reranker chat response's reported usage only; `null` means unavailable. Search embedding tokens, answer tokens and total cost are not measured. The standalone runner's Foundry chat call has no application audit identity, so its reranker call is not written to the API's audit log; search calls use the API's normal audit behavior.

## Prepare a run

1. Follow the [live pilot runbook](phase13-live-evaluation-pilot-runbook.md) for a non-production environment, authorized test identity, corpus and credential safeguards. Do not point PostgreSQL integration tests at the pilot database. The comparison makes two hybrid searches and at most one billable reranker chat call per case; check budget, quota and data handling before running.
2. Freeze the application commit, corpus, user/tenant, tags, Top-K, embedding and chat deployments/settings, and case set. Use the existing pilot questions AQ-001, AQ-003, AQ-004, AQ-005 and AQ-006, or another reviewed set. Verify relevant document IDs and passage relevance in the actual corpus before running. Add cases where the baseline is expected to struggle as well as ordinary cases; do not choose labels after seeing results.
3. Create a JSON case file outside the repository. Replace the illustrative ID with the actual authorized document ID, and include a unique row per case:

   ```json
   [
	 {
	   "caseId": "AQ-001",
	   "query": "What does a dead-letter queue do?",
	   "relevantDocumentIds": ["11111111-1111-1111-1111-111111111111"],
	   "tags": []
	 }
   ]
   ```

   `relevantDocumentIds` must be present; use `[]` for an unjudged case. Tags are optional and match all supplied tags. Use only approved non-sensitive questions and documents. The case file contains queries; protect it according to your data policy.
4. Start the configured API on the isolated environment and use an active Entra test user authorized for that corpus. Configure the runner process's `DefaultAzureCredential` separately with access to the approved Foundry deployment. Use the same Foundry endpoint and chat settings as the API. Supply values through a protected environment or secret store, not source control or command history:

   | Environment variable | Meaning |
   |---|---|
   | `RAG_EVAL_RUN` | Set to `1` to opt in to live billable calls. |
   | `RAG_EVAL_API_URL` | API origin, HTTPS or loopback HTTP. |
   | `RAG_EVAL_API_TOKEN` | Short-lived bearer token for the API, kept secret. |
   | `RAG_EVAL_CASES` | Absolute path to the reviewed JSON input. |
   | `RAG_EVAL_OUTPUT` | Absolute path to a **new** protected JSON output file; existing files are not overwritten. |
   | `RAG_EVAL_TOPK` | Optional, 1-20; default 5. |
   | `Foundry__Endpoint`, `Foundry__ChatDeployment` | Approved Foundry endpoint and deployment. |
   | `Foundry__ChatTemperature`, `Foundry__MaxOutputTokens`, `Foundry__ChatTimeoutSeconds` | Optional; defaults 0.2, 800 and 60. Match the API's effective settings. |

5. From the repository root, run:

   ```powershell
   dotnet test .\RagPlatform.Api.Tests\RagPlatform.Api.Tests.csproj --filter "FullyQualifiedName~LiveRerankingEvaluationTests.CompareHybridAndReranking"
   ```

   Without opt-in, this test is inconclusive and makes no live calls. The report is written even when individual cases fail; the test then fails to make missing comparisons visible. An interruption or setup failure can leave an empty output file. Use a new path for each repetition and review failures rather than silently discarding them.
6. Review per-case rankings, improvements, regressions, failures and `null` usage; record corpus/commit and environment metadata alongside the protected report. The output intentionally excludes query text, content and bearer tokens, but document/chunk IDs are still potentially sensitive. Do not commit raw reports without reviewing their data policy. Remove the token from the process environment after use.

A live run requires an available API, corpus and Foundry access; the existence of this runner is not a live evaluation result.
