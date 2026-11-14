# RAG Platform 2.0

A portfolio-grade enterprise AI platform built on ASP.NET Core, PostgreSQL, pgvector, Microsoft Entra ID, and modern observability tooling.

## Project Overview

RAG Platform 2.0 is a backend-first platform for ingesting, processing, storing, and retrieving organizational knowledge through a modern retrieval architecture. The solution is intentionally structured around phased delivery so each layer is implemented and validated before the next is introduced.

The project currently demonstrates:

- ASP.NET Core Web API
- PostgreSQL with EF Core
- pgvector storage and similarity search
- foundational document upload, processing, and chunking
- vector-based retrieval and context assembly
- Azure AI Foundry embedding and chat inference (validated)
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
- ✅ Phase 10C - Conversation History
- ✅ Phase 10D - Audit Foundation
- ✅ Phase 11 - Azure AI Foundry Integration

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
- owner-scoped conversation and message history
- durable audit events for authentication, user bootstrap, document and conversation lifecycle, denied resource access, vector search, and RAG execution
- Azure Application Insights integration
- OpenTelemetry instrumentation
- .NET User Secrets for local secure configuration

## Architecture Overview

The current architecture is designed to remain modular and extensible as the platform grows from a secure retrieval foundation into a broader AI platform.

### Core architecture layers

- API Layer: controllers for documents, conversations, ingestion, processing, search, and RAG workflows
- Identity Layer: Microsoft Entra ID and JWT bearer authentication
- Application Layer: services for retrieval, context creation, prompt assembly, and user context resolution
- Data Layer: PostgreSQL with EF Core, pgvector, users, roles, user-role assignments, tenant-owned documents, and user-owned conversations
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

Phase 11 connects this retrieval flow to Azure AI Foundry chat and embedding deployments. Agents, tool calling, Copilot Studio, and advanced orchestration remain outside the current implementation.

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

## Phase 10C - Conversation History

**Status:** ✅ Complete

**Validation:** ✅ Validated

Phase 10C adds user-owned conversation history. It reuses the active-user authentication policy and enforces conversation ownership in server-side queries. Conversation history is not yet connected to the RAG request pipeline.

### Conversation Entity

Each conversation belongs to one application user and contains:

- `Id`
- `Title`
- `UserId`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### ConversationMessage Entity

Each message belongs to a conversation and contains:

- `Id`
- `ConversationId`
- `Role` - `User`, `Assistant`, or `System`
- `Content`
- `CreatedAtUtc`

Messages are returned in chronological order. Public message creation assigns the `User` role server-side; `Assistant` and `System` roles are reserved for trusted application services.

### Ownership Model and User Isolation

The authenticated request is resolved to the local application user. Services apply `Conversation.UserId == currentUser.Id` when listing, retrieving, or deleting conversations. Message reads and writes first verify ownership of the parent conversation.

```text
Authenticated Active User
    ↓
Local Application User
    ↓
Conversation.UserId ownership filter
    ↓
Conversation Messages
```

- Conversations are owner-only, including for `PowerUser` and `SuperUser`; tenant-wide document permissions do not grant access to another user's conversation.
- The API derives `UserId` from the authenticated application user and does not accept it from the caller.
- A non-owner receives the same `404` response as for a missing conversation.
- Deleting a conversation cascades to its messages.

### API Endpoints

- `GET /api/conversations`
- `GET /api/conversations/{id}`
- `POST /api/conversations`
- `DELETE /api/conversations/{id}`
- `GET /api/conversations/{id}/messages`
- `POST /api/conversations/{id}/messages`

Endpoints use request and response DTOs; EF Core entities are not returned directly.

### EF Core Migration

Migration generated: `20261109114211_Phase10CConversationHistory`.

It adds the `Conversations` and `ConversationMessages` tables, the required conversation-to-user and message-to-conversation relationships, owner/message query indexes, UTC timestamp defaults, and a constraint for supported message roles. Apply it to the target development database before running the validation workflow:

```bash
dotnet ef database update
```

The migration has been applied to the validation database.

### Validation Results

Validation completed successfully using two separate Microsoft Entra ID users:

- ✅ User A created conversation `USER_A_CONVERSATION_TEST`.
- ✅ User A retrieved the owned conversation.
- ✅ User A created conversation messages.
- ✅ User A retrieved the owned messages.
- ✅ User B could not view User A conversations.
- ✅ User B received `404` when requesting User A's conversation by ID.
- ✅ User B received `404` when requesting User A's conversation messages.

### Acceptance Criteria

- ✅ All six endpoints require an authenticated active application user.
- ✅ Conversation ownership is derived from the current user and filtered server-side.
- ✅ User B cannot list or retrieve User A's conversation or its messages.
- ✅ Client-created messages are stored as `User`; callers cannot assign `Assistant` or `System`.
- ✅ API responses contain DTOs only.
- ✅ The migration is applied and the two-user workflow passes with separate Entra users.

The Postman requests are available in [`postman/`](postman/).

## Phase 10D - Audit Foundation

**Status:** ✅ Complete

**Validation:** ✅ Validated

Phase 10D adds durable, tenant-aware audit events to PostgreSQL. Audit events are written separately from Application Insights and OpenTelemetry telemetry. Business mutation events are added to the same EF Core save as their corresponding data changes.

### Event Coverage

- `AuthenticationTokenValidated` is written when the API validates a bearer token. This records authenticated API activity, not an interactive Microsoft Entra ID sign-in.
- `UserBootstrapped` is written when a local application user is first created.
- `ActiveUserAuthorizationDenied` records a validated identity that does not resolve to an active application user.
- `DocumentCreated` covers document creation, text upload, and web content import.
- `DocumentUpdated` and `DocumentDeleted` record document changes.
- `ConversationCreated` and `ConversationDeleted` record conversation changes.
- `DocumentAccessDenied` and `ConversationAccessDenied` record denied resource access attempts.
- `VectorSearchExecuted` records success or failure with duration, input character count, requested `topK`, result count, and average similarity.
- `RagQueryExecuted` records success or failure with duration, input character count, requested `topK`, retrieved result count, context character count, and prompt character count.

### Audit Event Data

Each event includes tenant ID, optional local actor user ID, event type, outcome, target type and ID, UTC timestamp, correlation ID, HTTP method, and request path. AI-operation events use typed numeric operational metadata only. Query text, prompt content, document content, conversation message content, and model responses are not written to audit events, telemetry, or application log messages.

### Database Migration

Generated migrations:

- `20261110105824_Phase10DAuditFoundation` creates `AuditEvents`, tenant/time and event/time indexes, and an optional actor-user relationship that preserves audit rows if a user is deleted.
- `20261110111605_AddVectorSearchAndRagAuditMetadata` adds typed operational metadata columns for vector search and RAG events.

Apply the migrations to the target database before validating:

```bash
dotnet ef database update
```

### Validation Results

Validation completed using two separate Microsoft Entra ID users:

- ✅ `AuthenticationTokenValidated`
- ✅ `ConversationCreated`
- ✅ `ConversationDeleted`
- ✅ `ConversationAccessDenied`
- ✅ Successful operations generated audit records.
- ✅ Denied access attempts generated audit records.
- ✅ Target resources were captured correctly.
- ✅ Outcomes (`Succeeded` / `Denied`) were captured correctly.
- ✅ Audit events were persisted to PostgreSQL.

Phase 10D establishes durable audit-event persistence; it does not include an audit-query API, retention/purge processing, or governance reporting. Audit investigation and governance dashboards are planned for Phase 16, while retention and purge policies remain future scope.

## Phase 11 - Azure AI Foundry Integration

**Status:** ✅ Complete

**Validation:** ✅ Successful

Phase 11 delivers the AI foundation by connecting the existing retrieval pipeline to Azure AI Foundry embedding and chat deployments. It validates model connectivity, embedding generation, vector storage, retrieval, and auditing. Foundational document APIs exist, but the complete business-document import and knowledge-base population pipeline is planned for Phase 12. Phase 13 will build out document Q&A over that populated corpus. Agents, tool calling, Copilot Studio, and Azure AI Search are not part of Phase 11.

### Implementation

- Uses Microsoft Entra credentials through `DefaultAzureCredential`; no Foundry API key is stored in configuration.
- Generates query and document embeddings with the configured Foundry embedding deployment.
- Filters vector retrieval to embeddings generated by the configured deployment, preventing comparisons across incompatible embedding spaces.
- Uses the existing document ownership and tenant scoping during retrieval.
- Instructs the chat model to rely only on retrieved context, treat context and conversation history as untrusted data, and acknowledge when evidence is insufficient.
- Returns the answer and retrieved document/chunk identifiers without returning the assembled prompt or raw context.
- Accepts an optional owner-scoped `conversationId`; recent messages are supplied as conversation context, and successful exchanges are persisted as user and assistant messages.
- Records `FoundryModelInvocation` audit events for embedding and chat calls, including operation, deployment, duration, and available token counts. Audit records contain operational metadata only, not query, prompt, document, conversation-message, or model-response content.

### Architecture boundary

RagPlatform owns document records, chunks, embeddings, and vector search, with PostgreSQL and pgvector as its persistence and retrieval layer. Azure AI Foundry generates embeddings and LLM responses for inference; it is not used as document storage. Generated vectors are stored and searched by RagPlatform.

### Configuration

Configure these values through environment variables, user secrets, or deployment configuration:

| Setting | Description |
| --- | --- |
| `Foundry__Endpoint` | HTTPS Foundry resource OpenAI v1 endpoint ending in `/openai/v1` |
| `Foundry__ChatDeployment` | Name of the deployed chat model |
| `Foundry__EmbeddingDeployment` | Name of the deployed embedding model |
| `Foundry__EmbeddingDimensions` | Expected vector dimension returned by the embedding deployment (default: `1536`) |
| `Foundry__MaxOutputTokens` | Maximum chat completion output tokens (default: `800`) |

Example resource endpoint configuration:

```text
Foundry__Endpoint=https://<foundry-resource>.services.ai.azure.com/openai/v1
Foundry__ChatDeployment=<chat-deployment-name>
Foundry__EmbeddingDeployment=<embedding-deployment-name>
Foundry__EmbeddingDimensions=1536
Foundry__MaxOutputTokens=800
```

The chat and embedding settings must use the deployment names configured in Foundry. Entra ID authentication uses `DefaultAzureCredential` and the `https://ai.azure.com/.default` scope; no API key is stored in the application configuration.

Assign access to the identity that actually calls the endpoint, at the Foundry resource scope:

| Azure role | Purpose |
| --- | --- |
| `Foundry User` | Foundry resource and project user access for development and pre-deployed model workflows. |
| `Cognitive Services User` | Entra ID data-plane permission for inference calls to the Foundry Models resource endpoint. |

Project-only assignments or subscription-level `Owner` alone do not replace the required inference data-plane permission. Assign resource-scoped roles to the local developer identity or deployed managed identity as appropriate. See the [Foundry RBAC guidance](https://learn.microsoft.com/azure/foundry/concepts/rbac-foundry) and [keyless inference setup](https://learn.microsoft.com/azure/foundry/foundry-models/how-to/configure-entra-id).

### Embedding generation workflow

1. Create, upload, or import a document, then process it if needed.
2. Create chunks with `POST /api/documents/{documentId}/chunks`.
3. For each chunk, call `POST /api/chunks/{chunkId}/embeddings`. The API sends the chunk content to the configured Foundry embedding deployment, validates the returned dimensions, and stores the vector with its deployment name.
4. Regenerate embeddings for chunks created with placeholder or different deployments. Vector search only compares vectors generated by the configured deployment. `POST /api/rag/query` generates the query embedding automatically using that same deployment.

### Database migration and validation

The `Phase11FoundryInvocationAudit` migration adds provider, deployment, operation, and token-count metadata to audit events. Apply it before using the updated audit model:

```bash
dotnet ef database update
```

Phase 11 validation completed successfully:

- ✅ Entra ID authentication and Foundry inference succeeded with the configured resource endpoint.
- ✅ `text-embedding-ada-002` generated 1536-dimensional vectors, which were stored successfully.
- ✅ `Foundry User` and `Cognitive Services User` permissions were validated for the inference identity.
- ✅ The end-to-end `Document Chunk → Foundry Embedding → Vector Storage` flow was proven.
- ✅ `gpt-35-turbo` returned grounded answers with retrieved source identifiers through `POST /api/rag/query`.
- ✅ Vector search used embeddings from the configured deployment, while existing ownership and tenant scoping remained enforced.
- ✅ Optional conversation context remained owner-scoped.
- ✅ `FoundryModelInvocation`, `VectorSearchExecuted`, and `RagQueryExecuted` audit events were validated with operational metadata only; prompt, query, document, message, and model-response content were not stored.

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

### Phase 10C - Conversation History

- User-owned conversations and conversation messages
- Server-side ownership filtering for conversation and message endpoints
- DTO-based API responses
- Message roles: `User`, `Assistant`, and `System`
- Two-user Entra validation completed successfully

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

### Conversation history APIs

- GET /api/conversations
- GET /api/conversations/{id}
- POST /api/conversations
- DELETE /api/conversations/{id}
- GET /api/conversations/{id}/messages
- POST /api/conversations/{id}/messages

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
- Conversations
- ConversationMessages
- AuditEvents
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

#### Conversations and ConversationMessages
Conversations belong to one application user through a required `UserId` foreign key. Messages belong to a conversation and are removed when that conversation is deleted. Services filter conversation access by the current user's local application ID.

#### AuditEvents
Stores tenant-scoped authentication, bootstrap, resource lifecycle, and denied-access events with actor, target, timestamp, and request-correlation metadata. Audit records do not store document, message, or prompt content.

### Database characteristics

- PostgreSQL-first persistence model
- EF Core relationships between documents, metadata, chunks, embeddings, and users
- pgvector extension enabled for vector storage and retrieval readiness
- migration-driven schema evolution
- identity mapping from Entra claims to application user records
- owner-filtered conversation and message queries
- tenant-scoped audit-event persistence

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

Authorization is database-backed and resource-based. Users can access their own documents; PowerUsers and SuperUsers can manage documents in their tenant. Tenant boundaries apply independently of role. Conversation history and the Phase 10D audit foundation are implemented and validated.

Conversation access is owner-only and does not inherit tenant-wide document-management permissions. The existing active-user authentication policy is used, while conversation services enforce ownership in their database queries.

## Postman Test Suite

The Postman assets in [`postman/`](postman/) support the validated Phase 10A authentication, Phase 10B RBAC workflow, and Phase 10C conversation ownership checks.

### Environment

Import `postman/RagPlatform.environment.template.json` and use the environment named **RagPlatform Local**. It defines:

- `baseUrl` - defaults to `http://localhost:5172`
- `userAToken` - access token for User A; populate locally
- `userBToken` - access token for User B; populate locally
- `documentId` - set automatically after User A creates the test document
- `conversationId` - set automatically after User A creates a test conversation

The committed environment template contains no credentials. Keep tokens in a local Postman environment and never commit or export an environment after adding tokens.

### Authentication Requests

Acquire separate delegated access tokens for User A and User B using Microsoft Entra ID and OAuth 2.0 Authorization Code Flow with PKCE. The collection's **Get User A Token** and **Get User B Token** requests validate each token against the protected API after the corresponding environment variable has been populated. See [`postman/README.md`](postman/README.md) for the Postman OAuth settings.

### Document Requests

- **Get Documents User A** - lists documents visible to User A.
- **Get Documents User B** - lists documents visible to User B.
- **Create Document User A** - creates `USER_A_PRIVATE_DOCUMENT` and stores its identifier in `documentId`.

### Phase 10B Validation Workflow

User A:

1. Acquire User A's token.
2. Create `USER_A_PRIVATE_DOCUMENT`.
3. Verify the document appears in **Get Documents User A**.

User B:

1. Acquire User B's token.
2. Call **Get Documents User B**.
3. Verify User A's document is not returned.

Expected results:

- ✅ User A can access owned resources
- ✅ User B cannot access User A resources
- ✅ Resource ownership enforced
- ✅ RBAC validated

Authentication is provided by Microsoft Entra ID using OAuth 2.0 Authorization Code Flow with PKCE. Authorization is enforced by the application's local `Users`, `Roles`, and `UserRoles` model together with resource ownership. The two-user RBAC validation completed successfully.

### Phase 10C Validation Workflow

The collection includes requests to create a conversation and add a user message as User A, then verify conversation and message access as both users. Validation completed successfully: User A retrieved their conversation and messages, while User B could not list the conversation and received `404` for direct conversation and message retrieval.

## Development Roadmap

### ✅ Completed Foundations

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
- ✅ Phase 10C - Conversation History
- ✅ Phase 10D - Audit Foundation

### ✅ AI Foundation

- ✅ Phase 11 - Azure AI Foundry Integration
  - Azure AI Foundry connectivity
  - Embedding generation
  - Vector storage integration
  - Retrieval foundation
  - Audit instrumentation
  - Production validation completed

### 🚧 Knowledge Ingestion & Q&A

Document Import & Knowledge Ingestion extends the Phase 11 inference and vector-storage foundation with a managed path from PDF upload through extraction, chunking, embedding, and final status settlement. Tasks 1-9 are implemented, including completion auditing, poison-message handling, and import status reporting. PostgreSQL/pgvector integration and isolated Service Bus end-to-end rollout validation remain pending. The worker remains disabled by default until those gates are passed.

- 🚧 Phase 12 - Document Import & Knowledge Ingestion
  - ✅ Task 1: Blob Storage and Service Bus configuration, options, and dependency injection
  - ✅ Task 2: document import fields, EF Core mappings, migration, and model consistency
  - ✅ Task 3: PDF validation, private Blob staging, and queued Document creation
  - ✅ Task 4: publish a typed Service Bus import message after the document transaction commits
    - Validation: Blob Storage configuration completed; Service Bus configuration completed; PDF upload validation passed; Phase 12 migration applied; Service Bus message published successfully.
    - Evidence: `documentId` `53bb6d68-427e-4806-a39e-ad1d46a50710`; `document-import` active messages = `1`.
  - ✅ Task 5: consume import messages with an in-process `BackgroundService` and `ServiceBusProcessor` (opt-in; worker remains disabled by default)
  - ✅ Task 6: extract PDF text with PdfPig, persist success/audit state, and delete staging blobs after successful extraction
    - PdfPig 0.1.16 downloads by persisted `StagingBlobKey`, enforces the 25 MB limit, and verifies the downloaded size against `Document.FileSize`.
    - Encrypted, corrupt/unreadable, no-page, and no-text PDFs are classified as permanent failures; only these failures persist `Failed` with a permanent-failure audit event.
    - Extracted text, `UpdatedAt`, and the success audit event are persisted before staging-blob deletion. Cleanup failure is logged and measured; redelivery retries cleanup without repeating extraction. `StagingBlobKey` remains persisted.
    - The settlement policy permits dead-lettering only after persisted `Failed` status and matching audit evidence. Transient failures remain retryable; successful messages are settled only after the later chunk, embedding, and Task 9 completion checks.
    - Validation: solution build succeeds. Import processor tests using EF Core InMemory currently fail during model initialization because that provider cannot map `DocumentEmbedding.EmbeddingVector` (`Pgvector.Vector`); PostgreSQL-backed persistence validation remains pending.
    - Rollout gate: keep the worker disabled until PostgreSQL/pgvector and end-to-end settlement/retry validation on an isolated test queue are complete. Do not enable it against a shared or production-like queue.
  - ✅ Task 7: connect extracted text to the existing chunk-generation service
    - The trusted import entry point accepts a validated persisted document, requires `ImportStatus = Processing` and persisted non-empty content, and does not depend on a current HTTP user. API chunk generation continues to use the same `DocumentChunkingService` algorithm.
    - Chunk records use contiguous zero-based `ChunkIndex` values and a persisted `CharacterCount`. Redelivery validates and reuses a complete set; partial or inconsistent sets are rejected without automatic regeneration.
    - PostgreSQL `SELECT ... FOR UPDATE` serializes each document's check-and-create sequence. The existing unique index on `(DocumentId, ChunkIndex)` is a final guard; a uniqueness conflict triggers a savepoint rollback and reuses the concurrently persisted set only after validation.
    - Chunk records and the `DocumentChunksGenerated` audit event commit in one transaction. The event records chunk count and duration without chunk text, and redelivery does not duplicate it.
    - `DocumentChunking` telemetry records duration, success/failure, generated chunk count, and reused chunk count without logging document or chunk content. `ImportStatus` remains `Processing`; Task 7 does not generate embeddings or settle messages.
    - Validation: solution build succeeds. EF Core InMemory tests cannot initialize the full model with `Pgvector.Vector`; PostgreSQL/pgvector integration validation remains pending.
  - ✅ Task 8: connect persisted chunks to Foundry embeddings and PostgreSQL/pgvector persistence
    - The trusted import entry point reloads and validates the persisted document and complete chunk set without requiring an HTTP user. It uses the configured embedding provider/model and configured vector dimensions.
    - Embeddings are model-specific. Valid embeddings for the configured model are reused on redelivery; only missing embeddings are generated, and existing vectors are never overwritten. Empty or incorrectly dimensioned vectors are rejected.
    - Provider generation runs before the database transaction. PostgreSQL persistence locks the document row with `SELECT ... FOR UPDATE`, re-reads embeddings for the configured model, and inserts only rows still missing. The unique index on `(ChunkId, ModelName)` remains the final concurrency guard; a conflict rolls back and retries against persisted rows.
    - `DocumentEmbeddingsGenerated` is persisted in the same transaction as the embedding rows. It records provider/deployment, generated and reused counts, and duration, without document/chunk content or vector values. Redelivery does not create another success audit or duplicate batch-success telemetry.
    - Per-call embedding telemetry is retained. Batch telemetry records model, duration, success/failure, and generated/reused counts. Generation or persistence failure emits failure telemetry; persistence rollback leaves previously persisted chunks and chunk audits intact. At the Task 8 boundary, `ImportStatus` remains `Processing` and settlement is deferred; Task 9 performs finalization and completion settlement.
    - Validation: solution build succeeds. EF Core InMemory tests cannot initialize the full model with `Pgvector.Vector`; PostgreSQL/pgvector integration validation remains pending.
  - ✅ Task 9 implementation: complete import status transitions, status reporting, and message settlement
    - After chunk and embedding processing, the processor verifies persisted non-empty content, a valid chunk set with its success audit, and a complete configured-model embedding set with valid dimensions and its success audit.
    - Under a PostgreSQL document-row lock, `ImportStatus = Completed`, `UpdatedAt`, and the `DocumentImportCompleted` success audit are persisted in one transaction. Invalid or incomplete evidence prevents finalization.
    - Messages are completed only after persisted `Completed` status, the success audit, and completion evidence have been verified. A completed-document redelivery validates evidence, skips extraction/chunking/embedding work, and can then be completed without duplicating the audit.
    - Malformed or mismatched envelopes have explicit classifications and are dead-lettered with the safe reason `InvalidDocumentImportMessage`; envelope validation does not mark a document `Failed`. Transient failures remain retryable, and the existing permanent-PDF failure rule is unchanged.
    - `GET /api/documents/{id}/import-status` returns only `DocumentId`, `ImportStatus`, `CreatedAt`, and `UpdatedAt`. It reuses document read authorization and returns `404` for inaccessible documents.
    - Validation gate: solution build succeeds. PostgreSQL/pgvector integration testing and end-to-end settlement/retry/dead-letter testing on an isolated Service Bus queue remain pending. EF Core InMemory cannot map `Pgvector.Vector`, so its current test failures occur before assertions and are not acceptance validation.
    - Rollout gate: keep the worker disabled until the database and isolated-queue validation passes and the test suite can provide meaningful results.
  - Existing document list/delete APIs are available; staging-aware deletion and abandoned-blob cleanup remain to be completed
  - Require each imported document to include `DocumentId`, `Title`, `FileName`, `CreatedAt`, `UpdatedAt`, and `OwnerUserId`
  - Do not retain original PDFs as document assets; use Blob Storage only for temporary staging
  - Keep the existing solution structure: no dedicated worker project, outbox pattern, or major project split in this MVP
  - Defer OCR, SharePoint/OneDrive import, URL crawling, web crawler ingestion, document versioning, and automatic document refresh
- ⬜ Phase 13 - Document Q&A
  - Ask questions against imported documents
  - Source citations
  - Retrieval validation
  - Multi-document retrieval
  - Context-grounded responses

### 🤖 Copilot Integration

- ⬜ Phase 14 - Copilot Studio Integration
  - External Copilot integration
  - Business workflow orchestration
  - API consumption through Copilot Studio
  - Enterprise Copilot connectivity

### 🧠 Advanced Knowledge Layer

Extend retrieval beyond vector similarity to include explicit knowledge relationships.

- ⬜ Phase 15 - GraphRAG
  - Knowledge graph enrichment
  - Relationship-aware retrieval
  - Hybrid vector + graph retrieval

### 🏢 Enterprise Platform Layer

Add operator-facing capabilities for managing users, roles, governance, and audit investigations.

- ⬜ Phase 16 - Administration Portal
  - User administration
  - Role management
  - Governance dashboards
  - Audit investigation

### ⚙️ AI Engineering & Operations Layer

Introduce repeatable AI development, evaluation, governance, and operational cost controls.

- ⬜ Phase 17 - AI SDLC, MLOps & LLMOps
  - Prompt Registry
  - Model Registry
  - Evaluation Pipelines
  - Cost Tracking
  - AI Governance
  - AI Observability

## Azure AI Foundry Scope

The validated Foundry integration is an inference boundary. RagPlatform owns `Documents`, `DocumentChunks`, and `DocumentEmbeddings`, including PostgreSQL/pgvector persistence, vector search, and audit data. Azure AI Foundry provides embedding generation, chat completions, and model inference; it does not store documents, chunks, embeddings, or vectors. Embeddings returned by Foundry are persisted and searched by RagPlatform. Original PDF files are not retained as document assets in the Phase 12 MVP.

## Phase 12 Architecture

### Asynchronous import flow

```text
User uploads PDF
        ↓
API validates the upload
        ↓
Temporary Azure Blob Storage staging
        ↓
Create Document (Status = Queued)
        ↓
Azure Service Bus
        ↓
Background worker in RagPlatform.Api
        ↓
Download PDF from staging
        ↓
PDF text extraction with PdfPig
        ↓
Delete temporary Blob after successful extraction
        ↓
DocumentChunk generation
        ↓
Azure AI Foundry embedding generation
        ↓
Store DocumentEmbeddings in PostgreSQL/pgvector
        ↓
Import status = Completed
```

Import status is tracked as `Queued`, `Processing`, `Completed`, or `Failed`. Tasks 1-9 implement the API publish foundation and opt-in worker path: validated message consumption, PdfPig extraction, audit/telemetry, transactional chunking, model-specific embedding generation and PostgreSQL/pgvector persistence, final status transition, and completion settlement. The message includes `DocumentId`, `TenantId`, `OwnerUserId`, `StagingBlobKey`, and `CreatedAtUtc`. If publishing fails, the API logs and audits the failure, attempts to persist `Failed`, and returns a service-unavailable response. Completion status and audit persistence are implemented; end-to-end rollout validation remains pending. This MVP deliberately uses the existing `RagPlatform.Api` project and does not add a separate worker project or an outbox pattern.

Service Bus decouples the upload request from long-running PDF and model operations, so callers do not wait for extraction and embedding generation. Its use is also an intentional learning objective for Azure Service Bus, background processing, event-driven ingestion, and retry handling. The worker deletes the staging blob only after extracted text and its success audit event have been persisted successfully. A cleanup failure is measured and logged; redelivery retries cleanup without repeating extraction. Failed PDF imports retain their staging blob for diagnosis or retry policy, and an Azure Blob lifecycle policy can remove abandoned staging files. Extracted text and document metadata are stored by RagPlatform; the original PDF is not retained after successful extraction.

### Settlement policy and rollout gate

- The `ServiceBusProcessor` remains configured with `AutoCompleteMessages = false`, `MaxConcurrentCalls = 1`, and `PrefetchCount = 0`.
- A permanent PDF failure is eligible for dead-lettering only after the database confirms both `ImportStatus = Failed` and the matching permanent-failure audit event. If either is missing, the message remains unsettled and retryable.
- Malformed or invalid import envelopes are classified separately and dead-lettered with the fixed safe reason `InvalidDocumentImportMessage`; envelope validation occurs before document processing and does not mark unrelated documents `Failed`.
- Transient download, database, cancellation, and unexpected failures do not set `Failed`; they remain unsettled for retry. The worker does not abandon them explicitly.
- After processing, the worker verifies persisted `Completed` status, the `DocumentImportCompleted` audit, and valid content/chunk/embedding evidence before calling `CompleteMessageAsync`. Completed-document redelivery validates that evidence, skips import work, and completes the message without creating another completion audit.
- Because transient messages are intentionally left unsettled rather than explicitly abandoned, retries rely on Service Bus lock expiry and broker delivery limits. This behavior still requires isolated-queue validation.
- Keep `ServiceBus:DocumentImportWorkerEnabled` set to `false`. Do not enable the worker against a production-like or shared queue until PostgreSQL/pgvector integration and isolated-queue completion, redelivery, retry, and dead-letter validation are finished.

### Ownership boundaries

- **RagPlatform** owns document records and extracted text, chunks, embeddings, PostgreSQL/pgvector storage, vector search, import status, and audit data.
- **Azure Service Bus** transports import work notifications; it is not document or vector storage.
- **The in-process document import worker** runs in `RagPlatform.Api`; extraction, staging cleanup, transactional chunking, model-specific Foundry embedding persistence, final status transitions, and message settlement are implemented. The worker remains disabled pending rollout validation.
- **Azure AI Foundry** provides embedding generation and chat/model inference only.
- **Temporary Azure Blob staging** is not document storage. The worker deletes the blob after successful extraction; lifecycle cleanup handles abandoned staging files.

### Future Foundry capabilities

- orchestration for AI workflows
- evaluation and governance layering
- centralized AI telemetry and monitoring

## Future Document Ingestion Candidates

Potential extensions beyond the Phase 12 MVP include OCR, URL import, web crawling, SharePoint ingestion, OneDrive ingestion, and document versioning. These are candidates for future planning, not Phase 12 deliverables.

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