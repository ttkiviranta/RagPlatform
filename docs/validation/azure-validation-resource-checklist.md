# Azure Validation Resource Checklist — Phase 12 and Phase 13

**Assessment date:** 2026-11-16  
**Scope:** Repository documentation and checked application configuration. This is not a live Azure subscription inventory; no Azure Resource Graph query or portal inspection was performed. Resource presence must be confirmed in the target non-production subscription before validation.

## Readiness summary

The documentation describes the Azure services required for acceptance, but does not identify a current subscription, resource group, or complete set of resource names/endpoints. Historical integration checks show that some services were used during earlier phases; that does not prove that the same resources remain provisioned or are ready for Phase 12–13 validation.

| Resource | Phase 12 | Phase 13 | Repository evidence / current assessment |
| --- | --- | --- | --- |
| Azure AI Foundry | Required for live embedding generation in the import flow | Required for embedding and chat inference | Phase 11 records successful historical Foundry integration. No current resource name, endpoint, or deployment names are recorded; checked `appsettings.json` has empty endpoint/deployment values. **Historically used; current validation resource unverified.** |
| Azure Blob Storage | Required for private temporary PDF staging and cleanup | Not required for standalone Q&A over an existing corpus; required for combined upload-to-answer validation | Logical container `document-staging` is documented; actual storage account and service URI are not. A prior upload/message-publish check is recorded but does not identify a current account. **Current account/container unverified.** |
| Azure Service Bus | Required for the isolated import queue, worker processing, settlement, retry, and DLQ validation | Not required for standalone Q&A; required for combined ingestion-to-answer validation | Functional processing is verified against the current environment, but formal isolated settlement/retry/DLQ acceptance, dedicated-resource isolation, and least-privilege identity evidence remain open. Worker defaults to disabled. |
| Application Insights | Required for worker operational evidence, or an explicitly approved equivalent telemetry target | Required for retrieval/answer metrics, failure and abstention monitoring, privacy, and performance baselines | Telemetry code/package is present, but no Application Insights resource name or current connection is recorded; checked `appsettings.json` has an empty connection string. **Resource connectivity and alerting unverified.** |
| Microsoft Entra ID | Required for API users and least-privilege service identities for Blob, Service Bus, and Foundry | Required for API authorization tests and Foundry inference identity | Phase 10 authentication and Phase 11 Foundry authentication are documented as validated; non-secret tenant/client configuration values exist. Current app registration, scopes/roles, test users, managed identity/service principal, and Azure role assignments are not confirmed. **Historically integrated; current validation identities/permissions unverified.** |
| PostgreSQL + pgvector | Required for fresh/upgrade migrations and import persistence tests | Required for vector retrieval, authorization, conversations, and citation persistence | Local Docker PostgreSQL 16.15 and pgvector 0.8.7 were verified. The existing `ragplatform` database is non-fresh and is at 14 of 16 migrations. **Local prerequisite exists; dedicated disposable validation databases are not evidenced.** An Azure-managed PostgreSQL resource is not documented or required if an isolated supported PostgreSQL instance is available. |

## Resources required by phase

### Phase 12 — PDF import and worker validation

- **PostgreSQL with pgvector:** isolated empty database and a separate database at the supported prior migration point for PG-01; disposable database for persistence, concurrency, and idempotency tests.
- **Azure Blob Storage:** private staging container, documented logical name `document-staging`; the uploader writes staged PDFs and the worker reads/deletes them according to the import lifecycle.
- **Azure Service Bus:** dedicated non-production namespace and import queue, logical name `document-import`, with dead-letter access and controlled delivery/lock settings.
- **Microsoft Entra ID:** API test users and the least-privilege workload identity used by the API/worker to reach Azure data-plane resources.
- **Azure AI Foundry:** non-production embedding deployment with a known model and vector dimension; the configured dimension must match persisted vectors.
- **Application Insights or approved telemetry equivalent:** observable processing outcomes, queue age, failures, retries, and DLQ behavior without PDF content or credentials in logs.

### Phase 13 — Document Q&A validation

- **PostgreSQL with pgvector:** isolated database with all current migrations applied; seeded synthetic vectors/documents for ranking and owner/tenant authorization tests; conversation and citation-snapshot persistence/deletion tests.
- **Azure AI Foundry:** non-production embedding and chat deployments, Entra-backed inference, recorded model/deployment versions, request limits, and timeout/token settings.
- **Microsoft Entra ID:** at least two tenants, multiple users per tenant, and the supported tenant-admin role where applicable; test API authorization and Foundry inference identity.
- **Application Insights or approved telemetry equivalent:** retrieval/answer duration, outcome, token usage when available, failures, abstentions, and alert evidence; verify prompts, excerpts, answers, and secrets are excluded.
- **Blob Storage and Service Bus:** needed only when Phase 13 tests are combined with the Phase 12 PDF-ingestion journey. They are not dependencies for isolated Q&A over an already-imported synthetic corpus.

## Resource checklist

Record non-secret resource aliases/IDs, environment, region, owner, validation run ID, and evidence location. Do not copy connection strings, keys, tokens, prompts, document contents, or answers into this checklist.

### 0. Test environment and isolation

- [ ] Select the non-production subscription, resource group, region, and environment owner.
- [ ] Confirm all test resources are isolated from production data, identities, queues, and telemetry.
- [ ] Identify an approved .NET execution host. Keep Smart App Control enabled on the current workstation; do not use that workstation to execute blocked application/test hosts.
- [ ] Assign cleanup and rollback owners; define a retention/cleanup date for disposable resources.
- [ ] Record the application commit, migration chain, resource aliases, and test-data version.

### 1. Azure AI Foundry

**Needed:** Phase 12 live embeddings; Phase 13 live embeddings and chat.

- [ ] Confirm the current non-production Foundry resource exists and record its alias, resource ID, region, and endpoint (no credentials).
- [ ] Confirm the embedding and chat deployments exist, are enabled, and are approved for validation; record deployment aliases and model/version.
- [ ] Confirm embedding model/dimension compatibility with application configuration and persisted data. The documented default of 1536 is not proof of the current deployment dimension.
- [ ] Verify the test workload identity has the required inference/data-plane access at the narrowest applicable scope (the phase documentation names `Foundry User` and `Cognitive Services User`; confirm the role actually required for the selected resource/API).
- [ ] Verify quota/rate limits, timeout/output-token settings, and test request budget.
- [ ] Capture a sanitized successful embedding and chat smoke-test outcome from an approved environment, including deployment alias, dimension/usage metadata, time, and duration.
- [ ] Confirm prompts, source excerpts, responses, and credentials are not persisted to diagnostic logs unless explicitly approved.

**Current evidence:** Historical Phase 11 inference validation only. Current endpoint/deployments and permissions are not established in repository documentation.

### 2. Blob Storage

**Needed:** Phase 12 PDF staging and lifecycle checks; combined Phase 12–13 journey.

- [ ] Confirm the non-production Storage Account and private staging container exist; record aliases and region. `document-staging` is a logical configured name, not a confirmed live container.
- [ ] Confirm the configured HTTPS Blob service URI resolves to the intended account and container.
- [ ] Verify public access is disabled and network/access restrictions match the test design.
- [ ] Verify the API/worker identity has only the required blob data-plane permissions for staging operations (write/read/delete as implemented).
- [ ] Verify lifecycle/cleanup handling for successful, failed, and abandoned imports; Phase 12 documentation notes failed/abandoned staging-Blob cleanup remains an operational limitation.
- [ ] In an isolated test, record sanitized Blob creation/read/deletion outcomes and correlated document/message IDs; do not retain PDF content as evidence.

**Current evidence:** Logical container name and architecture are documented; actual account, endpoint, container existence, access policy, and lifecycle configuration are unverified.

### 3. Service Bus

**Needed:** Phase 12 message publish, processing, settlement, retry, and dead-letter validation; combined ingestion flow.

- [ ] Confirm a dedicated non-production namespace and queue exist; record aliases, FQ namespace, region, and queue. `document-import` is a logical name only.
- [ ] Confirm DLQ is accessible and monitored; record baseline active, scheduled, and dead-letter counts.
- [ ] Record lock duration, maximum delivery count, TTL, duplicate-detection settings if used, and settlement configuration.
- [ ] Verify least-privilege identity permissions: sender access for the publishing API and receiver/settlement access for the processor, scoped to test resources.
- [ ] Confirm worker is disabled outside the isolated environment and can be enabled only for the test deployment. The repository default is `ServiceBus:DocumentImportWorkerEnabled=false`.
- [ ] Verify processor settings match the intended run (manual settlement; documented `AutoCompleteMessages=false`, `MaxConcurrentCalls=1`, `PrefetchCount=0`).
- [ ] Configure queue age, delivery-count, processing-failure, and DLQ monitoring/alerts; capture a harmless test event only after the isolated test gate is approved.
- [ ] Do not use shared or production queues for retry, poison-message, lock-expiry, or settlement-interruption testing.

**Current evidence:** Functional PDF upload and worker processing evidence exists; queue, namespace, settlement/retry/DLQ behavior, dedicated-resource isolation, role assignments, monitoring, and formal rollback evidence remain to be recorded.

### 4. Application Insights / telemetry

**Needed:** Phase 12 worker operations; Phase 13 retrieval/answer evaluation, diagnostics, and privacy validation.

- [ ] Confirm Application Insights resource exists, or document the explicitly approved telemetry equivalent and its owner.
- [ ] Confirm the test application has the correct connection/configuration through an approved secret/configuration mechanism. The checked repository `appsettings.json` value is empty; do not commit a connection string.
- [ ] Verify expected events/metrics are queryable for success, failure, retry, abstention, retrieval, generation, and latency.
- [ ] Configure/review alerts for processing failures, queue age/DLQ, error/abstention changes, and agreed latency/cost thresholds.
- [ ] Inspect sampled logs/telemetry to verify prompts, document text/excerpts, answers, tokens, and secrets are not collected.
- [ ] Record sanitized query names, metric dimensions, alert results, and p50/p95/p99 baselines without exporting sensitive content.

**Current evidence:** Telemetry integration is implemented/documented; no live Application Insights resource or connection is verified.

### 5. Microsoft Entra ID

**Needed:** Authenticated API validation, tenant/owner authorization, and workload identity access to Azure resources.

- [ ] Confirm the tenant and API app registration referenced by the application are active and belong to the selected non-production environment. Do not copy IDs or secrets into this checklist.
- [ ] Verify API audience, scopes/app roles, token issuer, and required claims against the currently deployed API configuration.
- [ ] Create or confirm synthetic test identities for at least two tenants and multiple users per tenant, including only supported administrator roles.
- [ ] Verify workload identity selection (`DefaultAzureCredential`/managed identity or approved service principal) for the actual host; ensure secrets are not committed or placed in logs.
- [ ] Verify resource-scoped role assignments for Foundry inference, Blob data access, Service Bus send/receive, PostgreSQL access if identity-based, and telemetry access where applicable.
- [ ] Capture positive and negative authorization outcomes without storing access tokens or user personal data.

**Current evidence:** Historical Entra/API and Foundry authentication validation plus non-secret tenant/client configuration values. Current app registration state, test users, scopes, workload identity, and resource role assignments are not documented.

### 6. PostgreSQL and pgvector

**Needed:** PG-01 through PG-05 and all database-backed Phase 12/13 tests.

- [x] Local PostgreSQL/pgvector prerequisite previously verified: PostgreSQL 16.15 and pgvector 0.8.7.
- [ ] Provision or identify a disposable test instance with the supported PostgreSQL version and `vector` extension enabled.
- [ ] Create separate disposable targets for the empty-database migration and prior-schema upgrade paths; never reset the existing `ragplatform` database.
- [ ] Record `__EFMigrationsHistory` and confirm all expected migrations after PG-01. The existing database is at 14 of 16 migrations and is not the latest target.
- [ ] Verify required tables, vector dimensions/indexes, foreign keys, constraints, and query support using the real Npgsql/EF model.
- [ ] Seed only synthetic owners, tenants, documents, chunks, and deterministic vectors; capture IDs/counts and expected ranking/authorization outcomes, not contents or vector payloads.
- [ ] Capture migration output, extension/version, schema summary, transaction/locking/idempotency results, and sanitized SQL/query evidence.

**Current evidence:** Local prerequisite instance exists and was inspected. The PG-01 fresh/upgrade tests are blocked by workstation Smart App Control; disposable migration targets and a completed migration run are not evidenced. An Azure-managed PostgreSQL resource is not documented and is optional if a suitable isolated PostgreSQL instance is provided.

## Readiness decision and next action

**Current Azure validation readiness: not established.** The repository proves implementation and historical use for some integrations, but does not prove current provisioning, isolation, credentials/identity assignments, deployment availability, monitoring, or test-resource readiness. Treat every resource without current inventory evidence above as **unverified**, not as provisioned.

Next, perform a read-only inventory in the intended non-production subscription and complete checklist sections 0–6 with sanitized evidence. Keep the current workstation's security settings unchanged. Then use an approved .NET execution environment and isolated resources to run PG-01 before database-backed Phase 12/13 acceptance.
