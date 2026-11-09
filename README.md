# RAG Platform 2.0

A portfolio-grade enterprise AI platform built on ASP.NET Core, PostgreSQL, pgvector, Microsoft Entra ID, and modern observability tooling.

## Project Overview

RAG Platform 2.0 is a backend-first platform for ingesting, processing, storing, and retrieving organizational knowledge through a modern retrieval architecture. The solution is intentionally structured around phased delivery so each layer is implemented and validated before the next is introduced.

The project currently demonstrates:

- ASP.NET Core Web API
- PostgreSQL with EF Core
- pgvector storage and similarity search
- document ingestion, processing, and chunking
- vector-based retrieval and context assembly
- Azure Application Insights and OpenTelemetry
- Microsoft Entra ID authentication
- JWT bearer token validation
- current user context and protected API access

## Current Platform Status

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
- ✅ Phase 10A - Microsoft Entra ID Foundation
- ✅ Phase 10B - Authorization & RBAC

### Phase 10A validation

- ✅ Microsoft Entra ID authentication validated
- ✅ OAuth 2.0 Authorization Code Flow with PKCE validated
- ✅ Dedicated App Registration validated
- ✅ Delegated permission `access_as_user` validated
- ✅ JWT token validation validated
- ✅ Protected API access validated
- ✅ `GET /api/documents` returned HTTP 200 using a valid Microsoft Entra ID access token

### Phase 10B validation

Implemented features:

- User role model with `User`, `PowerUser`, and `SuperUser`
- Resource ownership and `OwnerUserId` filtering
- User and tenant isolation
- Policy-based and resource-based authorization

Validation completed with two separate Microsoft Entra ID users:

- User A authenticated, created `USER_A_PRIVATE_DOCUMENT`, and successfully retrieved the owned document.
- User B authenticated and accessed protected endpoints but could not view User A's document.

- ✅ User A can access own documents
- ✅ User B cannot access User A documents
- ✅ Resource ownership enforced
- ✅ User isolation validated
- ✅ RBAC implementation validated successfully

### Current implementation status

The solution currently includes the following capabilities:

- ASP.NET Core Web API
- Swagger / OpenAPI
- PostgreSQL persistence via Entity Framework Core
- pgvector-based vector storage and top-K retrieval
- document CRUD endpoints
- upload, processing, and metadata workflows
- web content import from URL sources
- chunk generation and metadata storage
- embedding persistence and vector search
- basic retrieval-augmented generation pipeline
- context building and prompt generation
- Microsoft Entra ID authentication foundation
- JWT bearer authentication
- user bootstrap and current user context
- protected API endpoints
- User, PowerUser, and SuperUser role foundation
- tenant-scoped document ownership and resource authorization
- owner-scoped chunk, embedding, vector search, and RAG access
- Azure Application Insights integration
- OpenTelemetry instrumentation
- .NET User Secrets for local secure configuration

## Architecture Overview

The current architecture is designed to remain modular and extensible as the platform grows from a secure retrieval foundation into a broader AI platform.

### Core architecture layers

- API Layer: controllers for documents, ingestion, processing, search, and RAG workflows
- Identity Layer: Microsoft Entra ID and JWT bearer authentication
- Application Layer: services for retrieval, context creation, prompt assembly, and user context resolution
- Data Layer: PostgreSQL with EF Core, pgvector, users, roles, user-role assignments, and tenant-owned documents
- Observability Layer: Azure Application Insights and OpenTelemetry
- Configuration Layer: appsettings, environment variables, and User Secrets

## Basic RAG Architecture

The current retrieval flow is intentionally simple, deterministic, and vendor-neutral:

```text
User Question
    ↓
Vector Search
    ↓
Chunk Retrieval
    ↓
Context Builder
    ↓
Prompt Builder
    ↓
Response
```

This pattern validates the RAG foundation before introducing more advanced AI model orchestration, Azure AI Foundry integration, or external agent frameworks.

## Authentication and Authorization Architecture

Authentication uses Microsoft Entra ID and the OAuth 2.0 Authorization Code Flow with PKCE:

```text
Microsoft Entra ID
    ↓
JWT Bearer Authentication
    ↓
Current User Context
    ↓
Users Table
    ↓
Protected API Endpoints
```

Authorization is implemented locally using the application RBAC model:

```text
Users Table
    ↓
UserRoles Table
    ↓
Roles Table
    ↓
Authorization Policies
    ↓
OwnerUserId and Tenant-Scoped Resource Access
```

Authorization is intentionally separated from the identity provider. This keeps the application authorization model independent of the authentication provider and allows future support for Microsoft Entra ID, Google Identity, Auth0, Keycloak, or local authentication without changing that model.

An optional future enhancement may synchronize Entra groups and map them to application roles.

## Validated Architecture

Validation was completed using two separate Microsoft Entra ID users.

User A:

- Authenticated via OAuth 2.0 Authorization Code Flow with PKCE
- Created document `USER_A_PRIVATE_DOCUMENT`
- Successfully retrieved the owned document

User B:

- Authenticated via OAuth 2.0 Authorization Code Flow with PKCE
- Successfully accessed protected endpoints
- Could not see User A documents

Validation results:

- ✅ Authentication works with Microsoft Entra ID
- ✅ Access tokens are validated correctly
- ✅ Automatic user bootstrap works
- ✅ `OwnerUserId` ownership model works
- ✅ Resource isolation works
- ✅ RBAC enforcement works
- ✅ Multi-user authorization model validated

Test evidence:

`GET /api/documents`

- User A returned the owned document.

`GET /api/documents`

- User B returned an empty result set.

Expected behavior was confirmed.

## Technology Stack

### Core technologies

- ASP.NET Core 10
- C#
- PostgreSQL
- Entity Framework Core
- Npgsql
- pgvector
- Microsoft Entra ID
- JWT Bearer Authentication
- Swagger / OpenAPI
- HtmlAgilityPack
- Azure Application Insights
- OpenTelemetry
- .NET User Secrets

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
- abstraction for future embedding providers
- vector-ready schema foundation

### Phase 7 - Vector Search Foundation

- pgvector support in PostgreSQL
- vector storage in `DocumentEmbeddings`
- similarity search capability
- Top-K retrieval flow
- ranking and metadata support

### Phase 8 - Observability Foundation

- Azure Application Insights integration
- OpenTelemetry tracing and metrics
- structured logging readiness
- request and dependency telemetry
- database and search observability hooks

### Phase 9 - Basic RAG Foundation

- end-to-end retrieval pipeline foundation
- vector search retrieval over stored chunk embeddings
- context assembly for grounded question answering
- prompt generation using question + retrieved context
- RAG request telemetry for duration, chunk count, context size, and prompt size
- vendor-neutral foundation for later AI model integration

### Phase 10A - Microsoft Entra ID Foundation

- Microsoft Entra ID app registration integration
- JWT bearer authentication enforcement
- user bootstrap from validated claims
- current user context resolution
- local `Users` table for authenticated identity mapping
- protected API access across core endpoints

### Phase 10B - Authorization & RBAC

- Local application roles: `User`, `PowerUser`, and `SuperUser`
- `Roles` and `UserRoles` tables with baseline role assignment
- Policy-based and resource-based authorization
- Document ownership and tenant isolation
- `OwnerUserId` filtering across document access and document-derived retrieval
- Entra-authenticated owner isolation validated with two users

## API Capabilities

The current API exposes the implemented document processing, retrieval, and secure access foundation.

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

## Database Architecture

The platform uses PostgreSQL as its primary operational database and relies on EF Core migrations for schema evolution.

### Current tables

- Documents
- DocumentMetadata
- DocumentChunks
- DocumentEmbeddings
- Users
- Roles
- UserRoles
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

#### Users
Stores application-level identity records mapped to Microsoft Entra ID subjects, including the Entra object ID, tenant identifier, email, username, lifecycle metadata, and last login timestamp.

#### Roles and UserRoles
Stores the seeded application roles and each user's role assignments. Newly bootstrapped users receive the baseline `User` role.

Documents now carry tenant and owner identifiers. Existing documents are assigned to the configured single tenant during migration; their owner remains unset because historical ownership cannot be reliably inferred. Baseline users cannot access those legacy documents until a trusted operator assigns an elevated role or otherwise provisions access.

### Database characteristics

- PostgreSQL-first persistence model
- EF Core relationships between documents, metadata, chunks, embeddings, and users
- pgvector extension enabled for vector storage and retrieval readiness
- migration-driven schema evolution
- identity mapping from Entra claims to application user records

## Observability & Monitoring

The observability layer is a foundational enterprise capability for the project.

### Azure Application Insights

- Application Insights is configured for telemetry collection
- connection strings are managed through User Secrets, not source-controlled configuration files
- telemetry is guarded so local development can run without a configured connection string

### OpenTelemetry

- ASP.NET Core instrumentation enabled
- HttpClient tracing enabled
- SQL instrumentation enabled
- metrics collection enabled
- support for future OTLP exporter configuration

### Telemetry coverage

- API request and response telemetry
- latency tracking
- database operation visibility
- vector search monitoring
- embedding workflow telemetry
- structured logging readiness

## Security & Configuration

### User Secrets

The solution uses .NET User Secrets for local secret storage, including:

- `ConnectionStrings:DefaultConnection`
- `ApplicationInsights:ConnectionString`
- Azure AD / JWT configuration values for local development

### Environment-based configuration

The application supports configuration from:

- appsettings files
- environment variables
- .NET User Secrets

### Current authentication foundation

The platform now includes a working identity foundation based on Microsoft Entra ID and JWT bearer authentication.

- Microsoft Entra ID is the identity provider for API clients
- JWT bearer tokens are validated in ASP.NET Core
- application users are bootstrapped from validated claims
- the current user context is resolved per request
- protected endpoints enforce authentication before business processing begins

Authorization is database-backed and resource-based. Users can access their own documents; PowerUsers and SuperUsers can manage documents in their tenant. Tenant boundaries apply independently of role. Conversation history and audit logging remain planned for later phases.

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
- ✅ Phase 10A - Microsoft Entra ID Foundation
- ✅ Phase 10B - Authorization & RBAC

### Planned

- ⬜ Phase 10C - Conversation History
  - Conversations
  - Messages
  - Context Retention
  - Chat History
- ⬜ Phase 10D - Audit & Governance
  - Audit Logs
  - Security Events
  - Access Tracking
  - Governance Foundation
- ⬜ Phase 11 - Azure AI Foundry Integration
- ⬜ Phase 12 - Copilot Studio Integration
- ⬜ Phase 13 - GraphRAG
- ⬜ Phase 14 - Administration Portal
- ⬜ Phase 15 - AI SDLC, MLOps & LLMOps
  - Prompt Registry
  - Model Registry
  - Evaluation Pipelines
  - Cost Tracking
  - AI Governance
  - AI Observability

## Azure AI Foundry Vision

Azure AI Foundry is the natural next integration point for the platform once the retrieval, identity, and governance foundations are mature.

### Planned Foundry role in the platform

- embedding model integration
- chat model integration
- orchestration for AI workflows
- evaluation and governance layering
- centralized AI telemetry and monitoring

## Copilot Studio Vision

Copilot Studio can eventually act as a user-facing orchestration layer that consumes the platform’s APIs and business knowledge services.

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL with pgvector enabled
- Docker (optional for local PostgreSQL provisioning)
- Git
- Azure Application Insights connection string for telemetry-enabled local development
- Microsoft Entra ID tenant and app registration values for authenticated local development

### Restore dependencies

```bash
dotnet restore
```

### Apply database migrations

```bash
dotnet ef database update
```

### Run the application

```bash
dotnet run
```

### Access Swagger

Open the local Swagger UI in the browser once the application is running.

## Notes

This repository is intentionally aligned with the implemented codebase and the validated roadmap, rather than an aspirational or overly broad AI platform plan. The current solution is production-oriented for a portfolio project while remaining intentionally scoped to the phases that have been completed and validated.