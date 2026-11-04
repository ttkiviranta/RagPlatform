# RAG Platform 2.0

A portfolio-grade enterprise AI platform built on ASP.NET Core, PostgreSQL, pgvector, and modern observability tooling. This solution demonstrates a disciplined, milestone-based approach to building a retrieval-ready knowledge platform from document ingestion and processing through embedding storage, vector search, and production monitoring.

## Project Overview

RAG Platform 2.0 is a backend-first platform designed to ingest, process, store, and retrieve organizational knowledge through a modern retrieval architecture. The project is intentionally structured to progress in controlled phases: foundational API and database work first, then document processing, chunking, embeddings, vector retrieval, and observability.

The architecture is intended to evolve from an operational document platform into a production-ready AI foundation capable of supporting:

- semantic search
- retrieval-augmented generation
- AI governance and review workflows
- observability and operational monitoring
- enterprise authentication and authorization
- Copilot-enabled business workflows

This repository is positioned as a professional platform implementation rather than a toy prototype. It reflects real engineering decisions, migration-driven schema evolution, and careful separation between backend foundations and future AI capabilities.

## Current Status

The platform has completed the following phases:

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
- ✅ Phase 9 - Basic RAG Foundation

### Current implementation status

The solution currently includes the following capabilities:

- ASP.NET Core Web API
- Swagger / OpenAPI
- PostgreSQL persistence
- Entity Framework Core migrations
- Health checks and operational endpoint coverage
- Document CRUD endpoints
- Document upload workflows
- Document processing and metadata persistence
- Web content import from public URLs
- Document chunk generation
- Embedding storage and metadata persistence
- pgvector-based vector storage
- Top-K similarity search foundation
- Basic retrieval-augmented generation pipeline
- Context assembly and prompt generation for grounded Q&A
- Azure Application Insights integration
- OpenTelemetry instrumentation
- Secure local configuration using .NET User Secrets

## Architecture Overview

The current architecture follows a modular, layered approach designed to stay maintainable as the platform grows from a retrieval foundation into a broader AI platform.

### Core architecture layers

- API Layer: ASP.NET Core controllers for documents, processing, ingestion, chunking, embeddings, search, and RAG orchestration
- Application Layer: service-oriented business logic for document workflows, retrieval, context assembly, and prompt construction
- Data Layer: PostgreSQL with Entity Framework Core and pgvector
- Domain Layer: document, metadata, chunk, embedding, and retrieval models
- Observability Layer: Azure Application Insights and OpenTelemetry instrumentation
- Configuration Layer: environment-driven configuration and User Secrets

### Basic RAG architecture

The Phase 9 RAG foundation introduces a retrieval-first pipeline:

- user question request
- vector search over stored chunk embeddings
- top-K similarity retrieval
- context assembly from relevant chunks
- prompt generation using retrieved context
- response payload containing the query, retrieved chunk metadata, generated context, and generated prompt

This pattern keeps the application architecture clean and ready for future model integration without coupling the platform to Azure AI Foundry, Semantic Kernel, or Copilot Studio yet.

### Architecture principles

- thin controllers and service-driven logic
- dependency injection across the API
- asynchronous processing patterns
- schema evolution through migrations
- provider-agnostic future AI integration
- observability-first operational design
- secure local development configuration

## Technology Stack

### Core technologies

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

### Implementation footprint

The current platform has successfully integrated the following areas:

- document lifecycle management
- file ingestion and processing
- web content import
- chunking and metadata storage
- embedding persistence
- vector search foundation
- telemetry and monitoring foundation

## Implemented Features

### Phase 1 - API Foundation

- ASP.NET Core Web API startup configuration
- Swagger enablement
- health endpoints
- basic application structure
- project configuration and startup conventions

### Phase 2 - PostgreSQL Foundation

- PostgreSQL configuration
- EF Core DbContext setup
- document and related entities modeled in code
- migration support for schema evolution

### Phase 3 - Document Management API

- document creation
- document retrieval
- document update
- document deletion
- document metadata handling

### Phase 4A - Document Upload Foundation

- upload workflows
- file metadata capture
- validation handling
- storage of uploaded document content

### Phase 4B - Document Processing Foundation

- processing abstractions
- document metadata extraction
- structured document processing pipelines

### Phase 5A - Web Content Ingestion

- URL-based content import
- HTML content extraction
- normalization of imported content
- source URL persistence

### Phase 5B - Chunking Foundation

- chunk creation for document content
- configurable chunk size and overlap patterns
- chunk metadata storage
- duplicate prevention strategies

### Phase 6 - Embeddings Foundation

- embedding metadata entities
- embedding persistence model
- future-ready abstraction for embedding providers
- vector-ready schema foundation

### Phase 7 - Vector Search Foundation

- pgvector support in PostgreSQL
- `EmbeddingVector` storage in `DocumentEmbeddings`
- vector similarity search capability
- Top-K retrieval flow
- search result ranking
- similarity scoring metadata

### Phase 8 - Observability Foundation

- Azure Application Insights integration
- OpenTelemetry tracing and metrics
- structured logging readiness
- request and dependency telemetry
- database, embedding, and search observability hooks

### Phase 9 - Basic RAG Foundation

- end-to-end retrieval pipeline foundation
- vector search retrieval over stored chunk embeddings
- context assembly for grounded question answering
- prompt generation using question + retrieved context
- RAG request telemetry for duration, chunk count, context size, and prompt size
- vendor-neutral foundation for later AI model integration

## API Capabilities

The current API exposes a practical document processing and retrieval foundation. The following endpoints are available:

### Core document APIs

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

### Retrieval and prompt APIs

- POST /api/rag/query

These APIs are intentionally scoped to the implemented foundation and keep the solution aligned to the roadmap instead of prematurely introducing full AI orchestration or app-layer features.

## Database Architecture

The platform uses PostgreSQL as its primary operational database and relies on EF Core migrations for schema evolution.

### Current tables

- Documents
- DocumentMetadata
- DocumentChunks
- DocumentEmbeddings
- __EFMigrationsHistory

### Primary entities

#### Documents
Stores core document records including title, content, file metadata, and source URL.

#### DocumentMetadata
Stores derived processing metadata such as word count, character count, line count, and timing records.

#### DocumentChunks
Stores chunked content segments associated with parent documents.

#### DocumentEmbeddings
Stores embedding vectors, vector dimensions, model metadata, and chunk association data.

### Database characteristics

- PostgreSQL-first persistence model
- sqlite-free current architecture for the production path
- EF Core relationships for document-to-metadata, document-to-chunks, and chunk-to-embedding data
- pgvector extension enabled for vector storage and retrieval readiness
- migration-driven schema evolution

## Observability & Monitoring

The observability layer is a key component of the current platform and reflects enterprise readiness beyond a basic API skeleton.

### Azure Application Insights

- Application Insights is configured for telemetry collection
- connection strings are managed through .NET User Secrets rather than source-controlled files
- telemetry is guarded so local development can run without a configured connection string

### OpenTelemetry

- ASP.NET Core instrumentation enabled
- HttpClient tracing enabled
- SQL instrumentation enabled
- metrics collection enabled
- support for OTLP exporter configuration for future centralized observability pipelines

### Telemetry coverage

- API request and response telemetry
- latency tracking
- database operation visibility
- vector search monitoring
- embedding workflow telemetry
- structured logging readiness

This gives the platform the operational foundation required for future AI platform, enterprise, and production monitoring scenarios.

## Security & Configuration

Security and configuration are handled in a deliberate, environment-aware approach.

### User Secrets

The solution uses .NET User Secrets for local secret storage, including:

- `ConnectionStrings:DefaultConnection`
- `ApplicationInsights:ConnectionString`

This prevents credentials and telemetry connection strings from being committed to source-controlled configuration files.

### Environment-based configuration

The application supports configuration from:

- appsettings files
- environment variables
- .NET user secrets

This layered configuration model is appropriate for local development and supports future cloud deployment patterns.

### Future authentication strategy

Authentication and authorization are not yet implemented, but the system is architected to support a future enterprise model based on:

- Microsoft Entra ID
- RBAC
- role-based access control
- SuperUser administration
- user management and policy enforcement
- conversation history visibility controls
- audit logging and governance review

## Development Roadmap

### Completed

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
- ✅ Phase 9 - Basic RAG Foundation

### Planned

- ⬜ Phase 9 - Basic RAG
- ⬜ Phase 10 - Authentication & Authorization
  - Microsoft Entra ID
  - Role-Based Access Control (RBAC)
  - User Management
  - SuperUser Management
  - Conversation History
  - Audit Logging
- ⬜ Phase 11 - Azure AI Foundry Integration
- ⬜ Phase 12 - Copilot Studio Integration
- ⬜ Phase 13 - GraphRAG
- ⬜ Phase 14 - Blazor / Vue / React Administration Portal
- ⬜ Phase 15 - AI SDLC, MLOps & LLMOps

## AI SDLC Vision

The next evolution of the platform is not only about models and retrieval, but also about disciplined AI delivery and governance.

### Planned AI SDLC capabilities

- AI governance workflows
- model registry support
- prompt registry support
- evaluation pipelines
- quality gates and review processes
- responsible AI guardrails
- change management for model and prompt changes

This phase will make the platform operationally sustainable as AI features become more advanced and business-critical.

## MLOps Vision

MLOps provides the operational model needed to manage AI assets across their lifecycle.

### Planned MLOps capabilities

- model lifecycle management
- model versioning
- deployment tracking
- monitoring and drift observability
- evaluation tracking
- automation of validation and release gates

## LLMOps Vision

LLMOps extends the platform to manage prompts and large language model workflows as first-class operational assets.

### Planned LLMOps capabilities

- prompt management
- prompt versioning
- token monitoring
- cost tracking
- AI telemetry integration
- response quality evaluation
- retrieval evaluation metrics
- operational guardrails for production prompt and model behavior

## Azure AI Foundry Vision

Azure AI Foundry is the natural next integration point for this platform. The current architecture deliberately avoids premature AI vendor coupling while preparing a clean path to enterprise model integration.

### Planned Foundry role in the platform

- embedding model integration
- chat model integration
- orchestration support for AI workflows
- evaluation and governance layering
- centralized AI telemetry and monitoring
- enterprise-safe AI operations model

The platform is designed so Azure AI Foundry can be introduced once the retrieval, observability, and governance foundations are in place.

## Copilot Studio Vision

Copilot Studio will eventually act as a user-facing orchestration layer that consumes the platform’s APIs and business knowledge services.

### Planned scenarios

- business knowledge agents
- support workflows
- HR knowledge agents
- internal operations assistant patterns
- API-driven orchestration with the retrieval platform backend

This approach keeps the backend platform reusable while enabling conversational experiences above a strong data and retrieval foundation.

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL with pgvector enabled
- Docker (optional for local PostgreSQL provisioning)
- Git
- Azure Application Insights connection string for telemetry-enabled local development

### Restore dependencies

```bash
dotnet restore
```

### Build the project

```bash
dotnet build RagPlatform.Api/RagPlatform.Api.csproj
```

### Configure User Secrets

Example local setup:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ragplatform;Username=postgres;Password=your_password;"
dotnet user-secrets set "ApplicationInsights:ConnectionString" "InstrumentationKey=...;IngestionEndpoint=..."
```

### Run the application

```bash
dotnet run --project RagPlatform.Api/RagPlatform.Api.csproj
```

### Access Swagger

```text
https://localhost:5078/swagger
```

### Database setup

If using Docker, PostgreSQL with pgvector support can be started via the project container setup and then the EF Core migrations can be applied.

## Professional Portfolio Positioning

This project is best positioned as an enterprise-grade AI platform foundation, not simply a lightweight RAG example. It demonstrates:

- disciplined architecture progression
- data and persistence engineering
- retrieval-first design thinking
- observability and monitoring maturity
- migration and schema governance
- cloud-aligned configuration management
- scalable path toward AI governance and operational excellence

The platform shows a clear transition from backend foundation to enterprise AI platform architecture, which is highly relevant for recruiters, architects, and senior engineering teams evaluating practical AI platform work at production-readiness depth.

---

RAG Platform 2.0 is designed as a realistic, staged, and extensible foundation for enterprise knowledge retrieval, AI operations, and future generative AI capabilities.