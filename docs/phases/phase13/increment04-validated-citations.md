# Phase 13 Increment 4 - Validated Citations

## Scope

This increment adds server-assigned citation labels to retrieved evidence, validates the labels returned in the Foundry answer, and returns `Answer` with validated citation metadata. It does not add conversation persistence or change ingestion components.

## Citation source construction

`RagCitationService` reloads the retrieved chunk IDs and their document metadata from PostgreSQL. The database query applies `DocumentAccessService.ScopeDocuments` in the same query, so tenant, ownership, and existing role-based access are checked again before text or metadata is used. It verifies the persisted chunk-to-document relationship against the ranked search result, preserves the retrieval ranking order, and assigns contiguous citation numbers beginning at 1.

The model receives only numbered excerpts built from persisted chunk text. Source names are taken from persisted file name or title; document IDs and chunk IDs are copied from persisted records. The API does not parse or trust model-provided IDs, page numbers, URLs, filenames, or source metadata. `DocumentChunk` currently has no page-number field, so `PageNumber` is `null`.

Excerpts are whitespace/control-character normalized and capped at 500 characters. Source names are normalized and capped at 200 characters. Only sources whose labels were actually included in the bounded prompt and cited in the validated answer are returned.

## Prompt and label validation

The prompt directs Foundry to cite factual claims using only exact `[n]` labels supplied in the context, and not to produce source metadata. `FoundryAnswerGenerationService` captures numeric bracket references in the returned answer and rejects malformed labels, numbers not present in the prompt's supplied-label set, or non-fallback answers with no supplied citation label. The exact insufficient-evidence response is allowed without citations; adding text or citations to that response is rejected.

Rejected or malformed model responses fail the RAG request; they are not returned with success-shaped empty citations. Validation is syntactic label validation; it does not prove that the cited excerpt semantically entails every answer claim.

## Response contract

`RagQueryResponse` contains:

- `Answer`: the text returned by the answer-generation service.
- `Citations`: citation number, persisted document/chunk IDs, persisted source name when available, nullable page number, and safe excerpt.

The previous retrieval-score response remains available from the vector-search API; this RAG response no longer exposes internal retrieval results as source metadata.

## Validation and limitations

`RagPlatform.Api` builds successfully. PostgreSQL/pgvector and Foundry integration validation is still required to prove the whole query path. Current persisted chunk records do not contain page information. Semantic grounding, prompt injection resistance, and browser-side output encoding require separate evaluation.
