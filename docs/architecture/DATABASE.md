# Database

Persistence uses EF Core 10 with the SQL Server provider. [Infrastructure migrations](../../src/SIAMIS.Infrastructure/Migrations) are the authoritative schema history; do not maintain a competing handwritten table specification.

The D15 freeze verified 85 application tables and 42 applied migrations in local Development. The latest migration at that freeze is `20261004115720_AddSecureHrDocumentFoundation`. These are recorded freeze facts, not a claim about an arbitrary deployment's database.

Major persistence areas include employee/employment history, master data, calendars, leave policy/entitlement/evidence, attendance sources and revisions, payroll configuration and historical results, Identity/security audit, and private document metadata/versioning.

Established conventions include GUID identifiers, UTC application timestamps stored using SQL Server `datetime2`, explicit ownership relationships and historical snapshots. EmployeeNumber belongs to Employees. Payroll line SourceId is historical snapshot data, not a foreign key to live configuration. Constraints and indexes must be read from the migrations and EF configuration.

Development connections are local configuration, not shared credentials. See [Development setup](../deployment/DEVELOPMENT.md). Schema changes require their own approved checkpoint. Repository cleanup neither applies migrations nor queries or mutates a database.
