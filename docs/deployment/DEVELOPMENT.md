# Development setup

Run commands from the repository root. Prerequisites: .NET 10 SDK, a local SQL Server instance, and an account with the permissions needed for the intended local setup. EF Core packages and the local dotnet-ef tool are pinned to 10.0.12.

## Local configuration

The API reads `ConnectionStrings:SIAMIS`. The tracked base configuration targets LocalDB; the approved local Development instance is **Ray**, database **SIAMIS**, reached via `localhost` with Windows integrated authentication. A connection alias is not proof of server identity. Verify effective configuration and SQL identity before any intentional database operation. Development overrides are local and ignored.

Use `src/SIAMIS.Api/appsettings.Development.json`, the existing API user-secrets store, or environment configuration. An equivalent local-only connection string is:

```text
Server=localhost;Database=SIAMIS;Trusted_Connection=True;TrustServerCertificate=True;
```

Keep credentials out of tracked files. The project already has a UserSecretsId; no initialization command or project edit is needed. TrustServerCertificate is a local Development setting and does not establish a Production certificate policy.

## Build and run

```powershell
dotnet restore .\SIAMIS.sln
dotnet build .\SIAMIS.sln -c Release
dotnet run --project .\src\SIAMIS.Api\SIAMIS.Api.csproj --launch-profile SIAMIS.Api
```

The profile selects Development and listens at `https://localhost:7142` and `http://localhost:5142`. `/swagger` exposes Development API documentation; `/health` reports application status, not database readiness. `/` contains the original static AdminLTE starter, not completed HR screens.

The reported approved Development baseline is **46 applied migrations, zero pending**, through `20261009104602_AddEmployeeRegistrationIdempotency`; Stage 3 does not refresh that database evidence. For a new developer database, review the target/history and perform initialization as a separate explicitly approved setup action. Startup does not replace migration review. See [database safety](../architecture/DATABASE.md).

## Authentication and private documents

Protected routes require Identity authentication and the established CSRF/session workflow. There is no shared default administrator password. Initial administrator provisioning uses the existing explicit `--bootstrap-admin` workflow with securely supplied Bootstrap configuration; review [AdminBootstrap](../../src/SIAMIS.Api/Security/AdminBootstrap.cs) before using it. It is a database-mutating setup action, not a routine startup command.

Development credential delivery is opt-in via `Security:EnableDevelopmentCredentialDelivery`; it is not a Production delivery provider. Private documents default to the API's private `App_Data/hr-documents` directory in Development. A configured `PrivateDocuments:Root` must be absolute and outside the public web root; keep bytes and local settings untracked.

Photos use separate `EmployeePhotos:Root`; Development defaults to the API's `App_Data/employee-photos`. It must be absolute, outside the web root and separate from HR documents. Production has no implicit photo-storage default. Keep private bytes untracked; do not relocate/delete them as cleanup. Coordinate SQL, private-binary and Data Protection key backups.

Six existing QA personas and two review/helper accounts are intentional local records, not Production seeds or default credentials. Use authenticated web User Accounts; do not run the superseded `tools/development/Provision-QaPersonas.ps1`, read old credential files or reset accounts for convenience. Initial bootstrap remains an explicit operator decision.

See [frontend setup](../frontend/README.md) for Node/npm, TLS proxy and commands. Default port is 5173; current review uses 5175.

## Regression safety

The default `dotnet run --project .\tests\SIAMIS.Payroll.RegressionTests -c Release` executes database-free assertions. Optional SQL-backed modes create fixtures and need explicit disposable-database configuration; inspect the runner before selecting a mode. Do not test registration, photos, deletion, clocking or payroll by modifying live Development records. [Release notes](../releases/v0.5.1.md#verification-evidence) distinguish prior results from fresh checks.

## Read-only model consistency check

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project .\src\SIAMIS.Infrastructure --startup-project .\src\SIAMIS.Api --configuration Release --no-build -- --environment Development
```

This checks model consistency; it does not apply migrations. Existing regression scripts create/clean fixtures and require their established setup. They were not reorganized or run for documentation cleanup.
