# RAG Platform 2.0

A backend-first document ingestion and retrieval-augmented question-answering platform built with ASP.NET Core, PostgreSQL, pgvector, Microsoft Entra ID, and Azure AI Foundry.

## Current Development Status

- ✅ **Phases 1–11 completed and validated.** Validation depth and archived evidence vary by phase; see the [phase archive](docs/README.md).
- ✅ **Phase 12 implemented and documented.**
- ✅ **Phase 12 functional validation:** PDF ingestion, Service Bus processing, PostgreSQL/pgvector integration, Foundry embeddings, vector retrieval, full-text retrieval, hybrid retrieval, and semantic retrieval have been validated against the evaluation corpus.
- ⚠️ **Phase 12 rollout acceptance pending.** The functional results do not establish all failure, recovery, isolation, and operational gates. The import worker remains disabled by default and outside approved validation environments until rollout gates pass.
- ✅ **Phase 13 implemented and documented.**
- ⚠️ **Phase 13 acceptance validation pending.** Database, live Foundry, quality/security, performance/cost, retention, and rollout evidence remain open.

Phase 11 validation refers to the documented Azure AI Foundry inference integration; it does not imply Phase 12 or Phase 13 rollout acceptance.

Current capabilities include:

- PDF ingestion with temporary Azure Blob Storage staging and Service Bus processing
- PDF text extraction, chunk generation, and embedding generation
- PostgreSQL/pgvector storage, vector similarity retrieval, full-text retrieval, and Reciprocal Rank Fusion hybrid retrieval
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
- Previously reported baseline: 62 tests, before the search endpoint tests.

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
- ✅ Previously reported unit test baseline: 62/62 passed, before the search endpoint tests
- ✅ PostgreSQL integration tests passing (67/67)
- ✅ EF Core migrations validated in CI
- ✅ pgvector integration validated in CI

The previously reported total was 129 automated tests before the search endpoint tests.

## Phase 12 Validation Results

### Validated Capabilities

- PDF upload
- Service Bus message publishing
- Document Import Background Worker
- PDF text extraction
- Chunk generation
- Azure AI Foundry embedding generation
- PostgreSQL + pgvector storage
- Vector similarity search
- Full-text search
- Hybrid search with Reciprocal Rank Fusion
- Semantic retrieval

### Evaluation Corpus

- identity-security.pdf
- azure-servicebus.pdf
- postgresql-pgvector.pdf
- employment-policy.pdf
- hybrid-search-demo.pdf

### Validation Results

| Query | Expected Document | Result |
|---------|---------|---------|
| dead-letter queue | azure-servicebus | PASS |
| OAuth 2.0 | identity-security | PASS |
| HNSW | postgresql-pgvector | PASS |
| notice period | employment-policy | PASS |
| reciprocal rank fusion | hybrid-search-demo | PASS |
| how does the system stop one organization from reading another organization's files? | identity-security | PASS |

**Additional full-text validation**

| Query | Expected Document | Result |
|---------|---------|---------|
| OAuth 2.0 | identity-security | PASS |
| dead-letter queue | azure-servicebus | PASS |

**Additional hybrid validation**

- `dead-letter queue` correctly merged vector and full-text results for `azure-servicebus`.
- The semantic authorization question returned `identity-security`.
- `FoundByVectorSearch` and `FoundByFullTextSearch` were both verified for the overlap result.

### Notes

- All five evaluation PDFs successfully reached Completed status.
- Document chunk generation succeeded.
- Embeddings were generated using Azure AI Foundry deployment text-embedding-ada-002.
- Vector search returned the expected document as the highest-ranked result for all validation queries.
- Full-text search returned the expected document for exact/keyword-oriented queries.
- Hybrid search fused overlapping vector and full-text candidates using the existing Reciprocal Rank Fusion implementation.
- Semantic retrieval successfully matched natural-language questions to relevant content without relying solely on exact keywords.
- copilot.pdf failed extraction with NoExtractableText and was dead-lettered, which is expected behavior for a permanent extraction failure.

### Conclusion

The end-to-end ingestion, embedding, vector retrieval, full-text retrieval, hybrid retrieval, and semantic retrieval paths were successfully validated against the evaluation corpus. This demonstrates the tested functional scenarios, not every rollout or recovery acceptance scenario.

### Remaining Phase 12 Acceptance Evidence

The following criteria remain open unless supported by separately reviewed evidence:

- Fresh and supported upgrade migration paths, plus coverage of transaction atomicity, completion evidence, row locking, model/dimension mismatches, and concurrent processing.
- The remaining acceptance trace on confirmed isolated resources must cover correlation/audit/settlement evidence, staging-Blob deletion, and repository-preserved evidence beyond the successful functional run.
- Completed-message redelivery, transient retry, poison-envelope dead-lettering, and interruption between database completion and broker settlement, with no duplicate artifacts.
- Full permanent-PDF failure evidence, including persisted Failed status and audit. The observed `copilot.pdf` dead-letter result covers part of this scenario.
- Verified import-status responses for both completion and failure.
- Dedicated test resources and least-privilege identity permissions; demonstrated queue/DLQ monitoring, alerts, and content-safe logs.
- An approved approach for failed or abandoned staging Blobs, an exercised disable/rollback procedure, and rollout-owner approval.

Record sanitized evidence and review results against the [Phase 12 acceptance plan](docs/phases/phase12/acceptance-validation-plan.md). Older validation reports may not yet reflect the corpus results above.

## API surface

The API includes document CRUD and upload, URL import, document processing/metadata, chunks and embeddings, vector search, RAG query, conversations/messages, and document import status. OpenAPI/Swagger is the interactive API reference when the service is running.

Document create and update requests and web imports accept an optional `tags` array. Text and PDF multipart uploads accept repeated `tags` form fields. Names are trimmed, case-sensitive, 1–100 characters without control characters, and unique per document after trimming; at most 50 tags are allowed. On update, an omitted or `null` value preserves tags and an empty array clears them. Document responses contain a sorted `tags` array; PDF upload and import-status responses keep their status-only format, so use document GET to read tags. Deleting a document deletes its tags. Tags cannot yet be used to filter retrieval.

Search endpoints include `POST /api/search/vector`, `POST /api/search/fulltext`, and `POST /api/search/hybrid`. Each accepts `query`, `topK` (1–50, default 5), and an optional `documentId`. Full-text and hybrid searches return arrays of ranked chunks; hybrid results indicate which search sources found each chunk. Hybrid content is a vector snippet for vector-only results and full chunk content for full-text matches.

RAG retrieval is controlled by `Rag:UseHybridSearch`. It defaults to `false`, preserving vector-only RAG behavior. When enabled, RAG uses hybrid RRF retrieval; hybrid exceptions or empty results fall back to vector retrieval. Request cancellation is always propagated without fallback.

## Roadmap

### Completed

- Vector Search (pgvector)
- PostgreSQL Full-Text Search
- FullTextSearchService
- HybridSearchService
- Full-text and hybrid search APIs
- Reciprocal Rank Fusion (RRF)
- RAG retrieval backed by Hybrid Search (optional; vector remains the default)
- Document Ingestion Pipeline
- Semantic Search
- Citations
- Conversation History
- PostgreSQL Integration Testing
- GitHub Actions CI/CD

### In Progress

- Document tags: persistence and API management are implemented; tag-filtered retrieval remains to be implemented.

### Planned

- Reranking
- Search quality evaluation
- Additional retrieval strategies

## Next steps

Continue the document-tag learning feature with retrieval filtering. This work does not change Phase 12 or Phase 13 acceptance status.

Complete and record the remaining Phase 12 recovery, isolation, evidence, and operational rollout gates, then complete Phase 13 database, live Foundry, answer-quality, security, and operational acceptance. The previously identified Phase 14–17 themes (Copilot Studio integration, GraphRAG, administration, and AI engineering/operations) remain roadmap items rather than current implementation scope; the proposed sequencing below is not yet approved.

### Proposed roadmap alignment (not yet approved)

The [Phase 14 proposal](docs/phases/phase14/proposal.md) recommends dedicating Phase 14 to **Adaptive Retrieval and Fallback Strategy**: scoped File First retrieval, corpus-wide Vector RAG, and policy-controlled Model Knowledge/Web Search fallback with explicit provenance. This is a proposed re-sequencing, not an approved change to project scope or implementation status.

To preserve every concept in the earlier roadmap while adding this capability, the proposed sequence is:

- **Phase 14 — Adaptive Retrieval and Fallback Strategy**
- **Phase 15 — Copilot Studio Integration** (moved from the earlier Phase 14 position)
- **Phase 16 — GraphRAG**
- **Phase 17 — Administration**
- **Phase 18 — AI Engineering and Operations**

This proposal adds Phase 18 rather than dropping or combining the previously listed roadmap themes. Phase 12/13 acceptance remains the prerequisite to implementation and enablement. See the [documentation index](docs/README.md) for the proposal and its roadmap status.
