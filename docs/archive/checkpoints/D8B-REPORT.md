# D8B — Leave Calendar, Policy & Entitlement Foundation

## Scope and status

D8B foundation implementation and verification are complete. All temporary fixtures were removed and the exact Development baseline restored.
Only local Development `localhost/SIAMIS`, Windows integrated authentication and `TrustServerCertificate=True`, is used. No payroll behavior, Attendance processing, authentication, frontend, sandwich calculation, or medical upload/storage was added. No commit or push.

## Migration and actual schema

Migration: **20261003091135_AddLeaveCalendarPolicyEntitlementFoundation**.
Inspected before application and applied successfully to local Development SIAMIS through EF Core. Recorded with EF Core 10.0.12.

Eight new tables:

- WorkCalendars
- WorkCalendarWeeklyIntervals
- WorkCalendarDateOverrides
- WorkCalendarOverrideIntervals
- EmployeeWorkCalendarAssignments
- LeavePolicies
- EmployeeLeaveEntitlements
- EmployeeLeaveEntitlementAdjustments

The database now contains 65 application tables (excluding __EFMigrationsHistory).
Ten new foreign keys use NO ACTION. Sixteen new foundation/preparation check constraints are enabled and trusted. No calendar, policy, holiday, entitlement or employee seed data was added. Existing 10 LeaveTypes are unchanged.

EmployeeLeave was retained with five nullable additive columns: RequestedStartTime, RequestedEndTime, ChargeableMinutes, CalculationSnapshotVersion, CalculationSnapshotJson. Existing Days/status contracts were not rewritten. New columns remain null on legacy requests. Boundary times belong to StartDate and EndDate and represent a continuous request range, including multi-day ranges. Future D8C resolves the range into actual working intervals and snapshots those intervals. No request-interval table was introduced.

SQL checks enforce paired whole-minute request boundary times, valid same-day ordering, nonnegative optional charge minutes and paired positive snapshot version/valid JSON. No backfill or guessed calculation was performed.

## Work calendars and minute precision

Named calendars have unique Code, Name, optional Description, IsActive/IsDefault and UTC datetime2 timestamps. A filtered unique index permits at most one active default suggestion. API rejects inactive default suggestions.

Multiple weekly intervals per weekday are supported, with StartTime < EndTime, whole-minute precision, and server overlap protection. Adjacent intervals are allowed. Overnight intervals are not supported by this contract. ScheduledMinutes sums actual configured periods; lunch and eight-hour days are never assumed. Example verified: 08:00–12:00 plus 13:00–16:00 resolves to 420 minutes.

Date overrides are unique per calendar/date. Categories are controlled: PublicHoliday, SchoolHoliday, RestDay and ExceptionalWorkingDay. Non-working categories require no intervals. ExceptionalWorkingDay requires valid non-overlapping intervals that REPLACE the weekly schedule. Example verified: 10:00–12:15 replaces a 420-minute Monday with 135 minutes.

## Effective employee assignments

Assignments have inclusive EffectiveFrom/EffectiveTo, permit future dates and reject overlaps under an employee parent lock. Exactly one assignment is required for date resolution. Missing coverage returns HTTP 409 with Work calendar not configured; multiple matches return HTTP 409 ambiguity. There is no IsDefault fallback or organization-default history.

Changing IsDefault neither modifies nor creates assignments. IsActive controls whether new assignments can reference the calendar; current metadata does not invalidate historical explicit assignments. Existing assignments resolve independently of current default/active flags.

## Leave policies

Typed LeavePolicy revisions reference LeaveType, unique type/version and inclusive effective dates. Status is server-controlled Draft then Published. Drafts are editable; published records are immutable through the service/API. No policy hard-delete or unpublish endpoint exists. Published overlap is rejected; open-ended predecessors are never silently shortened. Historical resolution uses exactly one published revision covering the date.

Fields: BalanceTracked, optional ForeseeableNoticeHours, AllowsSuddenRequest, SupportingDocumentPolicy (None/AlwaysRequired/Conditional), optional DocumentTypeId, CertificateAfterConsecutiveDays, CertificateOnMondayWorkingDate, CertificateOnFridayWorkingDate, SandwichParticipation. Conditional document policies require at least one supported certificate trigger and an active DocumentType. None cannot contain certificate/document settings. AlwaysRequired is independent of conditional triggers.

CertificateAfterConsecutiveDays represents an EXCEEDS-N threshold, not an entitlement amount. Monday/Friday flags represent scheduled working-date triggers; D8C/D8D implement enforcement. LeaveNoticeCategory defines Foreseeable/SuddenIllness. No real school policy was seeded. Synthetic 24-hour and greater-than-2-day settings existed only during tests and were removed.

DocumentType linking is metadata only. DOC-011 was not altered. No new DTO exposes StorageKey or medical contents. No generic rules engine was introduced.

## Entitlements and adjustments

EmployeeLeaveEntitlements are unique per EmployeeId/LeaveTypeId/LeaveYear, using calendar years 1–9999 and nonnegative integer EntitledMinutes. No base update/delete endpoint is provided; corrections are adjustment entries.

EmployeeLeaveEntitlementAdjustments are append-only through the API, with signed nonzero integer minutes, mandatory Reason and server-controlled UTC CreatedAt. No actor IDs are invented. Derived AdjustmentMinutes uses a bigint aggregate; AdjustedEntitledMinutes = EntitledMinutes + SUM(AdjustmentMinutes). Administrative adjustments cannot make the configured total negative or exceed Int32.MaxValue minutes.

Missing entitlement returns no record; configured zero is a distinct record. No independently editable UsedMinutes, PendingMinutes, AvailableMinutes or carry-forward columns exist. Full reservation/balance calculations remain D8C.

## Concurrency and architecture

API controller -> Application request/response contracts and ILeaveFoundationService -> Infrastructure partial service -> EF Core. No controller DbContext access or EF entity responses. Reads use AsNoTracking for resources and aggregate DTOs. Calendar resolution uses a read-only Serializable transaction and parent locks for a coherent multi-query result.

Calendar mutation locks calendar parents; default changes/creation serialize the calendar set. Assignments and entitlement operations lock Employees first, consistent with D1. Policy writes/publication serialize the LeaveType parent using the existing scalar SQL lock convention compatible with TPC mapping. Unique indexes provide identity/default safeguards; interval/effective overlap checks remain transactional application checks. Adjustments serialize on the employee parent and derive totals from history, avoiding lost updates.

Mutation DTOs reject unmapped JSON properties, so callers cannot supply publication state, timestamps or balance totals. Administrative authorization remains Production Hardening work.

## API inventory

All 19 actions are documented in Swagger with DTO response types and applicable 400/404/409 responses. Creates return 201; reads/updates/publication return 200. Creation Location headers point to the corresponding readable collection.

| Method    | Route                                                                      |
| --------- | -------------------------------------------------------------------------- |
| GET, POST | /api/work-calendars                                                        |
| PUT       | /api/work-calendars/{calendarId}                                           |
| GET, POST | /api/work-calendars/{calendarId}/weekly-intervals                          |
| GET, POST | /api/work-calendars/{calendarId}/overrides                                 |
| GET, POST | /api/employees/{employeeId}/work-calendar-assignments                      |
| GET       | /api/employees/{employeeId}/work-calendar?date=YYYY-MM-DD                  |
| GET, POST | /api/leave-policies (GET supports leaveTypeId)                             |
| PUT       | /api/leave-policies/{policyId}                                             |
| POST      | /api/leave-policies/{policyId}/publish                                     |
| GET       | /api/leave-policies/resolve?leaveTypeId=...&date=YYYY-MM-DD                |
| GET, POST | /api/employees/{employeeId}/leave-entitlements                             |
| GET, POST | /api/employees/{employeeId}/leave-entitlements/{entitlementId}/adjustments |

## Verification

| Verification                            | Result                                                      |
| --------------------------------------- | ----------------------------------------------------------- |
| Restore                                 | Succeeded; dependencies up to date                          |
| Final Release build                     | Succeeded, 0 warnings / 0 errors                            |
| Pure contract/regression runner         | 440 passed: 40 new D8B + 400 existing                       |
| Focused D8B live API/SQL                | 127 passed, including D1, legacy leave, Swagger and cleanup |
| D5A / D5C live regressions              | 70 / 306 passed                                             |
| D6B / D6C / D6D live regressions        | 76 / 48 / 38 passed                                         |
| D6E / D6E boundary regressions          | 88 / 128 passed                                             |
| D7 operations / lifecycle / payslip     | 83 passed                                                   |
| Existing live payroll regressions total | 837 passed                                                  |
| Primary assertion total                 | 1,404 passed                                                |
| EF pending-model-changes                | None                                                        |
| git diff --check                        | Passed, no whitespace errors                                |
| Exact cleanup                           | All 65 tables; original rows and timestamps preserved       |
| Final Employee list / legacy leave GET  | HTTP 200, totalCount = 1 / []                               |
| TEST-EMP-001                            | Remains inactive; core data unchanged                       |
| New foundation tables                   | All eight empty                                             |
| Existing master data                    | 172 rows; all 10 LeaveTypes unchanged                       |

Payroll regressions cover Supplement, ReplaceAssignment, conflicts, failed-new-generation rollback, failed-regeneration preservation, provenance/audit metadata, manual reconciliation, payroll/period lifecycles, D3 salary entitlement, D4 policy/profile behavior, SSO/PIT and payslip integrity. Payroll and Attendance implementation files are unchanged. The temporary verification API was stopped after testing.

The initial unadapted payroll runner stopped at its obsolete 57-table precondition. The complete D8B-adapted run passed without changing monetary assertions or baseline comparisons. Initial sandbox restore access and test harness issues were resolved. No outstanding build/test/EF failures remain.

D8B live tests verify default uniqueness, multiple intervals, minute validation, overlap rejection, all override categories, replacement semantics, explicit assignments, future/inclusive assignments, missing/ambiguous resolution, default changes, policy typed round-trips/publication/immutability/coverage/document references, entitlement uniqueness/zero/missing precision, signed adjustments/reasons/audit, four concurrent adjustments, SQL constraints, hourly snapshot persistence, Swagger and D1/legacy leave regressions. Fixtures are removed in finally blocks and complete application rows/timestamps compared exactly.

Pure D8B model/contract checks run within the existing dependency-free payroll regression runner. Existing payroll regression scenarios are unchanged. The D8B wrapper changes only the obsolete 57-table precondition to 65 in memory and captures current baseline artifacts; original scenario files are not modified.

The live runs found two implementation defects: FromSql on a TPC-derived LeaveType is unsupported, corrected to the existing scalar SQL parent-lock pattern; a null replacement interval returned 500, corrected with validation and an explicit regression case. Test-only mismatches (constraint count, D1 routes and GUID case) were corrected and the complete D8B suite rerun successfully. The complete final D8B rerun passed after both fixes.

## Remaining D8C/D8D boundaries

D8C must replace legacy leave creation/update/status/deletion behavior; implement request timing, D1 coverage/gap checks, minute calculation, year allocation, Pending reservations, approval/rejection/cancellation and immutable calculation evidence. Pending reserves availability; Approved moves the quantity to Used; rejection/cancellation releases it. Missing tracked entitlement must fail clearly. No balance/readiness claims are made by D8B's legacy leave API.

D8C must preserve calendar/policy evidence when calculating requests and control configuration changes affecting existing reservations. D8B calendar resolution describes currently configured schedules; it is not a historical leave recalculation API. Legacy CRUD does not read/write the new foundation tables and is not approved for the final workflow.

D8D implements sensitive evidence linking/delivery and approved sandwich semantics. Authentication/RBAC is deferred; administrative endpoints remain development-only until authorization hardening. D9 handles Attendance. Unpaid leave payroll effects require a separate approved checkpoint. No carry-forward, assumed holidays, entitlement grants or monetary behavior was added.

## Final Development table counts

Independent exact comparison confirmed all original application rows/timestamps unchanged and all new tables empty. Only the D8B migration was added to history.

| Table                                | Rows |
| ------------------------------------ | ---- |
| AddressTypes                         | 3    |
| Attendance                           | 0    |
| AttendanceStatuses                   | 11   |
| ContractTypes                        | 7    |
| Countries                            | 13   |
| Departments                          | 12   |
| Designations                         | 20   |
| DocumentTypes                        | 15   |
| EmergencyContacts                    | 0    |
| EmployeeAddresses                    | 0    |
| EmployeeCompensations                | 0    |
| EmployeeContacts                     | 0    |
| EmployeeContracts                    | 0    |
| EmployeeDocuments                    | 0    |
| EmployeeHistory                      | 0    |
| EmployeeLeave                        | 0    |
| EmployeeLeaveEntitlementAdjustments  | 0    |
| EmployeeLeaveEntitlements            | 0    |
| EmployeePayrollComponentAssignments  | 0    |
| EmployeePayrollLines                 | 0    |
| EmployeePayrollPitResults            | 0    |
| EmployeePayrollSocialSecurityResults | 0    |
| EmployeePayrollStatutoryResults      | 0    |
| EmployeePayrolls                     | 0    |
| EmployeePayslips                     | 0    |
| EmployeePerformance                  | 0    |
| EmployeePitPaymentScheduleEntries    | 0    |
| EmployeePitPaymentScheduleSelections | 0    |
| EmployeePitPaymentSchedules          | 0    |
| EmployeeStatutoryEnrollments         | 0    |
| EmployeeTaxClaims                    | 0    |
| EmployeeTaxDeclarationSelections     | 0    |
| EmployeeTaxDeclarations              | 0    |
| EmployeeTaxOpeningBalances           | 0    |
| EmployeeTaxProfiles                  | 0    |
| EmployeeWorkCalendarAssignments      | 0    |
| Employees                            | 1    |
| EmploymentRecords                    | 1    |
| EmploymentStatuses                   | 9    |
| EmploymentTypes                      | 6    |
| Genders                              | 4    |
| HiringSources                        | 10   |
| LeavePolicies                        | 0    |
| LeaveTypes                           | 10   |
| Locations                            | 5    |
| MaritalStatuses                      | 6    |
| Nationalities                        | 13   |
| OrganizationProfiles                 | 0    |
| PayTypes                             | 6    |
| PayrollComponents                    | 17   |
| PayrollPeriods                       | 0    |
| PayrollRuleTargets                   | 0    |
| PayrollRules                         | 0    |
| PayrollSettings                      | 0    |
| PerformanceRatings                   | 5    |
| PitPolicyConfigurations              | 0    |
| PitTaxBrackets                       | 0    |
| SocialSecurityPolicyConfigurations   | 0    |
| StatutoryPolicyVersions              | 0    |
| StatutorySchemes                     | 0    |
| TeacherProfiles                      | 0    |
| WorkCalendarDateOverrides            | 0    |
| WorkCalendarOverrideIntervals        | 0    |
| WorkCalendarWeeklyIntervals          | 0    |
| WorkCalendars                        | 0    |

## Running verification again

Use the installed .NET 10 SDK or the workspace `.dotnet/dotnet.exe`:

```powershell
dotnet restore
dotnet build .\SIAMIS.sln -c Release --no-restore
dotnet run --project tests/SIAMIS.Payroll.RegressionTests -c Release --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project src/SIAMIS.Infrastructure --startup-project src/SIAMIS.Api --configuration Release --no-build
git diff --check
```

Live scripts require the Development API on http://localhost:5155 and local Windows-authenticated SQL access. Run `python tests/verify_d8b_live.py`, then `python tests/verify_d8b_regressions.py` sequentially. They create synthetic fixtures and restore their baseline in finally blocks. The pre-migration baseline artifact is an ignored local verification file; rerunning the focused script on a different checkout requires a matching read-only baseline capture. Do not use these scripts against production or a populated employer baseline.

## Complete changed-file list

- [src/SIAMIS.Api/Controllers/LeaveFoundationController.cs](../../../src/SIAMIS.Api/Controllers/LeaveFoundationController.cs)
- [src/SIAMIS.Api/Program.cs](../../../src/SIAMIS.Api/Program.cs)
- [src/SIAMIS.Application/Leave/LeaveFoundationContracts.cs](../../../src/SIAMIS.Application/Leave/LeaveFoundationContracts.cs)
- [src/SIAMIS.Domain/Entities/Leave/LeaveFoundation.cs](../../../src/SIAMIS.Domain/Entities/Leave/LeaveFoundation.cs)
- [src/SIAMIS.Domain/Entities/Employees/EmployeeLeave.cs](../../../src/SIAMIS.Domain/Entities/Employees/EmployeeLeave.cs)
- [src/SIAMIS.Infrastructure/Configurations/LeaveFoundationConfiguration.cs](../../../src/SIAMIS.Infrastructure/Configurations/LeaveFoundationConfiguration.cs)
- [src/SIAMIS.Infrastructure/Configurations/EmployeeConfigurations.cs](../../../src/SIAMIS.Infrastructure/Configurations/EmployeeConfigurations.cs)
- [src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs](../../../src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs)
- [src/SIAMIS.Infrastructure/Services/LeaveFoundationService.cs](../../../src/SIAMIS.Infrastructure/Services/LeaveFoundationService.cs)
- [src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs](../../../src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261003091135_AddLeaveCalendarPolicyEntitlementFoundation.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261003091135_AddLeaveCalendarPolicyEntitlementFoundation.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261003091135_AddLeaveCalendarPolicyEntitlementFoundation.Designer.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261003091135_AddLeaveCalendarPolicyEntitlementFoundation.Designer.cs)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](../../../src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs)
- [tests/SIAMIS.Payroll.RegressionTests/D8BFoundationContractTests.cs](../../../tests/SIAMIS.Payroll.RegressionTests/D8BFoundationContractTests.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Program.cs)
- [tests/verify_d8b_live.py](../../../tests/verify_d8b_live.py)
- [tests/verify_d8b_regressions.py](../../../tests/verify_d8b_regressions.py)
- [D8B-REPORT.md](D8B-REPORT.md)
