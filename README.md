# RAG Platform 2.0

A modern .NET-based Retrieval-Augmented Generation platform built with a clean and incremental architecture.

## Project Overview

RAG Platform 2.0 is a portfolio project focused on building a practical AI application foundation using the Microsoft .NET ecosystem. The solution currently emphasizes a stable API layer, PostgreSQL persistence, and document management capabilities, while keeping the scope intentionally narrow and production-oriented.

The current milestone is focused on document CRUD operations only. AI, vector search, and retrieval workflows remain future phases.

## Current Phase

### Phase 3: Document Management API

This milestone introduces a complete CRUD API for Document entities, using ASP.NET Core, Entity Framework Core, PostgreSQL, and dependency-injected services.

Completed in this milestone:

- ASP.NET Core Web API
- Swagger/OpenAPI
- Health endpoint
- PostgreSQL + EF Core persistence foundation
- Document entity and DbSet
- ApplicationDbContext configuration
- Document service contract and implementation
- Document controller with CRUD endpoints
- Validation for required title and content fields
- XML comments for Swagger documentation

Not yet implemented:

- RAG pipeline
- AI orchestration
- Azure AI Foundry integration
- Azure OpenAI integration
- Semantic Kernel integration
- pgvector or semantic retrieval
- File ingestion and chunking services
- Blazor UI

## Architecture Overview

The current architecture is intentionally small and maintainable:

- API layer: ASP.NET Core Web API
- Data access: Entity Framework Core with PostgreSQL
- Service layer: Document business logic and validation
- Controller layer: thin API endpoints only
- Persistence model: Document entity stored in the Documents table

This keeps the current milestone aligned with the roadmap while preserving a clean foundation for future retrieval and AI features.

## Current Project Status

| Area | Status | Notes |
| --- | --- | --- |
| Solution structure | ✅ Completed | Base project structure established |
| ASP.NET Core API foundation | ✅ Completed | API is running and exposed through Swagger |
| Swagger/OpenAPI | ✅ Completed | Endpoints documented and visible |
| Health endpoint | ✅ Completed | API health checks are available |
| PostgreSQL integration | ✅ Completed | PostgreSQL is configured and connected |
| Entity Framework Core | ✅ Completed | DB context and persistence are in place |
| Document CRUD API | ✅ Completed | Create, read, update, and delete workflow is implemented |
| Validation | ✅ Completed | Title and content are required |
| pgvector / vector search | 📋 Planned | Not part of this milestone |
| AI / RAG integration | 📋 Planned | Future phases |
| UI | 📋 Planned | Future phases |

## Technology Stack

### Core Platform

- ASP.NET Core Web API
- .NET 10
- Swagger/OpenAPI
- Entity Framework Core
- Npgsql
- PostgreSQL

### Current Domain Focus

- Document entity persistence
- CRUD API layer
- DI-based service architecture
- Async database access
- Structured validation

## Roadmap Status

| Phase | Status | Outcome |
| --- | --- | --- |
| Phase 1: API Foundation | ✅ Completed | API scaffold, Swagger, and health checks |
| Phase 2: PostgreSQL Foundation | ✅ Completed | EF Core and PostgreSQL foundation ready |
| Phase 3: Document Management API | ✅ Completed | CRUD API for documents |
| Phase 4: Document Processing | 📋 Planned | File ingestion and document workflow expansion |
| Phase 5: Vector Search | 📋 Planned | Embeddings, retrieval, and indexing |
| Phase 6: RAG Orchestration | 📋 Planned | Prompt and retrieval pipeline |
| Phase 7: Azure Integration | 📋 Planned | Azure AI Foundry and OpenAI integration |

## Endpoints

### Document API

- GET /api/documents
- GET /api/documents/{id}
- POST /api/documents
- PUT /api/documents/{id}
- DELETE /api/documents/{id}

## Development Setup

### Prerequisites

- .NET SDK
- Docker Desktop
- PostgreSQL running locally
- Git

### Run the API

```bash
dotnet restore
dotnet build
dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Swagger UI

Open the Swagger UI in the browser at the local ASP.NET URL generated for the app, for example:

```text
http://localhost:5129/swagger
```

## Summary

The project is now aligned with the roadmap for a clean and maintainable document management layer. The implementation remains intentionally scoped to CRUD and persistence without introducing AI, vector retrieval, or RAG features prematurely.

## License

This project is licensed under the MIT License.
