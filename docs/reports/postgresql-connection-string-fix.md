# PostgreSQL Connection-String Fix — Technical Debt Resolution

## Problem description

The PostgreSQL fallback configuration could produce an invalid connection string when the API was configured through `RAGPOSTGRES_*` environment variables. In particular, a supplied password was not included as its value, potentially preventing the API from authenticating with PostgreSQL.

## Root cause

The fallback connection string was assembled manually. Its password branch appended the literal `******;` instead of the configured password. Manual concatenation also did not safely encode passwords containing connection-string special characters.

## Affected configuration path

At startup, the API first reads `ConnectionStrings:DefaultConnection`. If that setting is empty or whitespace, it falls back to these process environment variables:

- `RAGPOSTGRES_HOST`
- `RAGPOSTGRES_PORT`
- `RAGPOSTGRES_DB`
- `RAGPOSTGRES_USER`
- `RAGPOSTGRES_PASSWORD`

A nonempty `ConnectionStrings:DefaultConnection` takes precedence and bypasses the fallback. If no password is supplied, the fallback leaves the password unset, preserving passwordless authentication configurations.

## Files changed

- `RagPlatform.Api/Program.cs` — builds the fallback connection string with `NpgsqlConnectionStringBuilder`, mapping host, port, database, username, and an optional password.
- `RagPlatform.Api.Tests/Services/PostgreSqlConnectionStringConfigurationTests.cs` — adds focused regression tests for configured connection-string precedence, fallback field mapping, passwords with special characters, and a missing password.

## Tests added

Four tests were added to cover:

1. `ConnectionStrings:DefaultConnection` precedence over fallback environment variables.
2. Mapping of fallback environment variables into the connection string.
3. Preservation of password values containing special characters.
4. Password omission when `RAGPOSTGRES_PASSWORD` is not set.

## Validation status

- Test project build: **successful**.
- Targeted test discovery: **4 tests found**.
- Targeted test execution: **blocked before assertions ran**; 0 passed and 4 failed to start because of the workstation application-control policy described below. This is not a passing test result.

## Smart App Control limitation

Smart App Control blocked loading the locally built `OpenTelemetry.Extensions.Hosting.dll` while the test host was starting (`FileLoadException`, `0x800711C7`). Consequently, the WebApplicationFactory-based tests could not execute in this workstation environment. The workstation security settings were not changed. Run the targeted tests in an approved execution environment to complete runtime validation.
