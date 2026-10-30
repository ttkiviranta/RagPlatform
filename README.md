# RAG Platform 2.0

A modern enterprise-grade Retrieval-Augmented Generation (RAG) platform demonstrating practical Microsoft AI engineering, scalable application design, and production-oriented software architecture.

## Project Overview

RAG Platform 2.0 is a portfolio project designed to showcase how to build and evolve an AI-enabled application using Microsoft ecosystem technologies and modern engineering practices. The project emphasizes architecture clarity, maintainability, extensibility, and real-world readiness for advanced AI workloads.

This initiative focuses on building a layered application foundation for document intelligence, AI-powered retrieval, semantic search, orchestration, and observability. It is intended to illustrate how a cloud-native AI system can evolve from a clean API foundation into a full intelligent platform supported by enterprise-grade services and DevOps workflows.

The project is intentionally structured to demonstrate both technical depth and portfolio quality. It highlights modern .NET development, API-first design, AI integration strategies, and future extensibility toward Azure-native services.

## Vision and Goals

The core vision of RAG Platform 2.0 is to create a practical, extensible AI platform that combines:

- Large language model integration
- Retrieval of contextual enterprise information
- Semantic search over document knowledge bases
- Agentic workflows and orchestration patterns
- Observability, monitoring, and operational readiness
- Clean architectural boundaries that scale over time

Primary goals include:

- Demonstrate modern .NET-based AI application architecture
- Create a reusable foundation for future RAG implementations
- Showcase Microsoft AI and data platform integration patterns
- Provide a professional-grade portfolio asset for recruiters and engineering leaders
- Emphasize maintainable architecture over ad hoc prototypes

## Architecture Overview

The system is planned around a modular, layered architecture designed for long-term extensibility.

### Target Architecture

- Presentation layer: Blazor-based UI for interaction, dashboards, and administration
- API layer: ASP.NET Core Web API for endpoints, orchestration, and integration boundaries
- Application layer: services for orchestration, retrieval, document processing, and AI workflows
- Domain layer: business logic, models, and domain services
- Data layer: PostgreSQL + pgvector for structured and vector-based storage
- AI layer: Azure AI Foundry, Azure OpenAI, and Semantic Kernel integration
- Observability layer: OpenTelemetry for tracing, metrics, and diagnostics
- Integration layer: Microsoft Copilot Studio and external AI ecosystem components

### Architectural Principles

- Separation of concerns across layers
- API-first design
- Extensible domain services
- Cloud-native deployment readiness
- Secure and observable application patterns
- Modular onboarding for future AI capabilities

## Current Project Status

### Project Maturity

Current maturity status: 15% foundational implementation.

The project has established the initial application foundation and is moving toward building the core platform services required for document ingestion, semantic retrieval, and AI reasoning.

### Status Summary

| Area | Status | Notes |
| --- | --- | --- |
| Solution structure | ✅ Completed | Base repository and project structure created |
| ASP.NET Core API foundation | ✅ Completed | API skeleton and controllers enabled |
| Swagger/OpenAPI | ✅ Completed | API documentation enabled |
| HTTPS support | ✅ Completed | HTTPS enabled by default |
| Health endpoint | 🚧 In Progress | Initial API health foundation being stabilized |
| PostgreSQL integration | 📋 Planned | Database layer pending |
| Entity Framework Core | 📋 Planned | Persistence and migrations pending |
| pgvector integration | 📋 Planned | Vector storage and similarity search pending |
| AI orchestration | 📋 Planned | Semantic Kernel and model integration pending |
| Azure AI Foundry | 📋 Planned | Cloud AI integration pending |
| Copilot Studio integration | 📋 Planned | Workflow integration pending |
| OpenTelemetry | 📋 Planned | Objectives, metrics, and traces pending |
| Blazor UI | 📋 Planned | Front-end experience pending |
| CI/CD automation | 📋 Planned | GitHub Actions workflow pending |

## Technology Stack

### Core Platform

- ASP.NET Core Web API
- .NET 10
- Swagger/OpenAPI
- C# / .NET ecosystem

### Data and AI

- PostgreSQL (planned)
- Entity Framework Core (planned)
- pgvector (planned)
- Azure AI Foundry (planned)
- Azure OpenAI (planned)
- Semantic Kernel (planned)

### Integration and Operations

- Microsoft Copilot Studio Integration (planned)
- OpenTelemetry (planned)
- GitHub Actions (planned)
- Docker (planned)
- Blazor UI (planned)

## Development Roadmap

| Phase | Priority | Status | Outcome |
| --- | --- | --- | --- |
| Phase 1: API foundation | High | ✅ Completed | Project scaffold, API, Swagger, and base architecture |
| Phase 2: Data foundation | High | 📋 Planned | PostgreSQL, EF Core, migrations, and schema design |
| Phase 3: Document pipeline | High | 📋 Planned | File ingestion, chunking, and processing services |
| Phase 4: Vector search | High | 📋 Planned | Embeddings, vector storage, and retrieval |
| Phase 5: RAG orchestration | High | 📋 Planned | Retrieval and generation pipeline |
| Phase 6: Agent frameworks | Medium | 📋 Planned | Orchestration and interaction patterns |
| Phase 7: Azure integration | Medium | 📋 Planned | Azure AI Foundry and Azure OpenAI connectivity |
| Phase 8: Observability | Medium | 📋 Planned | OpenTelemetry and production telemetry |
| Phase 9: UI experience | Medium | 📋 Planned | Blazor dashboard and user experience |
| Phase 10: DevOps | Medium | 📋 Planned | Build, automation, and deployment pipelines |

## Implemented Features

### ✅ Completed

- ASP.NET Core Web API project initialized
- Solution and project structure established
- Swagger/OpenAPI enabled for API documentation
- Controller-based routing configured
- HTTPS configuration enabled
- Initial architecture defined for future expansion
- GitHub repository initialized for portfolio visibility

### 🚧 In Progress

- Health API endpoint refinement
- API foundation stabilization
- Service-layer consistency checks
- Baseline application conventions and structure

## Planned Features

### 📋 Planned Core Features

- PostgreSQL integration
- Entity Framework Core setup and migrations
- Document upload and management endpoints
- Chunking service for knowledge segmentation
- pgvector implementation for vector storage
- Embedding generation services
- Semantic and hybrid search capability
- Retrieval-Augmented Generation workflow
- Agent orchestration layer
- GraphRAG exploration and knowledge graph capabilities
- Azure AI Foundry connectivity
- Azure OpenAI integration
- OpenTelemetry observability pipeline
- Copilot Studio business workflow integration
- Blazor-based interface for the end-user experience
- CI/CD automation with GitHub Actions
- Docker-based environment support

## Project Structure

```text
RagPlatform/
├── README.md
├── RagPlatform.slnx
├── RagPlatform.Api/
│   ├── Contracts/
│   ├── Controllers/
│   ├── Data/
│   ├── Models/
│   ├── Services/
│   ├── Properties/
│   ├── Program.cs
│   └── RagPlatform.Api.csproj
├── .github/
│   └── workflows/
├── docker/
├── docs/
├── scripts/
└── tests/
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or later, or VS Code with C# support
- Git
- Docker (planned for later stages)
- PostgreSQL (planned)
- Azure subscription for AI services (planned)

### Run the API locally

```bash
dotnet restore

dotnet build

dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Swagger UI

Once the API is running, Swagger can be accessed at:

```text
https://localhost:7085/swagger
```

or

```text
http://localhost:5172/swagger
```

## Current Sprint

### Sprint Objective

Establish a stable, extensible API foundation that supports future RAG and AI services without compromising maintainability.

### Current Focus

- Stabilize base API contract and controller model
- Validate .NET 10 API runtime behavior
- Confirm architecture conventions for future service layers
- Prepare the project for database and AI integration work

### Sprint Deliverables

- ✅ API project scaffold
- ✅ Swagger documentation enabled
- ✅ HTTPS-enabled startup configuration
- 🚧 Health endpoint validation
- 🚧 Service architecture stabilization

## Future Milestones

### Milestone 1: Data and Persistence Foundation

- PostgreSQL project integration
- Entity Framework Core adoption
- Initial migrations and schema design
- Data service abstraction

### Milestone 2: RAG Core Engine

- Document management APIs
- Chunking and indexing pipeline
- Embeddings generation
- Vector similarity search

### Milestone 3: AI Orchestration

- Semantic Kernel integration
- Retrieval workflows and prompt orchestration
- Agent patterns and orchestration logic
- GraphRAG enhancement

### Milestone 4: Azure Integration

- Azure AI Foundry integration
- Azure OpenAI model connectivity
- Governance and secure configuration patterns
- Cloud deployment considerations

### Milestone 5: Experience and Operations

- Blazor UI implementation
- Observability and telemetry with OpenTelemetry
- CI/CD automation with GitHub Actions
- Production readiness review

## Learning Objectives

This project is intended to provide hands-on learning in:

- Modern ASP.NET Core Web API design
- Layered software architecture for enterprise systems
- AI application integration patterns
- Retrieval-augmented generation approaches
- Vector database strategies with pgvector
- Secure configuration and Azure service integration
- Observability and operational model design
- Portfolio quality documentation and engineering communication

## Portfolio Value

RAG Platform 2.0 is designed as a portfolio-grade solution demonstrating the ability to bridge software engineering and AI application design. It is relevant to hiring managers, solution architects, and senior engineering reviewers because it reflects:

- Strong architectural thinking
- Clear project progression from foundation to platform scale
- Real-world AI engineering patterns
- Microsoft technology alignment
- Understandable and communicable technical decision-making
- Readiness for enterprise modernization and AI platform initiatives

The project is intentionally positioned as a platform narrative: from a minimal but stable API foundation to a future-ready AI ecosystem.

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

---

## Summary

RAG Platform 2.0 represents a thoughtful, modern AI engineering portfolio project that balances technical ambition with practical architectural discipline. It demonstrates the path from a clean API foundation to a future-ready knowledge platform powered by retrieval, semantic search, and AI orchestration capabilities.

This README is written to communicate credible engineering progress, honest maturity, and a clear roadmap for long-term platform evolution.
