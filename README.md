# RAG Platform 2.0

A portfolio-grade .NET platform for building a retrieval-first, AI-ready knowledge system. This project demonstrates disciplined backend engineering, phased delivery, and a clear path from document ingestion to semantic retrieval, enterprise AI integration, and agent-driven experiences.

## Project Overview

RAG Platform 2.0 is a modular .NET-based application designed to manage the lifecycle of organizational knowledge from ingestion through preparation, retrieval, and future AI-powered answer generation. The project follows a milestone-driven roadmap so each phase remains focused, testable, and aligned to real architecture needs.

This repository is intended to showcase:

- ASP.NET Core Web API architecture
- PostgreSQL persistence and EF Core modeling
- Document ingestion and metadata processing
- Web content import and HTML normalization
- Content chunking for future retrieval flows
- Embedding infrastructure for future model providers
- A clean evolution path toward semantic search and RAG

The solution is built as a professional portfolio project appropriate for recruiters, architects, and senior engineers evaluating backend architecture, system design, and phased AI platform delivery.

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

### Current implementation status

This solution currently includes:

- ASP.NET Core Web API
- Swagger / OpenAPI
- PostgreSQL database integration
- Entity Framework Core migrations
- Health endpoint
- Document CRUD operations
- Document file upload and validation
- Document processing and metadata extraction
- Web content import via URL
- Document chunk generation and persistence
- Embedding foundation abstraction and persistence
- SourceUrl tracking
- Metadata storage
- Database migration support

### Current maturity

The project is no longer in an early scaffold stage. It is now a functional backend foundation with real document lifecycle and retrieval-preparation capabilities. It is not yet a full RAG system, but it is ready for the next phase of vector and retrieval work.

## Architecture Overview

The platform follows a layered and modular design that keeps the system testable, extensible, and vendor-agnostic in its early stages.

### Current architecture layers

- API layer: ASP.NET Core controllers for documents, processing, web ingestion, chunking, and embedding foundation endpoints
- Application layer: service-based business logic for uploads, processing, source import, chunk creation, and future embedding workflow orchestration
- Data layer: PostgreSQL + Entity Framework Core
- Domain layer: document entities, metadata, chunks, and embedding metadata models
- Provider abstraction: embedding interfaces designed to support future AI providers without hard coupling

### Design principles

- Separation of concerns
- Dependency injection throughout the API
- Thin controllers and service-first business logic
- Async/await-first implementation patterns
- Data persistence via EF Core and PostgreSQL
- Future-ready abstractions for AI and retrieval systems
- No premature vendor coupling to Azure AI or RAG layers

This architecture keeps the project maintainable while supporting a controlled progression toward enterprise AI features.

## Technology Stack

### Core stack

- ASP.NET Core 10
- C#
- PostgreSQL
- Entity Framework Core
- Npgsql
- Swagger / OpenAPI
- HtmlAgilityPack
- .NET SDK

### Current capabilities delivered

- Structured document management
- File upload processing
- HTML content import
- Metadata extraction and persistence
- Content chunk creation and storage
- Embedding foundation architecture
- Database migrations and schema evolution

## Implemented Features

### Document management

- Create, read, update, and delete documents
- Document metadata persistence
- Structured document storage in PostgreSQL
- Association between documents and metadata

### Upload and processing

- File upload foundation for text-based content
- Input validation and file metadata capture
- Content type and size tracking
- Processing endpoints for derived document metadata

### Web content ingestion

- Accepts public URL input
- Downloads HTML content using HttpClientFactory
- Extracts page title and main content
- Removes boilerplate HTML and unnecessary page elements
- Stores source URL and document metadata
- Persists the imported content as a document object

### Chunking foundation

- Creates chunk records for documents
- Supports configurable chunk size and overlap
- Preserves sentence boundaries when possible
- Stores generated chunk content and metadata
- Prevents duplicate generation for the same document content set
- Associates chunks with their parent document

### Embeddings foundation

- Embedding metadata entity implemented
- Provider abstraction for future model providers
- Service and controller patterns for future embedding workflows
- Database persistence for embedding metadata
- No production AI provider calls yet

## API Capabilities

The current API already exposes the following capabilities:

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

### Web ingestion API

- POST /api/web/import

### Chunking APIs

- POST /api/documents/{id}/chunks
- GET /api/documents/{id}/chunks
- GET /api/chunks/{id}

### Embedding foundation APIs

- POST /api/chunks/{id}/embeddings
- GET /api/chunks/{id}/embeddings

These endpoints are designed to support a staged roadmap and intentionally avoid premature AI integration beyond foundational model abstraction.

## Database Overview

The current database model is centered on document preparation and retrieval readiness.

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
  - VectorDimensions
  - ModelName
  - CreatedAt

### Database characteristics

- PostgreSQL-based persistence
- EF Core relationship mapping
- Document-to-metadata relationship
- Document-to-chunks relationship
- Chunk-to-embedding relationship
- Unique indexes to prevent duplicate records where needed
- Migration-first schema evolution

## Progress Table

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
| Phase 7 | Vector Search Foundation | ⬜ Planned |
| Phase 8 | Semantic Search | ⬜ Planned |
| Phase 9 | Basic RAG | ⬜ Planned |
| Phase 10 | Observability Foundation | ⬜ Planned |
| Phase 11 | Authentication & Authorization | ⬜ Planned |
| Phase 12 | Azure AI Foundry Integration | ⬜ Planned |
| Phase 13 | Copilot Studio Integration | ⬜ Planned |
| Phase 14 | GraphRAG | ⬜ Planned |
| Phase 15 | Blazor / Vue / React Administration Portal | ⬜ Planned |

## Future Roadmap

The roadmap is intentionally scoped to ensure that architectural foundations are built before advanced AI features are introduced.

### Phase 7 - Vector Search Foundation

Planned work includes:

- pgvector integration
- Embedding storage strategy
- Vector indexing concepts
- Similarity search preparation
- Storage abstractions for retrieval workflows

### Phase 8 - Semantic Search

Planned work includes:

- Semantic ranking and relevance selection
- Search pipeline orchestration
- Retrieval quality optimization
- Query-time document matching

### Phase 9 - Basic RAG

Planned work includes:

- Retrieval of relevant chunks and documents
- Prompt construction based on retrieved knowledge
- Response generation using AI model integration
- Grounded answer workflow

### Phase 10 - Observability Foundation

Planned work includes:

- Azure Application Insights integration
- OpenTelemetry instrumentation
- Request tracing and dependency monitoring
- AI telemetry and diagnostics
- Health and performance visibility for production readiness

### Phase 11 - Authentication & Authorization

Planned work includes:

- Microsoft Entra ID integration
- Role Based Access Control (RBAC)
- User role management
- SuperUser role
- Standard User role
- Conversation history persistence
- Conversation visibility restrictions
- Audit logging for governance and compliance

### Phase 12 - Azure AI Foundry Integration

Planned work includes:

- Embedding model integration
- Chat model integration
- Prompt flow orchestration
- Agent workflow capabilities
- Evaluation pipelines
- Safety and telemetry controls

### Phase 13 - Copilot Studio Integration

Planned work includes:

- Copilot Studio agents
- REST API integration
- Agent orchestration patterns
- Business knowledge agent workflows
- IT support and HR support scenarios

### Phase 14 - GraphRAG

Planned work includes:

- Knowledge graph construction from document relationships
- Graph-based retrieval augmentation
- Entity and relationship awareness
- Structure-aware search and reasoning capabilities

### Phase 15 - Blazor / Vue / React Administration Portal

Planned work includes:

- Admin dashboard for documents and content workflows
- Portal to manage knowledge sources
- Monitoring and retrieval insights
- Operational overview for system administrators

## Azure AI Foundry Vision

Azure AI Foundry is a strategic future integration point for this platform. The foundation is intentionally designed to support this evolution without prematurely coupling the architecture to a single AI provider.

### Planned Foundry capabilities

- Embedding model support
- Chat model deployment integration
- Prompt flow orchestration
- Agent patterns for task execution
- Evaluation pipelines for quality and safety
- AI telemetry and observability
- Governance-centered AI operations

The current implementation avoids direct Azure AI Foundry calls. This keeps the project aligned to its staged roadmap while preparing the system for future integration with enterprise AI services.

## Copilot Studio Vision

Copilot Studio will become a future user experience layer that connects business domain knowledge to conversational experiences.

### Planned Copilot Studio scenarios

- Business knowledge agents
- IT support agents
- HR support agents
- Knowledge-driven operational workflow assistants
- REST-based orchestration with the platform backend

This is a future-facing integration layer and is not part of the current implementation scope.

## Authentication Vision

Authentication and authorization are planned as a future enterprise capability, not part of the current foundation delivery.

### Planned identity model

- Microsoft Entra ID integration
- Role Based Access Control (RBAC)
- SuperUser role for administrative access
- Standard User role for regular application access
- Conversation history persistence
- Conversation visibility restrictions
- Audit logging for sensitive actions and governance review

This vision ensures the eventual platform supports enterprise security and compliance requirements as the project grows beyond the technical prototype stage.

## Application Insights and OpenTelemetry Vision

The next operational milestone will focus on robust observability to support enterprise deployment readiness.

### Planned observability scope

- Azure Application Insights integration
- OpenTelemetry instrumentation
- Request tracing and dependency tracking
- AI workflow telemetry
- Logging for ingestion, processing, retrieval, and errors
- Operational dashboards and high-level service insights

This phase is essential for production readiness, lifecycle monitoring, and troubleshooting in distributed enterprise AI systems.

## Getting Started

### Prerequisites

- .NET SDK
- PostgreSQL instance
- Git
- Optional: Docker for local service management

### Restore dependencies

```bash
dotnet restore
```

### Build the solution

```bash
dotnet build
```

### Apply database migrations

```bash
dotnet ef database update --project RagPlatform.Api --startup-project RagPlatform.Api
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

### Implementation snapshot

- Completed phases: 8 / 15
- Planned phases: 7
- API foundation: implemented
- PostgreSQL integration: implemented
- Document lifecycle: implemented
- Upload foundation: implemented
- Processing foundation: implemented
- Web ingestion: implemented
- Chunking foundation: implemented
- Embedding foundation: implemented
- Provider-driven AI integration: not yet implemented
- Semantic search: planned
- RAG: planned
- Enterprise auth: planned
- Observability: planned

### Current project position

The solution is in a strong foundation stage and is well-prepared for the next major milestone: vector search foundation and retrieval architecture.

## License

This project is intended for portfolio demonstration, technical evaluation, and architectural learning. It is not a production-ready enterprise application yet, but it represents a disciplined and realistic approach to building an AI-enabled knowledge platform.

---

RAG Platform 2.0 is designed to evolve from a disciplined backend foundation into a modern knowledge retrieval and AI platform for enterprise-ready scenarios.
