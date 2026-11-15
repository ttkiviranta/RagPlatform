# Project Documentation Archive

This archive consolidates phase documentation available in the repository as of 2026-11-15. It is an index and curated summary, not a claim that missing design records, test logs, or operational reports were recovered.

## Phase archive

- [Phase 01 - API Foundation](phases/phase01/README.md)
- [Phase 02 - PostgreSQL Foundation](phases/phase02/README.md)
- [Phase 03 - Document Management API](phases/phase03/README.md)
- [Phase 04 - Upload and Processing](phases/phase04/README.md)
- [Phase 05 - Web Ingestion and Chunking](phases/phase05/README.md)
- [Phase 06 - Embeddings](phases/phase06/README.md)
- [Phase 07 - Vector Search](phases/phase07/README.md)
- [Phase 08 - Observability](phases/phase08/README.md)
- [Phase 09 - Basic RAG](phases/phase09/README.md)
- [Phase 10 - Identity, Authorization, Conversations, and Audit](phases/phase10/README.md)
- [Phase 11 - Azure AI Foundry](phases/phase11/README.md)
- [Phase 12 - Document Import and Knowledge Ingestion](phases/phase12/README.md)
- [Phase 13 - Document Q&A](phases/phase13/README.md)

## Cross-cutting archive

- [Architecture decisions](architecture/decisions.md)
- [Validation status and evidence gaps](validation/acceptance-status.md)
- [Phase 12 worker rollout gate](rollout/phase12-worker.md)
- [Consolidated phase status report](reports/phase-status.md)

## Sources reviewed

1. `README.md` - the repository's primary project overview, architecture notes, implementation summaries, roadmap, validation statements, Phase 12 design, and Phase 13 Increment 1 note.
2. `postman/README.md` - authentication, two-user authorization, and conversation validation instructions.
3. `RagPlatform.Api/Data/Migrations/` - migration names used to retain schema-change references in the phase archive.
4. Git history - phase implementation and documentation commit subjects, retained as short commit references in the phase records.

The repository contains no other Markdown, reStructuredText, AsciiDoc, or text documentation files. No standalone Phase 1-9 completion reports, architecture decision records, validation plans, retrospectives, or lessons-learned reports were present. The available README summaries have been preserved; unavailable evidence is called out in each phase record rather than inferred.

The Postman README contains tenant/application identifiers used to configure OAuth. They are not repeated in this archive; use the existing local Postman setup and never add tokens or secrets to committed documentation.

## Archive conventions

- **Documented** means the information is stated in a repository source or represented by a migration/commit reference.
- **Not found** means no supporting artifact was present in the repository at archive time; it does not prove that the work or evidence never existed elsewhere.
- Validation is reported at the same strength as its source. A build result is not described as an integration or production validation result.
- Phase 13 captures the Q&A plan and current contract increment, but does not mark the complete Q&A capability accepted.