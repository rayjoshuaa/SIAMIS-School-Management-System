# D8C — Leave request, calculation, balance and lifecycle engine

Date: 2026-10-03. Environment: local Development `localhost / SIAMIS`, Windows integrated authentication.

## Acceptance status: complete

Continued the existing working tree after the approved ExpectedStatus decision. The existing migration and foundation were retained. The four previously approved calculation contracts remain unchanged. No new unresolved contract boundary was found.

### Approved cancellation precondition

Cancellation now requires a typed ExpectedStatus with exactly Pending and Approved. The dedicated JSON enum converter rejects numeric values. Missing/null/invalid values return 400. The server continues to determine the target status.

The comparison occurs after EmployeeLeave is reloaded with UPDLOCK, inside the existing Serializable employee/request locking transaction. A valid precondition that differs from the locked status returns 409 before any lifecycle assignment, timestamp, remarks or balance effect. Approval/rejection retain their implicit Pending-only source-state checks.

Examples:

```json
{ "expectedStatus": "Pending", "cancellationRemarks": "Request withdrawn" }
```

```json
{ "expectedStatus": "Approved", "cancellationRemarks": "Approved leave withdrawn deliberately" }
```

Approved cancellation still requires a non-empty reason and preserves ReviewedAt. Repeated cancellation and terminal-state mutations return 409. The old generic status mutation routes remain removed.

The original race was rerun with non-empty cancellation remarks and ExpectedStatus=Pending. All 20 concurrent races produced exactly one HTTP 200 and one HTTP 409. The final state, balance usage and lifecycle audit fields corresponded exactly to the winner; snapshots and allocations were unchanged. Losing responses were explicit state conflicts, with no deadlock-handler response. Deliberate later cancellation with ExpectedStatus=Approved still succeeded. Stale ExpectedStatus=Pending on Approved returned 409 with exact header/allocation/balance preservation.

### Historical Paid/Unpaid classification

The calculator captures the authoritative LeaveType.IsPaid under the existing LeaveType lock and freezes it in the version-1 calculation snapshot. This is configuration classification, not a monetary calculation. It is exposed as IsPaid in employee detail/list, global history and the Pending queue.

The snapshot uses nullable IsPaid to distinguish unavailable legacy evidence from an actual IsPaid=false classification. New authoritative snapshots always contain true or false. Missing historical classification is not backfilled from today's master data; snapshot integrity validation requires it for authoritative lifecycle transitions.

Live verification covered both directions:

- A stored Paid request remained Paid in snapshot/detail/list/history after the live LeaveType changed to Unpaid; a new request used the new classification.
- An Approved Unpaid request retained IsPaid=false and 420 chargeable minutes after the live LeaveType changed to Paid; a new request used Paid classification with identical scheduled minutes.
- Approval/cancellation preserved original evidence. No Attendance, payroll header or payroll line was created by these operations.
- Temporary LeaveType changes were restored exactly, including the original row timestamps.

Approved + IsPaid=false + ChargeableMinutes therefore represents the historical HR fact of approved unpaid leave. No salary deductions, salary/30/8 assumption or payroll integration was introduced.

Neither ExpectedStatus nor snapshot IsPaid required a new entity field or migration. The original `20261003100244_AddLeaveRequestCalculationLifecycle` remains the only D8C migration.

## Completed implementation

### Schema and migration

Migration: `20261003100244_AddLeaveRequestCalculationLifecycle`.

Generated, inspected, built and applied only to local Development localhost/SIAMIS. It remains applied. No seed data, payroll tables or D8B foundation definitions were changed by this migration.

- EmployeeLeave: eight nullable additions, permitting historical legacy rows to remain readable: RequestMode, NoticeCategory, BalanceTracked, RequestedAt, ReviewedAt, CancelledAt, ReviewRemarks, CancellationRemarks.
- Lifecycle timestamps use SQL Server datetime2 and server DateTime.UtcNow; API timestamps are exposed as UTC.
- EmployeeLeaveAllocations: Id, EmployeeLeaveId, LeaveYear, ChargeableMinutes.
- Unique allocation index on EmployeeLeaveId + LeaveYear.
- Allocation FK to EmployeeLeave has NoAction delete behavior.
- EmployeeLeave indexes on EmployeeId + Status + StartDate and Status + RequestedAt.
- Five new checks: valid status, authoritative request shape, lifecycle timestamp shape, positive allocation minutes, calendar year 1–9999.
- All five checks were verified enabled and trusted in SQL Server; allocation uniqueness and NoAction FK were verified.
- Authoritative/snapshot checks preserve nullable legacy metadata. Legacy active requests lacking valid frozen evidence require an integrity review before transitions or overlapping creation; there is no guessed historical backfill.

### Request and calculation contract

- Strict request DTO: LeaveTypeId, inclusive StartDate/EndDate, explicit FullDay/Timed, explicit Foreseeable/SuddenIllness, optional boundary times and Reason.
- Unknown JSON fields are rejected, including status, days, calculated minutes, snapshots, allocations, balances, actors and lifecycle timestamps.
- FullDay resolves complete actual schedules. Timed intersects the first/last boundary dates and uses complete schedules on intermediate dates.
- Integer minutes are authoritative. Lunch gaps are excluded. No fixed eight-hour day is assumed. Zero-charge requests return 400.
- PublicHoliday, SchoolHoliday and RestDay have zero intervals. ExceptionalWorkingDay replaces the weekly schedule.
- Every requested date must have exactly one explicit effective calendar assignment, one valid D1 employment record and one Published policy. Missing/ambiguous coverage returns 409. IsDefault and current Employee.IsActive do not substitute for historical coverage.
- Future covered dates and adjacent employment history are supported; before-start, after-end, gaps and overlaps fail.
- Same BalanceTracked revisions may coexist in a request; mixed tracking semantics return 409 and require separate requests.
- Foreseeable notice uses MAX configured hours across all resolved revisions against the first chargeable interval converted explicitly from Asia/Bangkok to UTC. SuddenIllness requires every revision to allow it and a non-empty reason.
- Certificate metadata uses this request's qualifying positive-charge working dates only. Nonworking dates do not break the sequence or become chargeable. Working dates with zero charge break it. Longest sequence must exceed N. Monday/Friday triggers require actual charge. No document file is required for approval in D8C.
- Legacy Days is returned as the count of positively charged working dates; ChargeableMinutes/ChargeableHours expose precise duration.

### Balance, overlap and lifecycle

- Each successful request stores positive calendar-year allocation rows atomically with a Pending header and version-1 snapshot.
- Tracked requests require each year's entitlement. Missing entitlement differs from configured zero. Adjustments are included.
- Pending reserves, Approved consumes Used, Rejected/Cancelled release. Availability is derived from base + append-only adjustments − Used − Pending.
- Nontracked requests require no entitlement and create no unlimited entitlement configuration.
- Entitlement adjustment guard prevents reduction below Approved usage plus Pending reservations.
- Actual frozen charged intervals define overlap; disjoint same-day hourly requests and touching interval boundaries can coexist. Pending/Approved block; Rejected/Cancelled do not.
- Dedicated approve/reject/cancel commands preserve allocations and snapshots. Cancellation requires a matching ExpectedStatus under the mutation lock. Approved cancellation requires a reason and preserves ReviewedAt.
- Approval validates frozen schedule intersections, header identities, minutes, allocation rows, notice/timezone facts and certificate metadata. It checks current reservation coherence without recalculating live calendars or policies.
- Creation locks employee, relevant calendars in sorted GUID order, then the LeaveType scalar parent; configuration reads and balance checks occur in the same Serializable transaction. Lifecycle locks employee then request. D8B adjustment/assignment employee locking is reused. SQL deadlock/uniqueness conflicts in the new mutations produce sanitized 409 responses.
- Existing foundation APIs do not expose destructive interval/override/assignment deletion; Published policies stay immutable. Frozen evidence allows later calendar changes without rewriting requests. No broad HR configuration freeze was added.
- The approved cancellation source-state precondition resolves the lifecycle race, including cancellations submitted with remarks.

### Snapshot and operational reads

Version 1 includes frozen Paid/Unpaid classification, request identity/boundaries, server timestamp, business timezone, first chargeable local/UTC start, individual/effective notice facts, each considered date, employment/calendar assignment identities, calendar code/name, weekly/override source and interval identities, schedule intersections, policy identities/versions/typed flags, per-date/total minutes, year allocations, current-request certificate facts and sandwich participation metadata.

No medical content, StorageKey, unrelated employee PII or mutable balances are stored in this evidence.

Routes:

| Method | Route |
| --- | --- |
| GET | /api/employees/{employeeId}/leave |
| GET | /api/employees/{employeeId}/leave/{leaveId} |
| POST | /api/employees/{employeeId}/leave |
| POST | /api/employees/{employeeId}/leave/{leaveId}/approve |
| POST | /api/employees/{employeeId}/leave/{leaveId}/reject |
| POST | /api/employees/{employeeId}/leave/{leaveId}/cancel |
| GET | /api/leave-requests |
| GET | /api/leave-requests/pending |
| GET | /api/employees/{employeeId}/leave-balances?leaveYear=2030 |

The legacy PUT/DELETE routes are removed and return 405. GET compatibility is preserved. History supports employee/type/status/date-overlap filters, pagination and deterministic ordering. The Pending queue includes employee identity and calculation/document metadata. Detail provides typed calculation, frozen Paid/Unpaid classification and relational allocations. History/queue also expose IsPaid without requiring frontend JSON parsing.

Balance reads distinguish absent entitlement from configured zero. Nontracked types expose null entitlement/availability. When a whole year's Published revisions differ in tracking, the read model exposes BalanceTracked=null and PolicyCoverage=Mixed instead of choosing one annual flag; actual stored tracked allocations remain authoritative. PolicyCoverage can also be Tracked, NotTracked or NotConfigured.

## Verification results

| Suite/check | Result |
| --- | --- |
| Restore | Succeeded; projects up to date |
| Final Release solution build | Succeeded; 0 warnings, 0 errors |
| Pure focused runner | 570 assertions passed: 440 existing + 130 D8C |
| D8C live API/SQL suite | 245 assertions passed, including ExpectedStatus, Paid/Unpaid history, balances and concurrency |
| D8B/D1 live regression | 117 assertions passed |
| D5A live regression | 70 assertions passed |
| D5C live regression | 306 assertions passed |
| D6B live regression | 76 assertions passed |
| D6C live regression | 48 assertions passed |
| D6D live regression | 38 assertions passed |
| D6E live regression | 88 assertions passed |
| D6E boundary regression | 128 assertions passed |
| D7 operations/lifecycle/payslip regression | 84 assertions passed |
| Dedicated ExpectedStatus lifecycle regression | 230 assertions passed; all 20 races exactly one 200 and one 409 |
| Swagger | Request shape, response schemas and all new actions verified; PUT/DELETE absent |
| EF pending-model-changes | No changes since the applied migration |
| git diff --check | Exit 0; no whitespace errors or line-ending notices |
| Exact original application data comparison | Passed across all 65 pre-migration tables, including timestamps; new allocation table empty |

Reported suite total: **2000 passing assertions**, plus four regression-wrapper baseline assertions. The 20 dedicated races ended in 11 Approved and 9 Cancelled states; every race had one successful transition and one 409. The previously failing broader race is resolved. The first resumed race harness cleanup missed its newly introduced entitlement FK; the harness was corrected, only its fixtures were removed, and the entire 230-assertion suite reran with successful automatic exact cleanup. No verification error remains.

Regression harness adaptations are limited to the expected 66-table schema and 19 D8B-plus-EmployeeLeave checks. The obsolete legacy leave mutation block is superseded by the authoritative D8C tests. D1 history, D8B calendar/policy/entitlement assertions and all monetary expectations remain intact. Original payroll scenario files were not modified.

The existing payroll regression runner covers salary/proration, earning/deduction rules, Supplement/ReplaceAssignment, statutory calculations, preview/generation parity, failed-new rollback, failed-regeneration preservation, provenance/audit snapshots, manual reconciliation, header/period lifecycle and payslip history. Payroll implementation files were not changed.

Ignored verification JSON/logs are under `tests/SIAMIS.Payroll.RegressionTests/bin/`, including `d8c-live-results.json`, `d8c-regressions-results.json`, `d8c-transition-race-results.json`, `d8c-before-migration.json` and `d8c-final-baseline.json`. The task-owned Development API process was stopped after cleanup.

## Final Development baseline

66 application tables; 172 existing master-data rows. TEST-EMP-001 is the sole employee, ID 433f2c1a-6222-494f-a64f-cd0c31126dc4, IsActive=false. Its core data and timestamps match the exact captured baseline. EmploymentRecords=1. No temporary leave, configuration or payroll fixtures remain.

### Populated tables

| Table | Rows |
| --- | ---: |
| Employees | 1 |
| EmploymentRecords | 1 |
| AddressTypes | 3 |
| AttendanceStatuses | 11 |
| ContractTypes | 7 |
| Countries | 13 |
| Departments | 12 |
| Designations | 20 |
| DocumentTypes | 15 |
| EmploymentStatuses | 9 |
| EmploymentTypes | 6 |
| Genders | 4 |
| HiringSources | 10 |
| LeaveTypes | 10 |
| Locations | 5 |
| MaritalStatuses | 6 |
| Nationalities | 13 |
| PayrollComponents | 17 |
| PayTypes | 6 |
| PerformanceRatings | 5 |

### Tables with zero rows

Attendance, EmergencyContacts, EmployeeAddresses, EmployeeCompensations, EmployeeContacts, EmployeeContracts, EmployeeDocuments, EmployeeHistory, EmployeeLeave, EmployeeLeaveAllocations, EmployeeLeaveEntitlementAdjustments, EmployeeLeaveEntitlements, EmployeePayrollComponentAssignments, EmployeePayrollLines, EmployeePayrollPitResults, EmployeePayrolls, EmployeePayrollSocialSecurityResults, EmployeePayrollStatutoryResults, EmployeePayslips, EmployeePerformance, EmployeePitPaymentScheduleEntries, EmployeePitPaymentSchedules, EmployeePitPaymentScheduleSelections, EmployeeStatutoryEnrollments, EmployeeTaxClaims, EmployeeTaxDeclarations, EmployeeTaxDeclarationSelections, EmployeeTaxOpeningBalances, EmployeeTaxProfiles, EmployeeWorkCalendarAssignments, LeavePolicies, OrganizationProfiles, PayrollPeriods, PayrollRules, PayrollRuleTargets, PayrollSettings, PitPolicyConfigurations, PitTaxBrackets, SocialSecurityPolicyConfigurations, StatutoryPolicyVersions, StatutorySchemes, TeacherProfiles, WorkCalendarDateOverrides, WorkCalendarOverrideIntervals, WorkCalendars, WorkCalendarWeeklyIntervals.

## Deferred boundaries

D8C is complete. D8D retains document upload/linking/verification, secure evidence handling and approved sandwich charging. Cross-request certificate aggregation is outside V1 D8C. Attendance integration, payroll deductions, carry-forward, UI and authenticated reviewer attribution/RBAC remain deferred. No legal entitlement values or guessed policy data were seeded.

## Complete changed-file list

Modified:

1. `src/SIAMIS.Api/Controllers/EmployeeLeaveController.cs`
2. `src/SIAMIS.Application/Employees/EmployeeLeaveContracts.cs`
3. `src/SIAMIS.Application/Leave/LeaveFoundationContracts.cs`
4. `src/SIAMIS.Domain/Entities/Employees/EmployeeLeave.cs`
5. `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`
6. `src/SIAMIS.Infrastructure/Services/EmployeeLeaveService.cs`
7. `src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs`
8. `tests/SIAMIS.Payroll.RegressionTests/Program.cs`

Created:

9. `src/SIAMIS.Api/Controllers/LeaveOperationsController.cs`
10. `src/SIAMIS.Domain/Entities/Leave/EmployeeLeaveAllocation.cs`
11. `src/SIAMIS.Infrastructure/Configurations/LeaveRequestConfiguration.cs`
12. `src/SIAMIS.Infrastructure/Migrations/20261003100244_AddLeaveRequestCalculationLifecycle.cs`
13. `src/SIAMIS.Infrastructure/Migrations/20261003100244_AddLeaveRequestCalculationLifecycle.Designer.cs`
14. `src/SIAMIS.Infrastructure/Services/EmployeeLeaveReadService.cs`
15. `src/SIAMIS.Infrastructure/Services/LeaveRequestCalculator.cs`
16. `src/SIAMIS.Infrastructure/Services/LeaveSnapshotIntegrity.cs`
17. `tests/SIAMIS.Payroll.RegressionTests/D8CLeaveCalculationTests.cs`
18. `tests/verify_d8c_live.py`
19. `tests/verify_d8c_regressions.py`
20. `tests/verify_d8c_transition_race.py`
21. `D8C-REPORT.md`
