# System architecture

SIAMIS is a .NET 10 API-first solution with four projects:

| Project | Established responsibility | Project references |
|---|---|---|
| API | HTTP controllers, DTO input/output, dependency registration and security pipeline | Application, Infrastructure |
| Application | Service interfaces, request/response contracts and DTOs | Domain |
| Domain | Entity models | None |
| Infrastructure | EF Core/SQL Server, Identity persistence, service/query implementations and private storage | Application, Domain |

The usual path is controller → application interface → infrastructure service → EF Core. Business workflows currently reside in service implementations; the Application project does not contain every business implementation. Controllers do not expose DbContext or return EF navigation graphs.

SQL Server migrations define persistence. Identity users and employees are separate concepts with an optional linkage. Private HR document bytes use a storage abstraction outside the public web root. See [database](DATABASE.md), [security](SECURITY.md) and [authorization](AUTHORIZATION.md).

The regression executable and existing Python verification scripts remain in `tests/`. Checkpoint records contain their historical test evidence; this cleanup did not restructure or rerun the domain suites.

HR Backend V1 is frozen at `hr-backend-v1`. [The freeze summary](../modules/hr/HR-V1-FREEZE.md) defines its scope and deferred work. School Management and application frontend development require separate approved checkpoints.
