# Phase 10 - Identity, Authorization, Conversations, and Audit

Phase 10 consists of subphases 10A-10D. The repository README and Postman README are the main validation sources.

## Implementation summaries and decisions

### 10A - Microsoft Entra ID

JWT bearer authentication, current-user resolution, user bootstrap, and protected API access were implemented. The README records OAuth 2.0 Authorization Code Flow with PKCE and delegated `access_as_user` validation. Commit: `95ec956`.

### 10B - Authorization and RBAC

Local `User`, `PowerUser`, and `SuperUser` roles, resource ownership, tenant boundaries, and policy/resource-based authorization were added. Owner filtering applies to document-derived retrieval. Two-user validation confirmed User B cannot access User A's document. Commit: `cf8dff0`; migration: `20261105124743_Phase10B_RBACAndDocumentOwnership`.

### 10C - Conversation history

Conversations are user-owned; messages belong to conversations and are returned chronologically. The API assigns the `User` role to public user messages; trusted services own assistant/system messages. Ownership is checked in server-side queries, and a non-owner receives the same `404` as for a missing conversation. Commit: `3be3467`; migration: `20261109114211_Phase10CConversationHistory`.

### 10D - Durable audit foundation

Tenant-aware audit events are stored in PostgreSQL separately from Application Insights/OpenTelemetry. Business mutation events are added to the same EF save as their mutations. Content such as prompts, document text, messages, and model responses is excluded from audit/telemetry records. Commit: `6a799ad`; migrations: `20261110105824_Phase10DAuditFoundation` and `20261110111605_AddVectorSearchAndRagAuditMetadata`.

## Validation evidence

- The README records Entra token validation, protected API calls, and a two-user document ownership test.
- The README and Postman guide record User A conversation/message access and User B denial (`404`) for User A resources.
- Phase 10D validation records success/denial event persistence and expected target/outcome data.
- Postman source: `postman/README.md`; collection/environment templates are in `postman/`.

## Limitations

The README explicitly notes that Phase 10D does not include an audit-query API, retention/purge processing, or governance reporting. Those were deferred to later administration/governance scope. No independent Phase 10 retrospective or consolidated test-run transcript was found.