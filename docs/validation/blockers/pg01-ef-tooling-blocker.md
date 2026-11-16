# PG-01 Blocker — EF Tooling Blocked by Windows App Control

## Status

**Open — PG-01 migration execution is blocked by workstation App Control.** PostgreSQL and pgvector are available, and migration source files/history were inspected. A newly generated .NET 10 console application also fails to run under the current policy, showing that the issue is not specific to RagPlatform. Fresh and upgrade migrations have not been executed. No application code or security policy was changed.

This task is limited to resolving the design-time EF tooling blocker and completing PG-01. It does not authorize proceeding to PG-02.

## Observed symptoms

- `dotnet ef migrations list --project .\RagPlatform.Api\RagPlatform.Api.csproj --startup-project .\RagPlatform.Api\RagPlatform.Api.csproj --no-connect` builds the API successfully, then fails while loading `RagPlatform.Api.dll` with `0x800711C7` (`An Application Control policy has blocked this file`).
- A newly created .NET 10 console application created with `dotnet new console` also fails to run with `An Application Control policy has blocked this file`. This is a separate, non-RagPlatform application and demonstrates that the workstation policy affects locally generated .NET applications beyond this repository. The probe's exact path and timestamp were not supplied, so it has not yet been matched to a specific Code Integrity event.
- The error occurs during EF design-time assembly discovery, before any database connection or migration operation.
- The API DLL is unsigned. No `Zone.Identifier` stream was present on that file.
- PostgreSQL startup/readiness and pgvector were verified separately: PostgreSQL 16.15 accepts connections and the existing `ragplatform` database has pgvector 0.8.7.
- The existing `ragplatform` database is not fresh: it has 11 public tables and the first 14 migration IDs applied through `20261112090000_Phase12DocumentImportFoundation`. It was not modified and must not be used as the fresh-database target.

## Evidence collected

| Evidence | Observation |
| --- | --- |
| Code Integrity Operational event 3077 | At `2026-11-16 11:19:22`, `dotnet.exe` was blocked from loading the API DLL because it did not meet Enterprise signing-level requirements or violated code-integrity policy. |
| Enforced App Control policy | `VerifiedAndReputableDesktop`, Policy ID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`; the policy was identified as Windows Smart App Control. |
| Independent .NET probe | A newly generated .NET 10 console application fails with the same Application Control block message. This confirms the problem is not confined to the RagPlatform repository; its exact Code Integrity event and policy ID have not been correlated. |
| Test host correlation | On `2026-11-15`, Code Integrity events 3077/3033 show `testhost.exe` blocked from loading `RagPlatform.Api.dll`, and `vstest.console.exe` blocked from loading `RagPlatform.Api.Tests.dll`, under the same policy ID. |
| Device Guard | `CodeIntegrityPolicyEnforcementStatus=2` and `UsermodeCodeIntegrityPolicyEnforcementStatus=2`, indicating enforcement. |
| Smart App Control | `Get-MpComputerStatus` reports `SmartAppControlState=On`. |
| Assembly signature | `Get-AuthenticodeSignature` reports `NotSigned`. |
| Mark-of-the-Web | No `Zone.Identifier` alternate data stream was found. |
| AppLocker | No matching AppLocker block event for this DLL was found. |
| Policy details | The earlier non-elevated `CiTool -lp -json` call returned access denied. Subsequent active-policy review identified the enforced policy as `VerifiedAndReputableDesktop` (Windows Smart App Control). |
| EF build stage | EF tooling completed the project build, then failed loading `RagPlatform.Api.dll`; build success does not establish migration execution. |

## Root-cause assessment

Windows Smart App Control, enforcing the `VerifiedAndReputableDesktop` policy, is the confirmed cause of the EF design-time load failure. The independent .NET console probe confirms that the enforcement affects locally generated applications beyond RagPlatform; the test-host events also show the same policy blocking both the test host's application dependency and test assembly. This does not prove that every .NET application is blocked.

The system reports `SmartAppControlState=On`, and the active-policy review identifies the enforced policy as `VerifiedAndReputableDesktop` with the same GUID recorded in the Code Integrity events. The independent console probe has not been correlated to its own event, so its specific event record is not available; its failure message nevertheless matches the workstation-wide App Control block.

This is not indicated to be Mark-of-the-Web or an application-specific .NET trust failure. The operating system's code-integrity policy denies locally generated .NET artifacts when their host attempts to load them. No evidence currently points to RagPlatform code as the cause.

## Required administrator actions

A local administrator should:

1. Record the identified `VerifiedAndReputableDesktop` Smart App Control policy as the source; no application-specific RagPlatform rule has been identified.
2. Select a supported development environment or signing workflow. Smart App Control does not provide a per-application bypass; do not assume a supplemental allow rule can be added to this policy.
3. If any system or policy change is chosen, approve and document its scope and security impact before applying it. No such change is authorized by this assessment.
4. Confirm the selected environment permits the required locally compiled .NET artifacts before rerunning EF tooling.

## Proposed resolution options

1. **Use a trusted signed validation build.** Sign all executable artifacts that must be loaded, including relevant DLLs, with a certificate accepted by Smart App Control. Confirm signing and reputation requirements; strong-name signing or an untrusted self-signed certificate is not sufficient evidence of acceptance.
2. **Use a dedicated development VM/workstation with an intentionally configured App Control for Business policy.** Establish and test the policy for the development workflow; do not try to add a per-app exception to Smart App Control or create broad user-writable path rules.
3. **Run EF validation on an approved runner/workstation.** Use a controlled development or CI environment whose policy permits the repository's design-time assembly, and connect only to isolated disposable PostgreSQL databases.
4. **Turn Smart App Control off only as a deliberate device-wide choice.** Windows Security provides this setting, but it reduces protection for all apps on that device; it is not an app-specific exception and is not recommended as the default validation path.

Do not use `Unblock-File`, move the DLL to another directory, rename it, or attempt to bypass Code Integrity. No source-code change is indicated by the collected evidence.

## Validation impact

Until this workstation-policy blocker is resolved:

- EF migration discovery through `dotnet ef` cannot be verified in this environment.
- Fresh-database and upgrade-database migration tests cannot be run here.
- The migration source filenames and existing database history provide a manual chain check only; they do not prove that the migration assembly loads, SQL is generated, or migrations apply successfully.
- The existing non-fresh `ragplatform` database must remain untouched. Create dedicated disposable databases for PG-01 after EF tooling is permitted.
- PostgreSQL startup and pgvector availability passed, but that is only a prerequisite check, not a PG-01 migration pass.

## PG-01 execution after resolution

Run only after the administrator confirms the approved fix is active:

```powershell
dotnet ef migrations list --project .\RagPlatform.Api\RagPlatform.Api.csproj --startup-project .\RagPlatform.Api\RagPlatform.Api.csproj --context RagPlatform.Api.Data.ApplicationDbContext --no-connect
```

Then use separate disposable databases for the fresh and upgrade paths. The current Compose PostgreSQL credentials are for local testing only:

```powershell
docker exec ragplatform-postgres createdb -U postgres ragplatform_pg01_fresh
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=ragplatform_pg01_fresh;Username=postgres;Password=postgres"
dotnet ef database update --project .\RagPlatform.Api\RagPlatform.Api.csproj --startup-project .\RagPlatform.Api\RagPlatform.Api.csproj --context RagPlatform.Api.Data.ApplicationDbContext
```

For the upgrade path, create a second disposable database, update it first to `20261112090000_Phase12DocumentImportFoundation`, then update to the latest migration. Record the migration history and pgvector extension version in each database. Do not drop or reset the existing `ragplatform` database.

## Exit criteria

Close this blocker and mark PG-01 passed only when all criteria below are evidenced:

1. The local administrator identifies the active policy and approves a remediation; EF tooling can load the API assembly without a Code Integrity block.
2. EF migration discovery lists the complete current migration chain successfully.
3. A fresh disposable database migrates to the latest revision without manual schema repair.
4. A separate disposable database upgrades from the documented Phase 12 baseline to the latest revision without manual repair.
5. Both databases show all expected migration IDs in order, and the `vector` extension is installed with its version recorded.
6. Schema inspection confirms expected objects/constraints; logs record command output, PostgreSQL/pgvector versions, database aliases, migration IDs, and reviewer approval without credentials or application data.

Until these criteria pass, keep PG-01 **Blocked** and do not continue to PG-02.
