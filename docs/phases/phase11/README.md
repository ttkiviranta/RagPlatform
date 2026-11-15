# Phase 11 - Azure AI Foundry Integration

## Implementation summary

Phase 11 connects the existing embedding, vector search, and RAG pipeline to Azure AI Foundry model deployments. Commit: `a2afd03`; migration: `20261110125525_Phase11FoundryInvocationAudit`.

## Architecture decisions

- RagPlatform owns document records, chunks, embeddings, vector search, and audit data. Foundry is an inference boundary, not document or vector storage.
- Use `DefaultAzureCredential` and Microsoft Entra authentication; no Foundry API key is stored in application configuration.
- Filter search to the configured embedding deployment to avoid comparing incompatible embedding spaces.
- Keep prompt, query, document content, conversation messages, and model response content out of audit events, telemetry, and application logs.
- Use existing ownership/tenant scoping for retrieval and owner-scoped conversation history.
- Agents, tool calling, Copilot Studio, and Azure AI Search were explicitly outside Phase 11 scope.

## Configuration and Azure resource references

The README documents `Foundry__Endpoint`, `Foundry__ChatDeployment`, `Foundry__EmbeddingDeployment`, `Foundry__EmbeddingDimensions` (default `1536`), and `Foundry__MaxOutputTokens` (default `800`). The endpoint is documented as an HTTPS Foundry OpenAI v1 endpoint ending in `/openai/v1`.

Resource-scoped `Foundry User` and `Cognitive Services User` roles are named for project/development access and inference data-plane access. No actual Foundry resource name, endpoint, or deployment name is recorded in the repository. Historical validation names in the README are `text-embedding-ada-002` and `gpt-35-turbo`; treat these as historical test deployments, not current model recommendations.

## Validation evidence

The README marks Phase 11 validated: Foundry Entra authentication, 1536-dimensional embedding generation and persistence, chunk-to-embedding-to-vector flow, grounded chat with source identifiers, scoped retrieval, optional owner-scoped conversation context, and operational-only audit events.

## Limitations and missing records

No standalone model comparison, latency/cost evaluation, production-readiness report, or retrospective was found. Phase 11 validates inference integration, not the later Phase 12 asynchronous import rollout or the complete Phase 13 Q&A acceptance criteria.