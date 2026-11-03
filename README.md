# RAG Platform 2.0

A portfolio-grade .NET platform designed to evolve from a clean API foundation into a production-ready retrieval and AI architecture. The solution demonstrates disciplined milestone delivery, strong backend engineering practices, and a clear roadmap toward enterprise-grade knowledge retrieval, AI orchestration, and agent experiences.

## 1. Project Overview

RAG Platform 2.0 is a phased software platform focused on building a reliable retrieval pipeline for document-based knowledge systems. The project is intentionally structured around roadmap-aligned milestones so each layer is validated before moving into more advanced AI, vector, and orchestration capabilities.

The solution currently includes the foundational architecture for:

- ASP.NET Core API development
- PostgreSQL persistence with Entity Framework Core
- Document lifecycle management
- Document upload and metadata processing
- Web content ingestion
- Chunk preparation for downstream retrieval use cases
- Embedding architecture abstraction for future AI providers

This repository is positioned as a professional portfolio project for solution architects, backend engineers, and AI platform engineers seeking to demonstrate disciplined system design and phased delivery.

## 2. Project Vision

The long-term vision for this project is to evolve into an enterprise-ready AI knowledge platform capable of ingesting, processing, understanding, and retrieving structured and unstructured information across internal and external sources.

The architecture is designed to support:

- Document ingestion from files and public web content
- Chunk-based preparation for retrieval workflows
- Embedding abstraction for multiple AI providers
- Retrieval and ranking in future phases
- Semantic search and vector retrieval
- RAG orchestration over indexed knowledge
- Enterprise authentication and governance
- Azure AI Foundry integration and orchestration workflows
- Copilot Studio and business-agent experiences
- Graph-based knowledge retrieval and augmentation

The project intentionally avoids premature AI vendor coupling. Instead, it creates stable foundations first, ensuring that future AI capabilities can be added cleanly without destabilizing the platform.

## 3. Current Status

The project is currently aligned to the following completed milestones:

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

The current solution successfully includes:

- ASP.NET Core Web API
- Swagger / OpenAPI documentation
- PostgreSQL database integration
- Entity Framework Core model configuration and migrations
- Health endpoint
- Document CRUD API
- Document upload API
- Document processing API
- Web content import API
- Document chunking API
- Embedding foundation architecture
- SourceUrl support
- Metadata persistence
- Database migration support

### Current maturity

The software has reached the foundation stage for retrieval-oriented AI systems. It is not yet a complete RAG platform, but it is structurally ready for the next vector and search-driven phases.

## 4. Architecture Overview

The project follows a layered architecture that separates concern boundaries and supports future scaling.

### Current architecture layers

- API layer: ASP.NET Core controllers exposing endpoints for documents, processing, web ingestion, chunking, and embedding foundations
- Service layer: business logic for document management, processing, web import, chunk creation, and embedding preparation
- Data layer: PostgreSQL via Entity Framework Core
- Persistence model: documents, metadata, chunks, and embedding metadata
- Provider abstraction: embedding provider interfaces designed to support multiple future model providers

### Design principles

- Clear separation of concerns
- Dependency injection throughout
- Async and await-first implementation patterns
- Service-based business logic
- Thin controller layer
- Database-backed persistence for all core workflows
- Forward-compatible design for future AI integrations

This architecture is intentionally modular, allowing future phases to add vector search, retrieval, governance, and model integrations without reworking the foundation.

## 5. Technology Stack

### Core technologies

- ASP.NET Core 10
- PostgreSQL
- Entity Framework Core
- Npgsql
- Swagger / OpenAPI
- HtmlAgilityPack
- C# / .NET backend development

### Additional implemented capabilities

- Document CRUD and persistence
- File upload validation and file metadata tracking
- Text processing and document analysis
- Web source ingestion and HTML extraction
- Chunk generation and persistence logic
- Embedding abstraction layer for future model integrations

## 6. Implemented Features

### Implemented backend capabilities

- API project scaffold and configuration
- Swagger/OpenAPI configuration
- Health endpoint and operational readiness checks
- PostgreSQL connection and EF Core configuration
- Document entity and persistence model
- Document metadata entity and tracking
- SourceUrl support for documents
- Public URL import using HttpClientFactory
- HTML content download, parsing, and normalization
- Document chunk generation with configurable chunk sizing and overlap
- Chunk persistence linked to parent documents
- Duplicate chunk prevention logic
- Embedding metadata abstraction with provider-ready interfaces
- Logging via ILogger patterns
- Migration-based database evolution

### Implemented domain workflows

- Create, read, update, and delete documents
- Upload and validate content files
- Process stored documents and derive metadata
- Import web page content into the platform
- Split content into chunks for retrieval preparation
- Prepare future embedding metadata objects for downstream AI features

## 7. API Capabilities

The platform exposes a structured API intended for document processing and future retrieval workflows.

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

### Web content ingestion API

- POST /api/web/import

### Chunking APIs

- POST /api/documents/{id}/chunks
- GET /api/documents/{id}/chunks
- GET /api/chunks/{id}

### Embedding foundation APIs

- POST /api/chunks/{id}/embeddings
- GET /api/chunks/{id}/embeddings

These APIs are designed for staged feature development and are intentionally restricted to the foundation layer, not full end-to-end AI retrieval.

## 8. Database Schema Overview

The current data model is focused on structured document and content processing persistence.

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
  - Metadata relationship
  - Chunk relationship

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

- PostgreSQL-backed persistence
- EF Core relationship mapping
- Cascading document deletion patterns
- Unique constraints for document/chunk pairs and chunk/model combinations
- Migration-based schema evolution

## 9. Development Roadmap

The solution follows a disciplined roadmap and intentionally keeps each phase focused.

### Completed roadmap

| Phase | Name | Status |
| --- | --- | --- |
| Phase 1 | API Foundation | ✅ Completed |
| Phase 2 | PostgreSQL Foundation | ✅ Completed |
| Phase 3 | Document Management | ✅ Completed |
| Phase 4A | Document Upload Foundation | ✅ Completed |
| Phase 4B | Document Processing | ✅ Completed |
| Phase 5A | Web Content Ingestion | ✅ Completed |
| Phase 5B | Chunking Foundation | ✅ Completed |
| Phase 6 | Embeddings Foundation | ✅ Completed |

### Planned roadmap

| Phase | Name | Status |
| --- | --- | --- |
| Phase 7 | Vector Search Foundation | ⬜ Planned |
| Phase 8 | Semantic Search | ⬜ Planned |
| Phase 9 | Basic RAG | ⬜ Planned |
| Phase 10 | Observability Foundation | ⬜ Planned |
| Phase 11 | Authentication & Authorization | ⬜ Planned |
| Phase 12 | Azure AI Foundry Integration | ⬜ Planned |
| Phase 13 | Copilot Studio Integration | ⬜ Planned |
| Phase 14 | GraphRAG | ⬜ Planned |
| Phase 15 | Blazor Administration Portal | ⬜ Planned |

### Observability roadmap details

Phase 10 is intended to introduce operational maturity and production monitoring through:

- Azure Application Insights
- OpenTelemetry instrumentation
- Structured logging and telemetry pipelines
- API health and dependency monitoring
- Request tracing and AI workflow telemetry

## 10. Future Enterprise Features

The platform is intentionally designed to grow beyond foundational ingestion and retrieval into a full enterprise knowledge platform.

### Enterprise product direction

- Multi-tenant content ingestion across internal and external sources
- User-facing knowledge workspace management
- Search, summarization, and retrieval workflows
- Policy-controlled access to documents and conversation content
- Audit trails for AI and business operations
- Secure handling of enterprise documents and data governance workflows
- Scalable pipeline architecture for additional data sources and retrieval patterns

## 11. Azure AI Foundry Vision

Future support for Azure AI Foundry is planned as a strategic integration layer for enterprise AI workloads.

### Planned capabilities

- Embedding models for vector generation and semantic retrieval
- Chat models for grounded answer generation
- Prompt flows for orchestrated AI interactions
- Agent workflows for task execution and knowledge retrieval
- Evaluation pipelines for quality, safety, and accuracy
- Safety features including guardrails and policy enforcement
- AI telemetry for observability, diagnostics, and optimization

The current phase intentionally does not yet call Azure AI Foundry services. This avoids premature integration while the data and retrieval foundation remains under construction.

## 12. Copilot Studio Vision

The platform also envisions integration with Microsoft Copilot Studio to support conversational and business-oriented experiences.

### Planned capabilities

- Copilot Studio agents aligned to business domains
- REST-based integration with platform services
- Agent orchestration across internal knowledge sources
- Business Knowledge Agents for enterprise content access
- IT Support Agents for operational workflows
- HR Agents for employee support scenarios

This phase is positioned as a future user-facing experience layer, built on top of a stable backend foundation rather than as an early development dependency.

## 13. Authentication and Authorization Vision

Authentication and authorization are defined as a future enterprise capability and will be introduced once the core knowledge architecture is mature.

### Planned identity model

- Microsoft Entra ID integration
- User role management
- SuperUser role
- Standard User role
- Conversation history persistence
- Conversation visibility restrictions
- Audit logging for security and compliance review

### Governance objectives

- Restrict access based on user role and content sensitivity
- Preserve conversation history for user accountability
- Support enterprise identity and access patterns
- Create an auditable security model for future agent and RAG workflows

## 14. Getting Started

### Prerequisites

- .NET SDK
- PostgreSQL instance
- Git
- Optional: Docker for local or test infrastructure

### Restore and build

```bash
dotnet restore
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

### Swagger UI

Once running, open the Swagger UI in the browser:

```text
https://localhost:5078/swagger
```

## 15. Current Statistics

### Implementation snapshot

- Completed phases: 6 / 15
- Planned phases: 9
- Core architecture maturity: Foundation complete
- Data persistence: PostgreSQL + EF Core
- API layer: Active
- AI provider integration: Not yet connected
- Retrieval foundation: Planned next

### Current project position

This repository sits at a strong engineering foundation stage: it has implemented the essential ingestion, processing, and preparation patterns needed before introducing vector search, semantic retrieval, and enterprise AI experiences.

## 16. License

This project is provided for portfolio, learning, and architectural demonstration purposes. It is intended to illustrate a structured, milestone-driven approach to building an AI-enabled knowledge platform.

---

RAG Platform 2.0 is a disciplined, enterprise-minded engineering effort designed to move from backend foundation to intelligent retrieval without compromising architecture quality or roadmap clarity.
