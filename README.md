# RAG Platform 2.0

A portfolio-grade .NET platform for building a retrieval-first, AI-enabled knowledge system. This project demonstrates disciplined backend engineering, phased delivery, and a practical path from document ingestion to vector-based retrieval, enterprise observability, and future AI orchestration.

## Project Overview

RAG Platform 2.0 is a modular ASP.NET Core API designed to manage the lifecycle of organizational knowledge from ingestion through preparation, retrieval, and future AI-powered answer generation. The project follows a milestone-driven roadmap so every phase remains focused, testable, and aligned with real engineering requirements.

This repository is intended to showcase:

- ASP.NET Core Web API architecture
- PostgreSQL persistence with Entity Framework Core
- Document lifecycle and metadata handling
- Web content ingestion and HTML normalization
- Content chunking for retrieval preparation
- Embedding persistence and vector-ready storage
- pgvector-powered retrieval foundations
- Azure Application Insights and OpenTelemetry readiness
- Secure local configuration via .NET User Secrets

The solution is built as a professional portfolio project for recruiters, architects, and senior engineers evaluating backend architecture, cloud-ready design, and staged AI platform delivery.

## Current Status

The project has completed the following phases:

### Completed phases

- ✅ Phase 1 - API Foundation
- ✅ Phase 2 - PostgreSQL Foundation
- ✅ Phase 3 - Document Management API
- ✅ Phase 4A - Document Upload Foundation
- ✅ Phase 4B - Document Processing Foundation
- ✅ Phase 5A - Web Content Ingestion
- ✅ Phase 5B - Chunking Foundation
- ✅ Phase 6 - Embeddings Foundation
- ✅ Phase 7 - Vector Search Foundation
- ✅ Phase 8 - Observability Foundation

### Current implementation status

The solution currently includes:

- ASP.NET Core Web API
- Swagger / OpenAPI
- PostgreSQL database integration
- Entity Framework Core migrations
- Health endpoint
- Document CRUD operations
- Document upload and validation
- Document processing and metadata extraction
- Web content import via URL
- Document chunk generation and persistence
- Embedding abstraction and storage
- pgvector-backed vector storage
- Similarity search foundation
- Azure Application Insights integration
- OpenTelemetry tracing and metrics
- User Secrets-based secure configuration

## Architecture Overview

The project follows a layered, modular design that keeps the system maintainable, testable, and ready for future enterprise AI features.

### Current architecture layers

- API layer: controllers for documents, processing, web ingestion, chunking, embeddings, search, and telemetry
- Application layer: service-first business logic for uploads, processing, chunk generation, vector retrieval, and monitoring
- Data layer: PostgreSQL + Entity Framework Core + pgvector
- Domain layer: document entities, metadata, chunk records, embedding records, and search contracts
- Integration layer: telemetry pipeline based on Azure Application Insights and OpenTelemetry
- Configuration layer: secure local configuration using .NET User Secrets

### Design principles

- Separation of concerns
- Dependency injection throughout the API
- Thin controllers and service-driven business logic
- Async/await-first implementation patterns
- EF Core-first persistence model
- Explicit abstraction for provider-agnostic embedding and retrieval work
- Clean evolution path toward semantic search and RAG
- Security-focused local configuration management

## Technology Stack

### Core platform

- ASP.NET Core 10
- C#
- PostgreSQL
- Entity Framework Core
- Npgsql
- pgvector
- Swagger / OpenAPI
- HtmlAgilityPack
- Azure Application Insights
- OpenTelemetry
- .NET User Secrets

### Current capabilities delivered

- Structured document management
- File upload processing
- HTML content import
- Metadata extraction and persistence
- Content chunk creation and storage
- Embedding persistence architecture
- Vector storage and retrieval foundation
- Telemetry and monitoring foundation
- Secure local secret configuration

## Implemented Features

### Document management

- Create, read, update, and delete documents
- Document metadata persistence
- Structured document storage in PostgreSQL
- Relationship mapping between documents and metadata

### Upload and processing

- File upload foundation for text-based document content
- Input validation and file metadata capture
- Content type and file size tracking
- Processing endpoints for derived document metadata

### Web content ingestion

- Accepts public URL input
- Downloads HTML content using HttpClientFactory
- Extracts page title and main content
- Removes boilerplate HTML and unnecessary page elements
- Stores source URL and document metadata
- Persists imported content as a document record

### Chunking foundation

- Creates chunk records for documents
- Supports configurable chunk size and overlap
- Preserves sentence boundaries when possible
- Stores generated chunk content and metadata
- Prevents duplicate chunk generation for the same document content set
- Associates chunks with their parent document

### Embeddings foundation

- Embedding metadata entity implemented
- Provider abstraction for future AI model providers
- Service and controller patterns for embedding workflows
- Database persistence for embedding metadata
- Vector-ready storage design

### Vector search foundation

- PostgreSQL pgvector integration
- EmbeddingVector storage in the database
- Similarity search readiness
- Top-K retrieval patterns
- Search result metadata and similarity scoring
- Search-specific telemetry integration

### Observability foundation

- Azure Application Insights integration
- OpenTelemetry tracing and metrics
- ASP.NET Core instrumentation
- HTTP instrumentation
- SQL instrumentation
- Structured logging readiness
- Monitoring hooks for API, database, embedding, and search telemetry

## API Capabilities

The current API exposes the following capabilities:

### Document APIs

- GET /api/documents
- GET /api/documents/{id}
- POST /api/documents
- PUT /api/documents/{id}
- DELETE /api/documents/{id}

### Upload and processing APIs

- POST /api/documents/upload
- POST /api/documents/{id}/process
- GET /api/documents/{id}/metadata

### Web ingestion APIs

- POST /api/web/import

### Chunking APIs

- POST /api/documents/{id}/chunks
- GET /api/documents/{id}/chunks
- GET /api/chunks/{id}

### Embedding APIs

- POST /api/chunks/{chunkId}/embeddings
- GET /api/chunks/{chunkId}/embeddings
- GET /api/embeddings/{id}

### Vector search APIs

- POST /api/search/vector

## Database Overview

The database model is designed around retrieval readiness, document processing, and future AI workflows.

### Core entities

- Document
  - Id
  - Title
  - Content
  - FileName
  - ContentType
  - FileSize
  - SourceUrl
  - CreatedAt

- DocumentMetadata
  - Id
  - DocumentId
  - FileName
  - ContentType
  - CharacterCount
  - WordCount
  - LineCount
  - ProcessedAt

- DocumentChunk
  - Id
  - DocumentId
  - ChunkIndex
  - Content
  - CharacterCount
  - CreatedAt

- DocumentEmbedding
  - Id
  - ChunkId
  - EmbeddingVector
  - VectorDimensions
  - ModelName
  - CreatedAt

### Database characteristics

- PostgreSQL-based persistence
- pgvector extension enabled
- EF Core relationship mapping
- Document-to-metadata relationship
- Document-to-chunks relationship
- Chunk-to-embedding relationship
- Migration-driven schema evolution
- Vector-ready storage for retrieval workflows

## Observability & Monitoring

This solution includes the telemetry foundation needed for enterprise readiness and future AI diagnostics.

### Azure Application Insights

- Application Insights integration is configured for local development via .NET User Secrets
- Telemetry initialization is guarded so the application can still start without a configured connection string
- Event and metric tracking are prepared for API, database, embedding, and search telemetry

### OpenTelemetry

- ASP.NET Core instrumentation enabled
- HttpClient tracing enabled
- SQL instrumentation enabled
- Metrics pipeline configured for service telemetry
- Structured logging readiness prepared for Azure Monitor and Application Insights export

### Telemetry coverage

- API request telemetry
- Request duration and failure tracking
- Database operation tracking
- Embedding generation tracking
- Vector search tracking
- Search scoring and result count visibility

## Security & Configuration

Security and configuration are handled with a safe and staged approach for local development.

### User Secrets

The API project includes a User Secrets ID and loads secure values using the .NET user-secrets configuration provider.

Required local configuration entries:

- `ConnectionStrings:DefaultConnection`
- `ApplicationInsights:ConnectionString`

The repository does not store these values in source-controlled configuration files.

### Local developer guidance

- PostgreSQL connection string is stored in User Secrets
- Application Insights connection string is stored in User Secrets
- The app can run successfully in development without hardcoded secrets
- Application Insights is optional when no connection string is configured

## Roadmap

| Phase | Name | Status |
| --- | --- | --- |
| Phase 1 | API Foundation | ✅ Completed |
| Phase 2 | PostgreSQL Foundation | ✅ Completed |
| Phase 3 | Document Management API | ✅ Completed |
| Phase 4A | Document Upload Foundation | ✅ Completed |
| Phase 4B | Document Processing Foundation | ✅ Completed |
| Phase 5A | Web Content Ingestion | ✅ Completed |
| Phase 5B | Chunking Foundation | ✅ Completed |
| Phase 6 | Embeddings Foundation | ✅ Completed |
| Phase 7 | Vector Search Foundation | ✅ Completed |
| Phase 8 | Observability Foundation | ✅ Completed |
| Phase 9 | Basic RAG | ⬜ Planned |
| Phase 10 | Authentication & Authorization | ⬜ Planned |
| Phase 11 | Azure AI Foundry Integration | ⬜ Planned |
| Phase 12 | Copilot Studio Integration | ⬜ Planned |
| Phase 13 | GraphRAG | ⬜ Planned |
| Phase 14 | Blazor / Vue / React Administration Portal | ⬜ Planned |

## Azure AI Foundry Vision

Azure AI Foundry is the next strategic integration point for this platform. The current architecture is intentionally designed to support future embedding models, prompt workflows, and agent-based orchestration without prematurely coupling the solution to a single AI provider.

### Planned capabilities

- Embedding model integration
- Chat model deployment integration
- Prompt flow orchestration
- Evaluation pipelines
- Safety and governance controls
- AI telemetry and observability

## Copilot Studio Vision

Copilot Studio will act as a future user experience layer that connects domain knowledge to conversational automation.

### Planned scenarios

- Business knowledge agents
- IT support agents
- HR support agents
- External knowledge-driven assistants
- REST integration with the platform backend

## Authentication Vision

Authentication and authorization are planned for a later enterprise phase.

### Planned identity model

- Microsoft Entra ID integration
- Role-Based Access Control (RBAC)
- SuperUser administration
- Standard User access model
- Conversation history persistence
- Conversation visibility restrictions
- Audit logging and governance review

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL with pgvector enabled
- Docker (optional, for local database provisioning)
- Git

### Restore dependencies

```bash
dotnet restore
```

### Build the project

```bash
dotnet build RagPlatform.Api/RagPlatform.Api.csproj
```

### Apply database migrations

```bash
dotnet ef database update --project RagPlatform.Api/RagPlatform.Api.csproj --startup-project RagPlatform.Api/RagPlatform.Api.csproj
```

### Run the application

```bash
dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Access Swagger

```text
https://localhost:5078/swagger
```

## Current Statistics

- Completed phases: 10 / 14
- Active implementation maturity: foundation + retrieval + observability complete
- Document lifecycle: implemented
- Retrieval preparation: implemented
- Vector storage: implemented
- Search foundation: implemented
- Observability foundation: implemented
- Advanced AI orchestration: not yet implemented
- Basic RAG: planned
- Authentication and user governance: planned

## License

This project is intended for portfolio demonstration, technical evaluation, and architectural learning. It represents a disciplined, staged approach to building an AI-ready knowledge platform with a secure and observable backend foundation.

---

RAG Platform 2.0 is designed to evolve from a disciplined backend foundation into a modern, retrieval-aware, enterprise-ready AI platform.