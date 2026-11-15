# Phase 13 Increment 5 - Conversation Persistence

## Scope

This increment connects RAG queries to the existing owner-scoped conversation store. A query may supply `ConversationId`; if omitted, the successful turn creates a conversation whose title is derived from the query. The response includes the resulting conversation ID.

## Ownership and tenant boundaries

Conversation records retain their owning `UserId`. Conversation and message reads, updates, and deletes now scope by both the authenticated application user and that user's tenant. A conversation ID belonging to another owner or tenant is returned as not found. The RAG flow checks ownership before retrieval and checks it again when persisting the turn. New conversations are owned by the active current user. Prior conversation content is not fetched or generated across tenant/owner boundaries.

## Turn and citation persistence

Each successful RAG request performs fresh authorized vector retrieval using only the current question. Prior assistant answers and prior citations are not supplied as retrieval evidence. The bounded history passed to answer generation consists only of prior user messages (up to six messages and 2,000 characters); it is for resolving follow-up references, not factual support. Current-turn retrieved context remains the only evidence source.

The user question, assistant answer, and validated citations are persisted together in one database save. Citations are snapshots attached to the assistant message and contain the server-validated citation number, persisted document/chunk identifiers, source name, page number, and safe excerpt. The API returns those stored citations when reading conversation messages. Snapshot rows intentionally do not require live document/chunk foreign keys, so conversation history remains readable if a source document is later removed.

If a request uses a new conversation, that conversation and the successful turn are created together after answer generation. Failed retrieval, generation, or citation validation does not persist a partial turn or create an empty conversation.

## Retention and deletion design

The existing owner-authorized `DELETE /api/conversations/{id}` removes the conversation; database cascades remove its messages and their citation snapshots. This is immediate user-requested deletion and is audited. No automatic age-based retention period or purge worker is configured by this increment. Before enabling automatic expiry, select a tenant/legal retention period, make it configurable, define any legal-hold exceptions, and run a scheduled purge constrained by the same tenant/owner policy. Until then, conversation data is retained until owner deletion or an approved operational purge.

## Validation and limitations

The API build is the compile check. PostgreSQL migration application and integration tests must still verify atomic turn/citation writes, cascade deletion, and tenant/owner isolation. Live Foundry behavior, answer quality, retention-policy approval, and production purge operations remain separate acceptance work.
