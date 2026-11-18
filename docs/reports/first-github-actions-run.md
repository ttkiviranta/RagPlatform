# First GitHub Actions Run Investigation

## Run summary

| Item | Result |
|---|---|
| Workflow | `CI`, run #1 |
| Run outcome | **Failed** |
| Trigger | Push to `main` |
| Started | 2026-11-18 08:10 UTC |
| Duration | About 1 minute 10 seconds |
| Commit | `20091fe70742b09f95bd44c8f27e692437a07f6f` (`ci: add build and test workflow`) |
| Runner | GitHub-hosted `ubuntu-latest` |
| Run link | [GitHub Actions run #1](https://github.com/ttkiviranta/RagPlatform/actions/runs/44264798007) |

## Workflow outcome

The workflow completed its setup, checkout, .NET SDK setup, solution restore, local tool restore, and build steps successfully. The test step failed, so the overall workflow result was failure. Test-result publishing completed afterward.

## Build result

**Passed.** `dotnet build RagPlatform.slnx --configuration Release --no-restore` completed successfully.

## Test result and failing test

The test step failed on:

`DocumentChunkingImportTests.GenerateChunksForImportAsync_ReusesACompleteValidChunkSet`

The reported error was:

> The 'Vector' property 'DocumentEmbedding.EmbeddingVector' could not be mapped because the database provider does not support this type.

## Root cause analysis

The test created `ApplicationDbContext` with EF Core InMemory. The production model includes `DocumentEmbedding.EmbeddingVector`, whose type is `Pgvector.Vector`. EF Core InMemory does not support that PostgreSQL-specific type. EF validates the complete model when the context is used, so the test fails even though this scenario is not directly testing vector persistence.

This is a test-provider mismatch, not a missing production entity mapping: the production application configures Npgsql with pgvector support through `UseVector()`. A value converter would mask the provider mismatch rather than test the production database behavior.

## Why Smart App Control was no longer a blocker

The test ran on a GitHub-hosted Ubuntu runner, not on the local Windows workstation where Smart App Control had blocked test assembly loading. Smart App Control was therefore not the cause of this run's failure. The test reached EF Core model validation and failed with the provider-mapping error above; the local workstation policy was neither changed nor bypassed.

## CI Increment 2 recommendation

Separate provider-independent unit tests from database-dependent tests without removing or weakening assertions:

1. Categorize provider-independent tests as `Unit` and tests requiring the production database model as `PostgreSql`.
2. Keep the primary CI workflow limited to `Unit` tests.
3. Add a dedicated PostgreSQL integration workflow using a disposable `pgvector/pgvector:pg16` service.
4. Wait for PostgreSQL readiness, apply EF Core migrations, then run all `PostgreSql`-category tests against Npgsql with `UseVector()` enabled.
5. Publish TRX results and require the integration workflow as a CI check so database coverage is not silently dropped.
6. Serialize shared-database tests or otherwise isolate test data to prevent cross-test interference.

## PostgreSQL integration-test rationale

The application model contains PostgreSQL-specific pgvector types, and some import services also depend on PostgreSQL transactions and row-locking behavior. EF Core InMemory cannot represent these behaviors or validate this model. Running those tests against a disposable PostgreSQL/pgvector instance checks the same provider capabilities used in production while keeping provider-independent tests fast and database-free.

The first run was the Increment 1 build-and-test workflow; PostgreSQL service provisioning and migration execution were not part of that run. Increment 2 should add those as a separate, explicit validation path.
