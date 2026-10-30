# RAG Platform 2.0

A portfolio project for building a modern .NET-based Retrieval-Augmented Generation platform with a clean architectural foundation.

## Project Overview

RAG Platform 2.0 is a learning and portfolio project focused on building an AI-enabled application using the Microsoft .NET ecosystem. The project currently focuses on establishing the application foundation and database persistence layer before moving into document processing, vector retrieval, and AI orchestration.

The solution is intentionally scoped to stay practical and incremental. At this stage, the project demonstrates a working ASP.NET Core API and a PostgreSQL + Entity Framework Core foundation, without yet implementing RAG logic, AI integrations, or repository abstractions beyond the foundational DbContext pattern.

## Current Phase

### Phase 2: PostgreSQL Foundation

This repository is currently in the PostgreSQL and EF Core foundation milestone.

Completed in this milestone:

- ASP.NET Core Web API
- Swagger/OpenAPI
- Health endpoint
- Dependency injection setup
- PostgreSQL configuration
- Entity Framework Core setup
- ApplicationDbContext implementation
- Document entity model
- Initial EF Core migration
- Docker Compose setup for PostgreSQL
- Database validation and connectivity checks

Not yet implemented:

- RAG pipeline
- AI orchestration
- Azure AI Foundry integration
- Azure OpenAI integration
- Semantic Kernel integration
- Document ingestion and chunking services
- Vector search and pgvector indexing
- Repository/service layer beyond the data foundation
- Blazor UI

## Architecture Overview

The current architecture is intentionally minimal and aligned to the roadmap:

- API layer: ASP.NET Core Web API
- Data layer: PostgreSQL via Entity Framework Core
- Persistence model: ApplicationDbContext + Document entity
- Local database runtime: Docker Compose PostgreSQL container
- Observability: health checks and connectivity validation

The system is designed to evolve into a full RAG platform without coupling future AI functionality into the current data foundation.

## Current Project Status

### Status Summary

| Area | Status | Notes |
| --- | --- | --- |
| Solution structure | ✅ Completed | Base repository and project structure established |
| ASP.NET Core API foundation | ✅ Completed | API runs and exposes Swagger |
| Swagger/OpenAPI | ✅ Completed | API documentation enabled |
| Health endpoint | ✅ Completed | Health checks include application and database validation |
| PostgreSQL integration | ✅ Completed | PostgreSQL configured for local development |
| Entity Framework Core | ✅ Completed | EF Core is configured and working |
| Document persistence model | ✅ Completed | Document entity and DbSet added |
| Initial migration | ✅ Completed | Initial EF Core migration created and applied |
| Docker Compose support | ✅ Completed | PostgreSQL container defined for local use |
| RAG implementation | 📋 Planned | Not included in this milestone |
| AI services | 📋 Planned | Azure AI Foundry/OpenAI remain future work |
| Semantic Kernel | 📋 Planned | Future phase |
| pgvector / vector retrieval | 📋 Planned | Future phase |
| Blazor UI | 📋 Planned | Future phase |

## Technology Stack

### Core Platform

- ASP.NET Core Web API
- .NET 10
- Swagger/OpenAPI
- Entity Framework Core
- Npgsql PostgreSQL provider
- Docker Compose

### Data Foundation

- PostgreSQL
- ApplicationDbContext
- Document entity
- EF Core migrations

### Planned Future Stack

- pgvector
- Azure AI Foundry
- Azure OpenAI
- Semantic Kernel
- Copilot Studio integration
- Blazor UI
- OpenTelemetry

## Roadmap Status

| Phase | Status | Outcome |
| --- | --- | --- |
| Phase 1: API Foundation | ✅ Completed | Stable API scaffold, Swagger, and health checks |
| Phase 2: PostgreSQL Foundation | ✅ Completed | PostgreSQL + EF Core + migration-ready data layer |
| Phase 3: Document Pipeline | 📋 Planned | Document ingestion and processing |
| Phase 4: Vector Search | 📋 Planned | Embeddings and retrieval layer |
| Phase 5: RAG Orchestration | 📋 Planned | Retrieval and generation workflow |
| Phase 6: Azure Integration | 📋 Planned | Foundry and OpenAI connectivity |
| Phase 7: Experience and Operations | 📋 Planned | UI, observability, and deployment |

## Implemented Features

### ✅ Completed

- ASP.NET Core Web API initialized and configured
- Swagger/OpenAPI enabled for service exploration
- Health endpoint implemented and validated
- PostgreSQL connection string configured
- Entity Framework Core registered in dependency injection
- ApplicationDbContext created for persistence
- Document model implemented with Guid identifier and timestamp
- Initial migration generated and applied successfully
- Docker Compose configured for PostgreSQL startup
- Database connectivity validated with health checks

### 📋 Not Included in This Milestone

- RAG retrieval logic
- AI provider integration
- Semantic Kernel orchestration
- Vector search and pgvector workflows
- Repository pattern beyond the database foundation
- Business document services
- UI layer

## Project Structure

```text
RagPlatform/
├── README.md
├── RagPlatform.slnx
├── docker-compose.yml
├── RagPlatform.Api/
│   ├── Contracts/
│   ├── Controllers/
│   ├── Data/
│   │   ├── Migrations/
│   │   └── ApplicationDbContext.cs
│   ├── Models/
│   │   ├── Document.cs
│   │   └── HealthStatusResponse.cs
│   ├── Services/
│   │   └── HealthService.cs
│   ├── Properties/
│   ├── Program.cs
│   ├── appsettings.json
│   └── RagPlatform.Api.csproj
├── .gitignore
└── .github/
```

## Development Setup

### Prerequisites

- .NET SDK
- Docker Desktop
- PostgreSQL client tools (optional)
- Git

### Run PostgreSQL locally

```bash
docker compose up -d
```

### Run the API

```bash
dotnet restore
dotnet build
dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Access Swagger

```text
https://localhost:<port>/swagger
```

If the app is running with local ASP.NET defaults, Swagger is available through the configured HTTPS port provided by the development environment.

## Current Focus

The current milestone is intentionally narrow and stable:

- validate the API foundation
- ensure PostgreSQL connectivity works locally
- establish EF Core modeling conventions
- prepare the data layer for future document and retrieval features

The project is not yet implementing AI, retrieval, or RAG workflows. Those remain future milestones.

## License

This project is licensed under the MIT License.

```text
MIT License

Copyright (c) 2026 RAG Platform 2.0

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Summary

RAG Platform 2.0 has successfully established the API and PostgreSQL foundation required for the next stage of platform development. The project is now aligned with the roadmap and prepared to extend into document processing, vector search, and AI orchestration without introducing premature scope beyond the current milestone.
