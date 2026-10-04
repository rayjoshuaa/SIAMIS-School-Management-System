# D11 â€” Paid/Unpaid Leave Exhaustion

## 1. Executive summary

D11 implements prospective Snapshot V2 for one logical Leave request with chronological whole-minute paid/unpaid segments. Insufficient configured paid entitlement becomes unpaid coverage; missing paid entitlement remains a configuration conflict. Payroll formulas and transactions are unchanged. All focused verification and regressions passed; the exact Development data baseline is restored. No commit or push.

## 2. Repository/database state inspected

Starting commit: `3d86380` (D10). Development: SQL Server 2025, `localhost/SIAMIS`, integrated authentication. Initial state: 85 application tables, 40 migrations, 172 master rows, five security roles, one inactive TEST-EMP-001 (`433f2c1a-6222-494f-a64f-cd0c31126dc4`), one EmploymentRecord, no operational workflow fixtures/users. Original rows and timestamps were captured before the focused migration.

## 3. Previous D8C behavior

Tracked requests reserved all ChargeableMinutes and rejected insufficient available entitlement. Calendar resolution, immutable snapshots, medical evidence, lifecycle, Employee-first locking and D8D sandwich review were already established.

## 4. Approved exhaustion contract

Paid + tracked: min(total, available applicable calendar-year entitlement) is paid; excess is unpaid. Configured zero is valid. Missing entitlement is a conflict. Paid + untracked remains entirely paid without funding. Unpaid + tracked/untracked remains entirely unpaid without consuming a paid quota. BalanceTracked remains configuration metadata. Request-wide IsPaid stays the original requested LeaveType policy fact, not proof that all V2 coverage is paid.

## 5. Paid allocation algorithm

Resolve employment, explicit assigned calendar, published policy and whole-minute charged intervals using the existing calculator. Under the existing transaction/locks, read each applicable year's remaining entitlement including actual normal paid reservations and committed sandwich debit. Traverse dates and intervals in existing chronological order, assigning up to each year's remaining paid minutes. Negative stored availability is an integrity conflict rather than silent corruption recovery.

## 6. Unpaid allocation algorithm

The remainder of every charged interval is frozen as unpaid. No second request or substituted LeaveType is created. Unpaid segments consume no paid entitlement and create no debt/credit, Payroll transaction, KPI or disciplinary consequence.

## 7. Partial-day behavior

Split intervals at whole-minute boundaries. Example tested: 08:00â€“16:00 with 180 available minutes becomes 08:00â€“11:00 paid and 11:00â€“16:00 unpaid. No assumed eight-hour entitlement unit; this eight-hour schedule is a test fixture only.

## 8. Multi-date behavior

Allocation follows actual scheduled minutes, including split schedules, exceptional replacement intervals and different day lengths. Nonworking dates retain zero charged minutes and empty V2 payment intervals. Weekends/holidays are not reinterpreted.

## 9. Cross-entitlement-period behavior

Each calendar year independently funds its own charged dates. No borrowing/carry-over. Cross-year verification uses different budgets and checks exact attribution to yearly allocations.

## 10. Snapshot/history behavior

New requests persist Version 2. Dates add PaymentIntervals (StartTime, EndTime, IsPaid); yearly allocations add PaidMinutes/UnpaidMinutes. Their nonnegative sum equals ChargeableMinutes. Integrity reconstructs chronological classification from frozen paid totals and reconciles every interval and relational allocation. Original calculation/schedule/notice/evidence checks remain.

V1 remains supported with both relational values null and no invented payment segments. V1 full-minute accounting and original IsPaid semantics remain. No backfill or historical update. V1 Attendance source serialization is verified byte-for-byte against the original shape. Published policies/live entitlement changes cannot reinterpret frozen V2 allocation.

## 11. Entitlement lifecycle

Pending reservations and Approved usage count V2 PaidMinutes only; V1 uses its original ChargeableMinutes. Paid untracked and unpaid normal Leave do not consume paid entitlement. Append-only adjustments cannot reduce entitlement below paid reservations/usage plus committed sandwich amounts. Balances expose nonnegative available minutes.

## 12. Cancellation/rejection behavior

Existing status-based accounting releases only the previously reserved/used paid amount. Reject and cancel remain exactly-once; repeated and stale intent commands return conflict. Approved cancellation retains its reason requirement. No fake credit arises from unpaid minutes. No new edit/recalculation workflow exists.

## 13. Sandwich interaction

D8D qualification, review, potential/applied/unabsorbed debit, cap and release history are preserved. Normal paid reservations share availability with sandwich committed debit. Payment metadata is omitted when deriving nonworking gap schedule facts so it does not alter sandwich qualification. Tests cover exhausted boundaries and capped shared availability. Unpaid V2 normal chargeable minutes consume zero. Existing separately reviewed D8D sandwich debits remain independent of normal payment classification, including existing unpaid/tracked policy combinations; their approved policy-driven cap/consumption semantics were not changed or replaced with a new quota model.

## 14. Attendance interaction

Both paid and unpaid Approved Leave cover authorized absence. V2 paid/unpaid totals/report classification use frozen segments; V1 uses its original IsPaid semantics. Total interval partition, exact ticks, 5-minute grace and truncation residual remain unchanged. V2 segments enter deterministic source fingerprints. Cancellation detects stale current validity while preserving immutable finalized history; explicit reopen/refinalize remains required.

## 15. Payroll non-impact

No Payroll source file/formula/lifecycle or statutory contract changes. No salary deduction or automatic adjustment. Focused tests compare Payroll rows/configuration, and existing financial regression suites retain their monetary expectations.

## 16. Authorization/security

D10 cookie authentication, CSRF, explicit capabilities and authenticated actor stamping remain. Employee identity is Userâ†’Employee linkage. Focused tests verify own submission, wrong-parent denial, approval/evidence denial and rejection of forged actor/allocation fields. Existing security and operational suites are rerun.

## 17. API/DTO changes

Additive nullable PaidMinutes/UnpaidMinutes on detail/history/year allocations; PaymentIntervals on frozen date facts and Attendance reporting Leave metadata. Legacy snapshot null classification properties are omitted from serialization. Request contracts do not accept these fields. No new endpoint or UI. POST documentation describes V2 classification, paid exhaustion and missing-configuration conflict.

## 18. Concurrency behavior

Existing serializable Employee-first locking and configuration/entitlement serialization are retained. Two valid different-date requests may both succeed, but their combined paid reservation cannot exceed the single available budget; the remainder is unpaid. Overlap and stale lifecycle intent remain conflicts. Entitlement adjustment/create races preserve coherent nonnegative funding.

## 19. Migration/schema changes

Created/applied only to local Development SIAMIS: `20261004072245_AddLeavePaidUnpaidAllocation`.

- Two nullable SQL int columns on EmployeeLeaveAllocations: PaidMinutes and UnpaidMinutes, without defaults/backfill.
- CK_LeaveAllocation_Payment accepts legacy null/null or nonnegative populated values whose bigint sum equals ChargeableMinutes.
- CK_EmployeeLeave_Authoritative accepts SnapshotVersion 1 or 2.
- No table, FK, index, seed or prior migration changed.
- Unique request/year index and NoAction parent FK remain.
- Cross-table snapshot-version/mandatory-V2 classification consistency is enforced by application integrity validation; no trigger introduced.

## 20. Pure/live test results

| Verification | Passed checks |
|---|---:|
| Complete pure regression executable | 929 |
| New D11 pure classification/integrity/fingerprint/Attendance checks (included above) | 37 |
| Focused authenticated D11 live API/SQL matrix | 73 |
| Final read-only SQL schema/history/baseline checks | 13 |
| Final fresh-build health/Swagger checks | 9 |

A–Q covered: sufficient/exact/partial/zero paid tracked; missing configuration conflict; paid untracked; unpaid tracked/untracked; partial interval; chronological varying-day/nonworking dates; independent years; synthetic V1 compatibility; V2 integrity/SQL constraints; concurrent exhaustion; sandwich availability; mixed Approved Attendance; Payroll non-impact; D10 ownership/capabilities/actor rejection. Frozen allocation survives later entitlement edits. Cancellation preserves finalized Attendance and produces structured stale/reopen metadata.

## 21. Regression results

| Domain suite | Passed checks |
|---|---:|
| D8B Leave foundation / D1 employment | 117 |
| D8C request/calculation/entitlement/read models | 246 |
| D8C lifecycle ExpectedStatus/concurrency | 230 |
| D8D evidence/sandwich | 527 |
| D8D capped consumption/concurrency | 392 |
| D9B Attendance evidence | 121 |
| D9C daily calculation/precision | 116 |
| D9D finalization/stale/reopen/concurrency | 174 |
| D9E reporting/history/query behavior | 201 |
| Eight Payroll suites | 836 |
| **Domain total (excluding orchestration checks)** | **2,960** |

Payroll breakdown: D5A 70; D5C 306; D6B 76; D6C 48; D6D 38; D6E 88; D6E boundaries 128; D7 operations/lifecycle/payslips 82. Original monetary expectations remain unchanged, including Supplement/ReplaceAssignment, conflicts, failed-new rollback, failed-regeneration preservation, source/audit snapshots, manual reconciliation, statutory persistence and lifecycle protections.

Dedicated D10 security: 45 standard + 28 advanced + 26 Production-pipeline = 99 live checks. Additionally 226 anonymous operation checks plus one Swagger cookie/CSRF integration check = 227. Production-pipeline simulation explicitly used only localhost/SIAMIS, trusted loopback forwarding and owned temporary process; it did not access a production database.

Obsolete tests were adjusted only for approved V2 additions/exhaustion, unpaid normal consumption and SQL fixture compatibility. Missing configuration, overlap, invalid lifecycle, evidence, actor, ownership and monetary protections remain. Initial fixture/assertion failures were cleaned up and resolved; no production-code regression was found. Successful D11 suite results were resumed without unnecessarily repeating completed Payroll/Attendance suites.

## 22. Exact Development cleanup

The final read-only SQL comparison equals the captured pre-D11 rows and timestamps across all 85 application tables. Only the intentional schema/migration change remains. Final non-empty workflow/security counts: Employees=1 (TEST-EMP-001, inactive), EmploymentRecords=1, Roles=5. Master data remains 172 rows, including PayrollComponents=17. Users/UserRoles/claims/logins/tokens/audits=0. All Leave requests/allocations/evidence/sandwich cases/entitlements/adjustments/policies/calendars/assignments, Attendance evidence/reviews/actions/finalized revisions, payroll headers/lines/rules/targets/settings/periods/compensations/assignments/statutory results and other temporary fixtures=0. The permanent existing master values, employee core fields, employment and timestamps match exactly.

Migration history contains all original 40 entries unchanged plus only D11, total 41. All owned Development/Production verification API processes were stopped. Ignored bin artifacts contain baseline captures, suite results and logs; no credentials are persisted in the report/source.

## 23. Complete changed-file list

- `D11-REPORT.md`
- `src/SIAMIS.Api/Controllers/EmployeeLeaveController.cs`
- `src/SIAMIS.Application/Employees/AttendanceReportingContracts.cs`
- `src/SIAMIS.Application/Employees/EmployeeLeaveContracts.cs`
- `src/SIAMIS.Domain/Entities/Leave/EmployeeLeaveAllocation.cs`
- `src/SIAMIS.Infrastructure/Configurations/LeaveRequestConfiguration.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004072245_AddLeavePaidUnpaidAllocation.Designer.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004072245_AddLeavePaidUnpaidAllocation.cs`
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`
- `src/SIAMIS.Infrastructure/Services/AttendanceDayCalculator.cs`
- `src/SIAMIS.Infrastructure/Services/AttendanceReportProjection.cs`
- `src/SIAMIS.Infrastructure/Services/AttendanceReviewSources.cs`
- `src/SIAMIS.Infrastructure/Services/EmployeeLeaveReadService.cs`
- `src/SIAMIS.Infrastructure/Services/EmployeeLeaveSandwichService.cs`
- `src/SIAMIS.Infrastructure/Services/EmployeeLeaveService.cs`
- `src/SIAMIS.Infrastructure/Services/LeavePaymentAllocation.cs`
- `src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs`
- `src/SIAMIS.Infrastructure/Services/LeaveSnapshotIntegrity.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D11LeavePaymentTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs`
- `tests/verify_d11_baseline.py`
- `tests/verify_d11_live.py`
- `tests/verify_d11_regressions.py`
- `tests/verify_d8c_live.py`
- `tests/verify_d8d_capped_focus.py`
- `tests/verify_d8d_live.py`
- `tests/verify_d9c_live.py`

27 changed/new non-ignored files. Prior migrations, Payroll services, authentication/RBAC configuration and employee core models are unchanged.

## 24. Remaining HR gaps

Leave-to-Payroll monetary policy/integration, disciplinary/KPI behavior and frontend remain separate approved work. D11 introduces no edit workflow, carry-over, debt or unpaid quota. Historical V1 paid/unpaid segment evidence remains unavailable rather than fabricated. Schema downgrade after V2 data requires explicit data review; do not roll back to V1 constraints with V2 requests present.

## 25. Verification/status table

| Check | Result |
|---|---|
| dotnet restore | PASS, up-to-date |
| dotnet build .\SIAMIS.sln -c Release --no-restore | PASS, 0 warnings / 0 errors |
| Pure/live/concurrency/domain/security checks | PASS, totals above |
| Migration application | PASS, local Development localhost/SIAMIS only |
| Actual schema/constraints/FK/index | PASS, 13 final SQL assertions |
| EF migrations has-pending-model-changes (Release/Development) | PASS, no changes since last migration |
| Health/Swagger, strict requests and new response metadata | PASS |
| Exact original rows/timestamps and fixture cleanup | PASS |
| git diff --check | PASS |
| Targeted changed-source secret/whitespace scan | PASS (not an exhaustive security product) |
| Payroll formulas/calculation/generation/preview source | Unchanged |
| Commit/push | Neither performed |

Final EF command: `dotnet ef migrations has-pending-model-changes --project src/SIAMIS.Infrastructure --startup-project src/SIAMIS.Api --configuration Release --no-build -- --environment Development`.

