# System architecture

SIAMIS is a .NET 10 API-first solution with four projects:

| Project        | Established responsibility                                                                  | Project references          |
| -------------- | ------------------------------------------------------------------------------------------- | --------------------------- |
| API            | HTTP controllers, DTO input/output, dependency registration and security pipeline           | Application, Infrastructure |
| Application    | Service interfaces, request/response contracts and DTOs                                     | Domain                      |
| Domain         | Entity models                                                                               | None                        |
| Infrastructure | EF Core/SQL Server, Identity persistence, service/query implementations and private storage | Application, Domain         |

The usual path is controller → application interface → infrastructure service → EF Core. Business workflows currently reside in service implementations; the Application project does not contain every business implementation. Controllers do not expose DbContext or return EF navigation graphs.

SQL Server migrations define persistence. Identity users and employees are separate concepts with an optional linkage. Private HR document bytes use a storage abstraction outside the public web root. See [database](DATABASE.md), [security](SECURITY.md) and [authorization](AUTHORIZATION.md).

The regression executable and existing Python verification scripts remain in `tests/`. Checkpoint records contain their historical test evidence; this cleanup did not restructure or rerun the domain suites.

The historical HR V1 baseline is recorded at `hr-backend-v1`. [The freeze summary](../modules/hr/HR-V1-FREEZE.md) preserves that checkpoint; subsequent approved employee-management and clocking extensions are indexed in [current HR status](../modules/hr/README.md). Frontend development now includes the premium V3 foundation and supported HR workflows. Future modules still require separate approval.

Employee photos reuse the private-storage abstraction with dedicated metadata and employee capabilities, separately from confidential HRDocuments. Database-controlled number reservations and durable registration receipts protect identity across concurrent commands/retries. The React frontend uses bounded queries and shared capability-aware routing; no frontend role-name checks replace backend authorization. See [frontend architecture](../frontend/ARCHITECTURE.md).
