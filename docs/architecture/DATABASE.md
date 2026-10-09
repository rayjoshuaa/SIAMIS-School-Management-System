# Database

Persistence uses EF Core 10 with the SQL Server provider. [Infrastructure migrations](../../src/SIAMIS.Infrastructure/Migrations) are the authoritative schema history; do not maintain a competing handwritten table specification.

The D15 freeze verified 85 application tables and 42 applied migrations in local Development. The latest migration at that freeze is `20261004115720_AddSecureHrDocumentFoundation`. These are recorded freeze facts, not a claim about an arbitrary deployment's database.

## Current approved Development schema

The reported v0.5.1 baseline is **46 applied migrations, zero pending** on Ray / SIAMIS. Four approved additive extensions follow the historical freeze:

| Migration                                           | Purpose                                       |
| --------------------------------------------------- | --------------------------------------------- |
| `20261008061002_AddEmployeeClockingFoundation`      | Clock sessions and immutable evidence         |
| `20261009064219_AddPermanentEmployeeNumbering`      | Database allocator and permanent reservations |
| `20261009081620_AddPrivateEmployeePhotos`           | Private photo revisions                       |
| `20261009104602_AddEmployeeRegistrationIdempotency` | Durable registration receipts                 |

Read actual migration operations for indexes and constraints. Stage 3 does not apply migrations or refresh this database baseline. Production state is not inferred from Development.

New employee numbers start at 100000, are immutable and may have gaps. Database-controlled allocation and permanent reservations protect committed/retired numbers from reuse; never use `MAX + 1`. GUIDs, legacy numbers and relationships remain unchanged. Employee and initial employment creation stay atomic. Active is assigned on registration; hire-date effectiveness still governs current employment eligibility. Account provisioning is separate.

Registration receipts commit atomically with registration. Actor/operation-scoped keys replay equivalent successful requests for 30 days without another allocation; changed payloads conflict. Expired keys retain permanent protection against duplicate creation. Failed uncommitted requests remain retryable. See [idempotency](../frontend/F5.1E1-EMPLOYEE-REGISTRATION-IDEMPOTENCY.md).

## Deployment and rollback safety

Before applying an explicitly approved migration, verify environment/instance/database, exact pending list, schema consistency, record relationships and numeric collision preflight. Create a checksum backup, run `RESTORE VERIFYONLY`, restore to a disposable database and run `DBCC CHECKDB`. Apply only the approved migration, then compare identities, links, counts, reservations, allocator state and EF history/model consistency.

Do not treat `Down` as a safe business rollback: dropping reservations or receipts can defeat non-reuse guarantees; dropping photo metadata disconnects private binaries. Coordinate restore or forward repair with the owner. Keep SQL backups and data files outside version control. No rollback or database operation is part of documentation cleanup.

Major persistence areas include employee/employment history, master data, calendars, leave policy/entitlement/evidence, attendance sources and revisions, payroll configuration and historical results, Identity/security audit, and private document metadata/versioning.

Established conventions include GUID identifiers, UTC application timestamps stored using SQL Server `datetime2`, explicit ownership relationships and historical snapshots. EmployeeNumber belongs to Employees. Payroll line SourceId is historical snapshot data, not a foreign key to live configuration. Constraints and indexes must be read from the migrations and EF configuration.

Development connections are local configuration, not shared credentials. See [Development setup](../deployment/DEVELOPMENT.md). Schema changes require their own approved checkpoint. Repository cleanup neither applies migrations nor queries or mutates a database.
