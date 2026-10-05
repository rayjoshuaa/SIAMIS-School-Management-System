# SIAMIS School Management System

SIAMIS is an API-first school management system. Its HR backend covers employment, leave, attendance, payroll, accounts and confidential HR documents. Future clients can use the same API.

## Current status

| Area | Status |
|---|---|
| HR | Backend V1 Frozen — authoritative Git tag: `hr-backend-v1` |
| School Management | Planned; next major backend module |
| Frontend | F1 React/TypeScript design-system foundation; application workflows and final shell deferred |
| Production | Deployment validation and infrastructure setup remain required |

## Technology and architecture

C#, .NET 10, ASP.NET Core Web API, EF Core 10, SQL Server, ASP.NET Core Identity, Swagger/OpenAPI and Git. The new frontend uses React, TypeScript, Vite, Tailwind CSS and Radix primitives. The original static AdminLTE starter remains historical and is not used by the new frontend.

| Directory | Responsibility |
|---|---|
| `src/SIAMIS.Api` | Controllers, HTTP contracts/routing, authentication and authorization wiring |
| `src/SIAMIS.Application` | DTOs, application contracts and interfaces |
| `src/SIAMIS.Domain` | Domain entities |
| `src/SIAMIS.Infrastructure` | EF Core persistence and service implementations |
| `tests/` | Established regression runner and verification scripts |
| `frontend/` | React frontend and Development design-system showcase |
| `docs/` | Current documentation and archived engineering evidence |

Controllers use application interfaces; EF entities are not API responses. See [system architecture](docs/architecture/SYSTEM-ARCHITECTURE.md).

## Development startup

Install the .NET 10 SDK and configure a local SQL Server connection using the [Development guide](docs/deployment/DEVELOPMENT.md). From the repository root:

```powershell
dotnet restore .\SIAMIS.sln
dotnet build .\SIAMIS.sln -c Release
dotnet run --project .\src\SIAMIS.Api\SIAMIS.Api.csproj --launch-profile SIAMIS.Api
```

The launch profile uses `https://localhost:7142` and `http://localhost:5142`. Swagger is available at `/swagger` in Development; `/health` reports application status. Protected API routes require authentication. Local account setup and database initialization are explicit setup steps, not part of starting the API.

## Documentation

- [Documentation index](docs/README.md)
- [Frontend setup and design system](docs/frontend/README.md)
- [Frozen HR V1 contract](docs/modules/hr/HR-V1-FREEZE.md)
- [Development](docs/deployment/DEVELOPMENT.md) and [Production requirements](docs/deployment/PRODUCTION.md)
- [Historical checkpoint archive](docs/archive/checkpoints/README.md)

Changing frozen HR business semantics requires a new explicitly approved checkpoint.
