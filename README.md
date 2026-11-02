# RAG Platform 2.0

A portfolio project built to demonstrate a disciplined, phased approach to building a Retrieval-Augmented Generation platform with .NET, PostgreSQL, Entity Framework Core, and future AI integration points.

## Project Overview

RAG Platform 2.0 is a structured, milestone-based solution designed to evolve from a clean API foundation into a future-ready RAG architecture. The project intentionally keeps scope narrow and production-oriented during each phase so the underlying foundation remains stable before introducing heavier AI and retrieval features.

The current implementation covers the document lifecycle, PostgreSQL persistence, document processing, web ingestion, chunk preparation, and embedding architecture.

## Current Status

The project has successfully completed the following phases:

### Completed Phases

- Phase 1: API Foundation
- Phase 2: PostgreSQL Foundation
- Phase 3: Document Management API
- Phase 4A: Document Upload Foundation
- Phase 4B: Document Processing Foundation
- Phase 5A: Web Content Ingestion
- Phase 5B: Chunking Foundation
- Phase 6: Embeddings Foundation

### In Progress

- None at the moment

### Planned Phases

- Phase 7: Vector Search Foundation
- Phase 8: Semantic Search
- Phase 9: Basic RAG
- Phase 10: Azure AI Foundry Integration
- Phase 11: Copilot Studio Integration
- Phase 12: GraphRAG

## Project Progress

| Metric | Status |
| --- | --- |
| Completed phases | 6 / 12 |
| In-progress phases | 0 |
| Planned phases | 6 |
| Architecture maturity | Foundation ready for vector and retrieval work |

## Implemented Features

- ASP.NET Core Web API with Swagger/OpenAPI
- PostgreSQL integration
- Entity Framework Core persistence layer
- Document CRUD API
- Text document upload support for `.txt` and `.md`
- Document processing metadata service
- Web content ingestion from public URLs
- HTML extraction and normalization with HtmlAgilityPack
- Chunk generation service and controller
- Document chunk storage and retrieval
- Embedding architecture with provider abstraction
- Placeholder embedding provider for future vendor integrations
- Dependency injection-based service composition
- API documentation through XML comments and Swagger

## Architecture Overview

The platform is intentionally structured as a layered application that supports iterative feature delivery without coupling the system to vendor-specific AI dependencies too early.

### Current Architecture Layers

- API layer: ASP.NET Core Web API controllers
- Service layer: document, processing, web ingestion, chunking, and embedding services
- Data layer: Entity Framework Core with PostgreSQL
- Persistence model: documents, metadata, chunks, and embedding metadata
- Provider abstraction: future embedding providers for Azure AI Foundry, Azure OpenAI, OpenAI, or local models

This keeps the solution maintainable, testable, and aligned with a staged roadmap for intelligent document retrieval.

## Embeddings Foundation

Phase 6 establishes the abstraction required for future embedding workflows without yet integrating a concrete AI provider.

### Included in the Embeddings Foundation

- `DocumentEmbedding` entity
- `IEmbeddingProvider` interface
- `IEmbeddingService` interface
- `EmbeddingService` placeholder implementation
- `PlaceholderEmbeddingProvider`
- `EmbeddingController`
- EF Core persistence for embedding metadata records
- provider-ready design that supports future Azure AI Foundry, Azure OpenAI, OpenAI, and local model integration

### Design Principles

- Dependency injection
- SOLID-compatible service boundaries
- Async/await-based workflow
- ILogger support
- No production AI calls yet
- No pgvector implementation yet
- No semantic search or RAG orchestration yet

## Roadmap

| Phase | Name | Status | Description |
| --- | --- | --- | --- |
| Phase 1 | API Foundation | ✅ Completed | ASP.NET Core API, Swagger, and health configuration |
| Phase 2 | PostgreSQL Foundation | ✅ Completed | EF Core and PostgreSQL setup |
| Phase 3 | Document Management API | ✅ Completed | Document CRUD operations |
| Phase 4A | Document Upload Foundation | ✅ Completed | Text-based file ingestion |
| Phase 4B | Document Processing Foundation | ✅ Completed | Metadata generation and document processing |
| Phase 5A | Web Content Ingestion | ✅ Completed | URL import and HTML extraction |
| Phase 5B | Chunking Foundation | ✅ Completed | Chunk creation, metadata, and storage |
| Phase 6 | Embeddings Foundation | ✅ Completed | Embedding provider architecture and metadata persistence |
| Phase 7 | Vector Search Foundation | 📋 Planned | Vector storage and similarity foundations |
| Phase 8 | Semantic Search | 📋 Planned | Semantic matching and ranking work |
| Phase 9 | Basic RAG | 📋 Planned | Retrieval + prompting workflow |
| Phase 10 | Azure AI Foundry Integration | 📋 Planned | Foundry integration and orchestration |
| Phase 11 | Copilot Studio Integration | 📋 Planned | Copilot Studio experience and connectors |
| Phase 12 | GraphRAG | 📋 Planned | Graph-based retrieval augmentation |

## Technology Stack

### Core Technologies

- ASP.NET Core 10
- PostgreSQL
- Entity Framework Core
- Npgsql
- Swagger/OpenAPI
- HTML parsing with HtmlAgilityPack

### Implemented Capabilities

- Chunking services
- Embedding architecture
- Document lifecycle workflows
- Web ingestion services
- Database-backed persistence

## API Endpoints

### Document APIs

- GET /api/documents
- GET /api/documents/{id}
- POST /api/documents
- PUT /api/documents/{id}
- DELETE /api/documents/{id}
- POST /api/documents/upload

### Processing APIs

- POST /api/documents/{id}/process
- GET /api/documents/{id}/metadata

### Web Import APIs

- POST /api/web/import

### Chunking APIs

- POST /api/documents/{id}/chunks
- GET /api/documents/{id}/chunks
- GET /api/chunks/{id}

### Embedding APIs

- POST /api/chunks/{id}/embeddings
- GET /api/chunks/{id}/embeddings

## Development Setup

### Prerequisites

- .NET SDK
- PostgreSQL instance
- Git
- Optional: Docker Desktop for local infrastructure support

### Run the API

```bash
dotnet restore
dotnet build
dotnet ef database update --project RagPlatform.Api --startup-project RagPlatform.Api
dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Open Swagger

```text
https://localhost:5078/swagger
```

## Summary

RAG Platform 2.0 has reached a mature foundation stage. The solution currently includes reliable API, persistence, document processing, web ingestion, chunking, and embedding architecture patterns. The next strategic milestone is vector search and retrieval enablement, while still preserving a clean and extensible architecture for future Azure AI and orchestration layers.

## License

This project is licensed under the MIT License.
