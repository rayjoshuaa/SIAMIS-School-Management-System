# Development setup

Run commands from the repository root. Prerequisites: .NET 10 SDK, a local SQL Server instance, and an account with the permissions needed for the intended local setup. EF Core packages and the local dotnet-ef tool are pinned to 10.0.12.

## Local configuration

The API reads `ConnectionStrings:SIAMIS`. The tracked base configuration targets LocalDB; the established Development environment targets `localhost` / `SIAMIS` with Windows integrated authentication. Development overrides are local and ignored by Git. Verify the effective target before any intentional database operation.

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

The existing local SIAMIS database is already migrated. For a new developer database, review the local target and migration history and perform initialization as a separate explicit setup action. Startup does not replace migration review.

## Authentication and private documents

Protected routes require Identity authentication and the established CSRF/session workflow. There is no shared default administrator password. Initial administrator provisioning uses the existing explicit `--bootstrap-admin` workflow with securely supplied Bootstrap configuration; review [AdminBootstrap](../../src/SIAMIS.Api/Security/AdminBootstrap.cs) before using it. It is a database-mutating setup action, not a routine startup command.

Development credential delivery is opt-in via `Security:EnableDevelopmentCredentialDelivery`; it is not a Production delivery provider. Private documents default to the API's private `App_Data/hr-documents` directory in Development. A configured `PrivateDocuments:Root` must be absolute and outside the public web root; keep bytes and local settings untracked.

## Read-only model consistency check

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project .\src\SIAMIS.Infrastructure --startup-project .\src\SIAMIS.Api --configuration Release --no-build -- --environment Development
```

This checks model consistency; it does not apply migrations. Existing regression scripts create/clean fixtures and require their established setup. They were not reorganized or run for documentation cleanup.
