# SIAMIS School Management System

SIAMIS is an API-first school management platform. Current development is the HR module; future school, admissions, finance and learning modules follow the [official roadmap](docs/ROADMAP.md).

## Current status

**v0.5.1 — HR Foundation & Employee Management** is a release candidate, not a Production deployment. Authentication-refresh remediation (Stage 1) and deterministic frontend installation (Stage 2) are approved. Stage 3 documentation/artifact reconciliation awaits review; final release verification and owner Git operations remain outstanding.

Implemented: premium V3 shared UI, authenticated shell, HR Dashboard, Employee Directory/360, employment/account lifecycle, User Accounts, Attendance Management and employee clocking. Employee numbering, private photos, controlled deletion and registration idempotency extend the historical HR V1 foundation. See [HR status](docs/modules/hr/README.md) and [release notes](docs/releases/v0.5.1.md). F5.2 QR credentials are planned, not implemented.

## Technology and architecture

C#, .NET 10, ASP.NET Core Web API, EF Core 10, SQL Server and ASP.NET Core Identity. Frontend: React, TypeScript, Vite, Tailwind CSS, Radix UI, Lucide, TanStack Query, React Hook Form and Zod. Exact versions remain in project manifests and the repaired frontend lockfile.

| Directory                   | Responsibility                                           |
| --------------------------- | -------------------------------------------------------- |
| `src/SIAMIS.Api`            | HTTP routing, authentication and authorization wiring    |
| `src/SIAMIS.Application`    | DTOs, contracts and interfaces                           |
| `src/SIAMIS.Domain`         | Domain entities                                          |
| `src/SIAMIS.Infrastructure` | EF persistence and service implementations               |
| `tests/`                    | Established regression runner and verification utilities |
| `frontend/`                 | React application and isolated Development showcase      |
| `docs/`                     | Current guides and historical engineering evidence       |

Controllers use application interfaces; EF entities are not API responses. See [architecture](docs/architecture/SYSTEM-ARCHITECTURE.md).

## Development startup

Install .NET 10 and configure local SQL Server using the [Development guide](docs/deployment/DEVELOPMENT.md).

```powershell
dotnet restore .\SIAMIS.sln
dotnet build .\SIAMIS.sln -c Release
dotnet run --project .\src\SIAMIS.Api\SIAMIS.Api.csproj --launch-profile SIAMIS.Api
```

API: `https://localhost:7142` / `http://localhost:5142`; Development Swagger: `/swagger`. `/health` reports application status, not database readiness. Database setup and account bootstrap require separate explicit approval. Follow [frontend setup](docs/frontend/README.md) for the React app, normally port 5173; current review uses 5175.

## Documentation

- [Documentation index](docs/README.md)
- [Development](docs/deployment/DEVELOPMENT.md) and [Production requirements](docs/deployment/PRODUCTION.md)
- [Historical HR V1 freeze](docs/modules/hr/HR-V1-FREEZE.md)
- [Historical checkpoints](docs/archive/checkpoints/README.md)

The API's original static AdminLTE landing page remains present; it is not the React product UI. Changing frozen business semantics requires a separately approved checkpoint.
