# SIAMIS

SIAMIS is a school management system built as an API-first ASP.NET Core application. The solution contains separate API, Application, Domain, and Infrastructure projects. The initial HR database foundation defines configurable master data and employee-related tables; employee workflows and API endpoints are not included yet.

## Requirements

- .NET 10 SDK
- SQL Server or SQL Server LocalDB to apply the migration and run database-backed features

The API can start without a running SQL Server because the foundation does not connect to the database at startup.

## Run locally

From this directory:

```powershell
dotnet restore .\SIAMIS.sln
dotnet build .\SIAMIS.sln
dotnet run --project .\src\SIAMIS.Api\SIAMIS.Api.csproj
```

The development launch profile serves the API at `https://localhost:7142` and `http://localhost:5142`. Open `/swagger` for API documentation, `/health` for a basic health response, or `/` for the AdminLTE starter page.

## HR database foundation

EF Core 10 maps the HR master data and employee foundation in `SIAMISDbContext`. The initial migration is `InitialHrDatabase` in the Infrastructure project. It seeds configurable master-data defaults only; it contains no employee records. The migration has been generated but not applied to a database.

The EF command-line tool is pinned in `.config/dotnet-tools.json`. Restore it with `dotnet tool restore` before using `dotnet ef` commands. To apply the migration, first configure a local SQL Server connection string as described below, then run:

```powershell
dotnet ef database update --project .\src\SIAMIS.Infrastructure\SIAMIS.Infrastructure.csproj --startup-project .\src\SIAMIS.Api\SIAMIS.Api.csproj
```

## Database configuration

The `SIAMIS` connection string is in `src/SIAMIS.Api/appsettings.json` and defaults to SQL Server LocalDB. Replace it with the connection string for your SQL Server instance when needed. For local secrets, use .NET user secrets rather than committing credentials:

```powershell
dotnet user-secrets init --project .\src\SIAMIS.Api\SIAMIS.Api.csproj
dotnet user-secrets set "ConnectionStrings:SIAMIS" "<your SQL Server connection string>" --project .\src\SIAMIS.Api\SIAMIS.Api.csproj
```

The web starter uses the AdminLTE 3.2 CDN distribution, which includes its Bootstrap 4 foundation. An internet connection is needed to load those assets in the browser.
