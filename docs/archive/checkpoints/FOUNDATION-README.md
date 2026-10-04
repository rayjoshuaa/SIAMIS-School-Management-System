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

## D3 Basic Salary entitlement

Payroll Preview and Generation share `BasicSalaryEntitlementService`. Basic Salary
supports one **complete calendar month** and existing Monthly compensation (`PAY-001`).
PayrollPeriod CRUD still permits other ranges, but Preview/Generation return HTTP 400
for partial, cross-month or multi-month periods. Daily/Hourly compensation is not converted:
the affected employee fails calculation.

`PayrollSettings.BasicSalaryProrationMethod` defaults to `ThirtyDay`, the only implemented
policy. With no active settings, calculation uses **SystemFallback** without inserting settings.
An active valid row uses **Configured**; an invalid configured method fails rather than falling
back. Preview and historical audit identify both the method and its source. This is the approved
SIAMIS payroll policy, not a claim of a universal statutory salary formula.

Employment start (`StartDate ?? HireDate`), EndDate and compensation effective boundaries are
inclusive. Payable days are the union of employment dates within the month; gaps are unpaid.
Weekends, holidays, attendance and leave do not change entitlement in D3.

- Full continuous month with unchanged monthly salary: exact monthly salary, including February
  and 31-day months. Adjacent employment/context changes do not reduce entitlement.
- Partial unchanged salary: `min(monthly salary, monthly salary / 30 * payable days)`.
- Salary changes: calculate applicable compensation segments independently, with no global cap
  across different salary rates. Each segment is capped at its own monthly salary.
- No compensation anywhere on payable dates: employee Skipped. Partial or overlapping coverage:
  employee Failed. Every payable date must have exactly one Monthly compensation.
- Contributing currencies are trimmed and uppercased and must match. No currency conversion.
- Decimal intermediates are retained; final amounts round to four places, AwayFromZero. The final
  segment absorbs any rounding residual so displayed segments reconcile to the Basic Salary line.

Preview exposes typed `basicSalaryCalculationSnapshot` on its BasicSalary line. Generation
stores the same version-1 structure in `EmployeePayrollLines.BasicSalaryCalculationSnapshotJson`:
method/source, period, currency, payable days, full/prorated flags, final amount, and employment/
compensation segment inputs and amounts. For full unchanged salary split across explanatory
segments, amounts are allocated by calendar days to reconcile to exact monthly entitlement;
`fullMonthAllocation` distinguishes this from ThirtyDay partial proration. Other source types
retain null snapshots. Existing historical rows remain nullable and are not backfilled.

Generation retains period-then-employee locking and Serializable transactions; compensation
create/update/delete now share the employee lock. Failed regeneration preserves the prior
complete payroll. Stored audits do not query or change with live employment/compensation edits.
Rules with BasicSalary bases use the final entitlement. Other rule formulas, assignments,
manual adjustments, targeting and lifecycle behavior remain unchanged. Assignments still use
period-start effective-date selection and are not prorated.

Broader payroll amounts have no explicit currency field and remain effectively single-currency;
this checkpoint prevents mixed compensation currencies only. Daily/hourly policies, statutory
Thai tax/social security/provident fund, attendance/leave deductions, expected-population period
closure, and multi-currency payroll remain deferred.

## D4A statutory policy foundation

Statutory configuration now has separate schemes, immutable Published policy versions,
typed Social Security/PIT parameters, and a date-based resolver. It is not connected to
payroll monetary calculation. No schemes or legal numeric parameters are seeded.

See [D4A implementation and verification report](D4A-REPORT.md) for the schema, APIs,
publication rules, local migration result, verification evidence and future boundaries.

## D4B employee statutory inputs

Employee statutory enrollments and taxpayer profiles now have focused APIs. Tax-year
declarations use immutable Verified revisions, manual Spouse/Child/Parent claims and
explicit aggregate opening states. Section 33 enrollments now participate in D5C payroll calculation; tax declarations remain inputs only.
No employee inputs are automatically created. See [D4B implementation and verification report](D4B-REPORT.md).

## D5A SSO contribution wage classification

Components now carry explicit Unknown / Included / Excluded metadata, copied into
historical payroll-line snapshots without changing monetary calculations. Existing
components remain Unknown. The focused migration is applied to local Development SIAMIS.
See [D5A implementation and verification report](D5A-REPORT.md) for changed files,
API semantics, migration/schema verification, live tests, final baseline counts and deferred statutory decisions.

## D5B Section 33 contribution-wage contract

D5B introduced the pure resolver of final Included earning snapshots and explicit
unresolved-source diagnostics. D5C now uses it in the shared statutory calculator.
D5B itself created no migration. See [D5B findings, verification and decisions](D5B-REPORT.md).


## D5C Section 33 monetary integration

Preview and Generation share the approved SSO-TH-V1 contract. PayrollPeriod.EndDate
resolves policy and enrollment for the stored YYYY-MM contribution month; it represents
the month and is not a legal assertion that the final day creates liability.
Applicable requires explicit Included/Excluded final earnings, a compatible Published
TH-SSO-33/TH/THB policy and equal percentage-point employee/employer rates. Unknown
or missing determination fails. Explicit NotApplicable preserves generic payroll behavior.

Positive wage is clamped to policy monthly bounds; zero wage uses zero base/contribution.
D3 determines partial-month earnings; SSO bounds are not prorated. Raw decimal employee
contribution is rounded explicitly to whole baht, half up. Employer amount equals the
rounded employee amount and never affects employee payroll totals.

DEDUCT-001 presents the positive automatic employee deduction, with Statutory provenance
and result snapshot ID. It is not the formula authority. A final Assignment/PayrollRule
deduction for that code conflicts for Applicable employees; nothing is silently suppressed.
Zero contribution stores a result and creates no zero-value line. Result foreign keys use
NoAction; line SourceId remains historical snapshot data without a foreign key.

Manual earning changes return 409 once an SSO result exists. Manual deductions retain
existing lifecycle and reconciliation rules. Generated lines remain protected. Forced
regeneration replaces the entire snapshot, including existing Manual adjustments.

No statutory values or legal component classifications are seeded. Development returns
to its clean baseline after verification, so real calculation requires reviewed policy,
explicit enrollment and wage classifications. Geographic relief, PIT, Provident Fund,
authentication and configurable scheme-to-component binding remain deferred.
See [D5C implementation and verification report](D5C-REPORT.md).
