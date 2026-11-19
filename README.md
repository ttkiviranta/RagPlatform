# RAG Platform 2.0

A backend-first document ingestion and retrieval-augmented question-answering platform built with ASP.NET Core, PostgreSQL, pgvector, Microsoft Entra ID, and Azure AI Foundry.

## Current Development Status

- ✅ **Phases 1–11 completed and validated.** Validation depth and archived evidence vary by phase; see the [phase archive](docs/README.md).
- ✅ **Phase 12 implemented and documented.**
- ⚠️ **Phase 12 acceptance validation pending.** PostgreSQL/pgvector integration and isolated Service Bus end-to-end validation remain open. The import worker stays disabled until rollout gates pass.
- ✅ **Phase 13 implemented and documented.**
- ⚠️ **Phase 13 acceptance validation pending.** Database, live Foundry, quality/security, performance/cost, retention, and rollout evidence remain open.

Phase 11 validation refers to the documented Azure AI Foundry inference integration; it does not imply Phase 12 or Phase 13 rollout acceptance.

Current capabilities include:

- PDF ingestion with temporary Azure Blob Storage staging and Service Bus processing
- PDF text extraction, chunk generation, and embedding generation
- PostgreSQL/pgvector storage and similarity retrieval
- RAG question answering through Azure AI Foundry
- Server-validated citations and owner/tenant-scoped conversation history
- Microsoft Entra ID authentication, application authorization, and durable audit events
- Application Insights/OpenTelemetry instrumentation and RAG/import telemetry

Implementation completion does not imply production readiness. See the [combined Phase 12–13 validation roadmap](docs/validation/phase12-phase13-validation-roadmap.md) for the remaining gates and recommended execution order.

## Documentation

Detailed phase material, completion reports, architecture notes, rollout information, and acceptance plans are maintained in `docs/` rather than duplicated in this landing page. Available phase reports and plans are linked from their phase folders.

- [Documentation index](docs/README.md)
- [Phase 12 documentation](docs/phases/phase12/)
  - [Completion report](docs/phases/phase12/completion-report.md)
  - [Acceptance validation plan](docs/phases/phase12/acceptance-validation-plan.md)
- [Phase 13 documentation](docs/phases/phase13/)
  - [Completion report](docs/phases/phase13/completion-report.md)
  - [Acceptance validation plan](docs/phases/phase13/acceptance-validation-plan.md)
- [Phase 14 proposal](docs/phases/phase14/proposal.md)
  - [Implementation plan](docs/phases/phase14/implementation-plan.md)
- [Validation documentation](docs/validation/)
- [Reports](docs/reports/)

## Project Documentation Map

```text
docs/
├─ architecture/   # Cross-phase architecture decisions
├─ phases/         # Phase summaries, implementation notes, reports, and plans
├─ reports/        # Consolidated project status and reports
├─ rollout/        # Operational rollout gates
└─ validation/     # Acceptance evidence, gaps, and combined roadmaps
```

## Architecture at a glance

RagPlatform owns document records, extracted text, chunks, embeddings, retrieval, conversations, and audit data. Azure services provide temporary PDF staging, asynchronous message transport, and model inference; they are not the platform's document or vector database.

```text
PDF upload
  → Blob staging and Service Bus message
  → API-hosted import worker
  → PDF extraction and chunking
  → Azure AI Foundry embeddings
  → PostgreSQL / pgvector

Question
  → authorized vector retrieval
  → bounded evidence context
  → Azure AI Foundry answer generation
  → server-validated citations
  → optional owner-scoped conversation persistence
```

The import worker runs in `RagPlatform.Api` and is opt-in. It is disabled by default pending Phase 12 acceptance. Retrieval and authorization are application-controlled; model output is not an authorization boundary. Conversation history is owner- and tenant-scoped, bounded, and not treated as evidence for later answers.

### Hybrid Search

```text
Query
  ↓
VectorSearchService
  +
FullTextSearchService
  ↓
HybridSearchService
  ↓
Results
```

## Technology

- ASP.NET Core 10 / C#
- PostgreSQL, Entity Framework Core, Npgsql, and pgvector
- Microsoft Entra ID and JWT bearer authentication
- Azure AI Foundry for embedding and chat inference
- Azure Blob Storage and Azure Service Bus for the Phase 12 import flow
- Azure Application Insights and OpenTelemetry
- Swagger / OpenAPI

## Local development

### Prerequisites

- .NET 10 SDK
- Docker (for the included PostgreSQL/pgvector service), or a compatible PostgreSQL instance with pgvector
- Azure identity/configuration for features that call Azure services

The repository pins .NET SDK 10.0.100 in `global.json` and the EF Core CLI in `.config/dotnet-tools.json`. A local machine without SDK 10 can roll forward to an installed newer SDK; CI installs the pinned SDK. Restore the local tool manifest before running EF commands.

### Start PostgreSQL and run the API

```bash
docker compose up -d postgres
dotnet restore
dotnet tool restore
dotnet ef database update
dotnet run --project RagPlatform.Api
```

Use the Swagger URL printed by the application. Configure connection strings and Azure settings through .NET User Secrets or environment variables; do not commit credentials. Authenticated API workflows require a configured Microsoft Entra ID application. Foundry calls use the configured Entra-backed identity; no model API key should be committed.

Build and run provider-independent tests with:

```bash
dotnet build RagPlatform.slnx
dotnet test RagPlatform.Api.Tests/RagPlatform.Api.Tests.csproj --filter "TestCategory=Unit"
```

Database-backed tests are categorized as `PostgreSql` and require a disposable PostgreSQL instance with pgvector. Configure `ConnectionStrings__DefaultConnection` for migrations and set `RAGPLATFORM_TEST_POSTGRES_CONNECTION` to the same disposable test database, then apply migrations with `dotnet ef database update` and run:

```bash
dotnet test RagPlatform.Api.Tests/RagPlatform.Api.Tests.csproj --filter "TestCategory=PostgreSql"
```

CI runs the unit and PostgreSQL test categories in separate workflows. Live Foundry and Service Bus behaviors require isolated Azure resources; see the [validation roadmap](docs/validation/phase12-phase13-validation-roadmap.md).

## Testing and CI

### Unit Tests

- Executed in GitHub Actions on every push and pull request.
- Category: `Unit`
- Current count: 62 tests

```bash
dotnet test --filter "TestCategory=Unit"
```

### PostgreSQL Integration Tests

- Executed in a dedicated GitHub Actions workflow.
- Uses PostgreSQL with pgvector.
- Applies EF Core migrations automatically.
- Category: `PostgreSql`
- Current count: 67 tests

```bash
dotnet test --filter "TestCategory=PostgreSql"
```

### CI Workflow

- Build the application.
- Execute unit tests.
- Provision PostgreSQL/pgvector.
- Apply EF Core migrations.
- Execute integration tests.
- Publish TRX artifacts.

### Current Status

- ✅ GitHub Actions CI enabled
- ✅ Unit tests passing (62/62)
- ✅ PostgreSQL integration tests passing (67/67)
- ✅ EF Core migrations validated in CI
- ✅ pgvector integration validated in CI

**Total automated tests: 129**

## API surface

The API includes document CRUD and upload, URL import, document processing/metadata, chunks and embeddings, vector search, RAG query, conversations/messages, and document import status. OpenAPI/Swagger is the interactive API reference when the service is running.

## Roadmap

### Completed

- Vector Search (pgvector)
- PostgreSQL Full-Text Search
- FullTextSearchService
- HybridSearchService
- Reciprocal Rank Fusion (RRF)
- Document Ingestion Pipeline
- Semantic Search
- Citations
- Conversation History
- PostgreSQL Integration Testing
- GitHub Actions CI/CD

### In Progress

- None

### Planned

- Hybrid Search API
- RAG retrieval backed by Hybrid Search
- Reranking
- Search quality evaluation
- Additional retrieval strategies

## Next steps

Complete and record the Phase 12 PostgreSQL/pgvector and isolated Service Bus rollout gates, then complete Phase 13 database, live Foundry, answer-quality, security, and operational acceptance. The previously identified Phase 14–17 themes (Copilot Studio integration, GraphRAG, administration, and AI engineering/operations) remain roadmap items rather than current implementation scope; the proposed sequencing below is not yet approved.

### Proposed roadmap alignment (not yet approved)

The [Phase 14 proposal](docs/phases/phase14/proposal.md) recommends dedicating Phase 14 to **Adaptive Retrieval and Fallback Strategy**: scoped File First retrieval, corpus-wide Vector RAG, and policy-controlled Model Knowledge/Web Search fallback with explicit provenance. This is a proposed re-sequencing, not an approved change to project scope or implementation status.

To preserve every concept in the earlier roadmap while adding this capability, the proposed sequence is:

- **Phase 14 — Adaptive Retrieval and Fallback Strategy**
- **Phase 15 — Copilot Studio Integration** (moved from the earlier Phase 14 position)
- **Phase 16 — GraphRAG**
- **Phase 17 — Administration**
- **Phase 18 — AI Engineering and Operations**

This proposal adds Phase 18 rather than dropping or combining the previously listed roadmap themes. Phase 12/13 acceptance remains the prerequisite to implementation and enablement. See the [documentation index](docs/README.md) for the proposal and its roadmap status.
