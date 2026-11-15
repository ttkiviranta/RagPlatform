# Phase 13 Increment 6 - Quality and Hardening

## Scope

This increment adds retrieval-quality and answer-generation telemetry, structured outcome logging, and deterministic tests for prompt framing and evidence abstention. It does not change authorization, retrieval or citation API contracts, conversation ownership, or ingestion.

## Telemetry

`VectorSearch` now reports:

- `VectorSearchDurationMs`: end-to-end search duration, including query embedding, authorized candidate count, and pgvector ranking.
- `CandidateChunkCount`: eligible chunks matching the embedding model/dimension, tenant/owner scope, and optional document scope before Top-K.
- `ReturnedChunkCount` (also retained as `ResultCount`): ranked chunks returned to the caller.
- `AverageSimilarity`, `TopK`, `ModelName`, `Success`, and `FailureType` when retrieval fails.

Candidate count is obtained with a count query using the same authorized, model-compatible query before the ranking query. This adds database work; monitor `VectorSearchDurationMs` and query cost while validating the feature.

`AnswerGeneration` reports:

- `AnswerGenerationDurationMs`, `AnswerReturnedChunkCount`, and validated `AnswerCitationCount`.
- `Provider` and `Deployment` dimensions identifying the answer source.
- `AnswerInputTokenCount` and `AnswerOutputTokenCount` only when Foundry returns usage values.
- `Outcome`: `Succeeded`, `NoContext`, `InsufficientEvidence`, `GenerationFailed`, or `CitationValidationFailed`, plus `Success` and a sanitized exception type for failures.

Retrieval failures are emitted by the vector-search stage. Foundry invocation failures and citation-validation failures are recorded by answer generation. A no-context or insufficient-evidence response is an expected successful abstention and is distinguished from a generated answer by `Outcome`.

Telemetry contains counts, timings, model/deployment labels, and exception type only; it does not include user questions, prompts, retrieved text, source names, or generated answer content.

## Safety tests

`RagQualityHardeningTests` covers:

- Retrieved prompt-injection text remains in the explicitly untrusted context while the system instruction requires ignoring commands; prior assistant answers are not passed as conversation evidence.
- Empty/insufficient context returns the explicit fallback without making a Foundry call.
- A weak-evidence fallback is returned without citations and is counted as `InsufficientEvidence`.
- Foundry failures and unsupported citation labels emit distinct failure outcomes and retain token usage when available.

These tests verify deterministic prompt construction, service control flow, and telemetry classification. They do not prove that a live model will resist every prompt injection or that a citation semantically entails a claim; adversarial model evaluation remains required.

## Operational metrics and logging guidance

Use `VectorSearchDurationMs` and `AnswerGenerationDurationMs` percentiles (especially p95/p99), stage success rates, candidate-to-returned counts, average similarity, abstention outcomes, citations per answer, and token counts grouped by deployment. Establish a representative baseline before setting alert thresholds; alert on sustained latency or failure-rate regressions, unexpected abstention/citation shifts, and token/cost growth.

Example Application Insights queries:

```kusto
customMetrics
| where name in ("VectorSearchDurationMs", "AnswerGenerationDurationMs")
| summarize p50=percentile(value, 50), p95=percentile(value, 95), p99=percentile(value, 99) by name, bin(timestamp, 5m)
```

```kusto
customEvents
| where name == "AnswerGeneration"
| summarize Requests=count(), Failures=countif(tostring(customDimensions.Success) == "False"),
	Abstentions=countif(tostring(customDimensions.Outcome) == "InsufficientEvidence")
  by Deployment=tostring(customDimensions.Deployment), bin(timestamp, 15m)
```

Keep structured logs limited to stage, outcome, duration, counts, deployment, and correlation identifiers. Never log query/prompt content, retrieved excerpts, or model answers. Use failure type and correlation IDs to locate traces without putting sensitive payloads into dimensions.

## Validation and limitations

The full solution build succeeds, and all five focused `RagQualityHardeningTests` pass. PostgreSQL/pgvector, live Foundry, prompt-injection behavioral evaluation, and latency/token-cost baselines remain unvalidated.
