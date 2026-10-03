# D8D — Leave evidence and sandwich foundation

## Completion

Implemented the approved external evidence receipt/history foundation and corrected HR sandwich-review lifecycle. Detection means a potential case awaiting review; it imposes no entitlement debit. No binary delivery, authentication/RBAC, Attendance processing, Payroll monetary change, historical backfill, or post-charge correction was implemented. No commit or push was performed.

## Migration and actual Development schema

Final balance-floor follow-up: `20261003164508_AddCappedLeaveSandwichConsumption` was inspected and applied only to local Development localhost/SIAMIS. It adds one nullable int, `EmployeeLeaveSandwichAllocations.AppliedDebitMinutes`, and `CK_LeaveSandwichAllocation_Applied` requiring null or 0 <= applied <= potential. It does not rewrite either applied migration, historical snapshots, rows, or seed values. Null distinguishes unreviewed/legacy facts; new successful reviews persist an explicit amount, including zero. Legacy active uncapped accounting retains its existing potential-amount fallback without inventing HR review events. The Development migration baseline had no sandwich cases or allocations.

`20261003121650_AddLeaveEvidenceAndSandwichFoundation` was inspected and applied only to Windows-authenticated local Development `localhost/SIAMIS`.

Policy correction follow-up: `20261003132846_AddLeaveSandwichReviewLifecycle` was inspected and applied to that same Development database. Its only schema operations replace two case/event state check constraints, adding ReviewPending, ReasonAccepted, and ReasonNotAccepted. No tables, columns, seed values, or historical rows were changed by the follow-up. The original migration and its recorded history were preserved. Reserved/Exempted remain allowed only to preserve legacy history; new commands cannot create either state or fabricate an HR decision for legacy rows. Both new constraints were verified enabled/trusted. Reverting the follow-up requires compatible data; the Down constraints reject new-state rows rather than silently reinterpret them.

Seven application tables were added:

- EmployeeLeaveEvidence
- EmployeeLeaveEvidenceEvents
- EmployeeLeaveApprovalEvidence
- EmployeeLeaveSandwichCases
- EmployeeLeaveSandwichEvents
- EmployeeLeaveSandwichDates
- EmployeeLeaveSandwichAllocations

Also added nullable `LeavePolicies.SandwichEquivalentDayMinutes`, its check constraint, and the `EmployeeLeave(EmployeeId, LeaveId)` ownership alternate key. Existing published policy rows were not backfilled. No default equivalent minutes or seed data was introduced.

Live SQL verification confirmed all 12 focused checks are enabled and trusted, all focused FKs use NoAction, ownership composites are present, and there is no ActorId FK to Employees. Unique indexes protect evidence successors, active employee/type/date spans, and case revisions. Application table count is 73, excluding __EFMigrationsHistory. SQL rejects negative applied amounts and applied amounts above potential.

## Evidence behavior

- Receipts explicitly identify `ExternalReceipt`; the API accepts an opaque reference token, not a URL, filesystem path, storage key, or binary.
- Receipts are immutable. Acceptance, rejection, and supersession append events with server UTC timestamps. Review remarks are required; real reviewer identity remains nullable.
- Supersession creates a successor version under the same employee/request/type. Accepted historical receipts and approval associations are retained.
- Approval requires current Accepted evidence for every distinct required type derived from frozen D8C policy facts. New snapshots freeze the requirement mapping; legacy snapshots derive it from their own frozen facts.
- Missing, Recorded, Rejected, or superseded evidence returns 409 without changing leave status, timestamps, allocation, balance, or sandwich state.
- Successful approval freezes the exact accepted EvidenceIds. Later supersession and leave cancellation do not rewrite them.
- D8C certificate thresholds and qualifying-working-date calculations remain unchanged. Sandwich debit dates do not become certificate qualifying dates.

## Sandwich behavior

- One request must cover every frozen scheduled interval on each boundary date. FullDay and fully covering Timed requests qualify; partial requests cannot be combined.
- Same-request and separate-request boundaries are supported. Both boundary policies and every gap-date policy must participate for the same owning LeaveType and consistent BalanceTracked classification. Mixed LeaveType or Paid/Unpaid boundaries are excluded.
- Published policy equivalent minutes are explicit positive integers with no default. Existing participating versions with no configured unit cause a configuration conflict when a case would form.
- Gap facts, assignments, employment/calendar resolution, policy revisions, paid classification, debit units, boundaries, and affected years are frozen. Missing/conflicting frozen facts return 409 rather than guessing.
- Gap dates retain ScheduledMinutes=0, with separate SandwichDebitMinutes. Normal ChargeableMinutes and EmployeeLeave.Days are not increased.
- Detection creates ReviewPending and freezes potential debit dates/allocations without sandwich reservation/use. Normal scheduled leave still follows existing D8C entitlement validation.
- The first boundary may be Approved while review is pending. Approval that would complete the pair returns 409 until HR review succeeds; the transaction leaves status, timestamps, allocations, balances, case/event history, evidence, and approval associations unchanged. A single request containing both boundaries is subject to the same gate.
- ReasonAccepted imposes zero additional debit and permits final approval. Only normal scheduled leave consumes entitlement.
- ReasonNotAccepted resolves every affected year's available entitlement atomically. AppliedDebitMinutes = min(PotentialDebitMinutes, lawful available entitlement), independently per year with no borrowing. Partial and zero available entitlement are valid and do not reject review merely because full potential debit cannot be absorbed. Missing/inconsistent entitlement configuration still returns 409 without partial writes. Final boundary approval converts only the applied reservation to Charged exactly once.
- Potential debit remains frozen from policy; UnabsorbedDebitMinutes = PotentialDebitMinutes - AppliedDebitMinutes. The applied yearly amount is preserved after rejection/cancellation; CommittedDebitMinutes becomes zero on release. Current balance calculations and adjustment guards use only applied amounts. There is no negative balance, entitlement debt, automatic unpaid conversion, monetary debt, or disciplinary/behavior subsystem.
- Rejection/cancellation releases reservation or reverses charged use once. The explicit review outcome, reason, and UTC time remain in append-only history, including after release. ReasonAccepted has no sandwich amount to release.
- Nontracked cases preserve facts without creating entitlement reservation/use. Cross-year allocation uses each gap date's calendar year, atomically.
- HR review requires ExpectedStatus=ReviewPending, explicit Outcome=ReasonAccepted/ReasonNotAccepted, and a nonempty reason. Decision time is server UTC; authenticated reviewer identity remains nullable/deferred. Stale state returns 409. The old exception route was removed. Review is scoped to the case revision; no post-review/post-charge correction command was added.
- Paid/Unpaid is independent from review outcome. Both outcomes were verified for both classifications. SandwichDebitMinutes are entitlement-policy facts only; gap dates remain ScheduledMinutes=0 and never become unpaid scheduled working time, Attendance absence, salary deduction, or a payroll line. Future unpaid-leave Payroll integration requires a separately approved contract using actual scheduled unpaid working time.
- Lifecycle decisions use the frozen case; existing historical Approved pairs are not automatically backfilled.
- Serializable writes acquire the shared employee UPDLOCK first, then deterministic calendar/type locks. Entitlement reductions include sandwich commitments.

## API/read models and Swagger

Six documented actions:

| Method | Path |
|---|---|
| GET | /api/employees/{employeeId}/leave/{leaveId}/evidence |
| POST | /api/employees/{employeeId}/leave/{leaveId}/evidence |
| POST | /api/employees/{employeeId}/leave/{leaveId}/evidence/{evidenceId}/accept |
| POST | /api/employees/{employeeId}/leave/{leaveId}/evidence/{evidenceId}/reject |
| GET | /api/employees/{employeeId}/leave-sandwich-cases?leaveId=... |
| POST | /api/employees/{employeeId}/leave-sandwich-cases/{caseId}/review |

These new controller actions are Development-only and return 404 in other environments. This environment check does not provide authenticated authorization. Swagger verifies all actions, response codes, strict contracts, and rejection of unknown audit/provenance fields.

Leave detail/list/history exposes evidence requirements, receipt state/type/history, approval EvidenceIds, separate sandwich debit, case states/dates/classification, and review history. Cases retain SandwichDebitMinutes/PotentialDebitMinutes as configured facts; AppliedDebitMinutes/AppliedSandwichDebitMinutes preserve actual historical entitlement absorption, UnabsorbedDebitMinutes records the difference, and CommittedDebitMinutes reports current reservation/use. ReviewOutcome distinguishes accepted reasons from unabsorbed consequences; ReviewedAt is server UTC. Yearly allocations expose potential/applied/unabsorbed amounts; gap dates retain their potential units and zero scheduled work. For nontracked cases, applied entitlement consumption is zero. Leave aggregates and balances use applied amounts only for active committed states and exclude ReviewPending/ReasonAccepted/Released.

## Verification results

| Suite | Passed assertions |
|---|---:|
| Pure/model regression executable | 649 |
| Capped-consumption focused prerequisite suite | 391 |
| Complete D8D live API/SQL/Swagger/concurrency | 523 |
| D1 and D8B live regressions | 117 |
| D8C live regressions | 246 |
| D8C ExpectedStatus/concurrency regressions | 230 |
| Established Payroll live regressions | 836 |
| Regression-wrapper baseline checks | 6 |
| Total | **2,998** |

The pure executable includes 79 new D8D assertions, including strict typed review outcomes/preconditions, forbidden caller audit fields, a ReviewPending default, and capped-minute arithmetic. Payroll regression breakdown: D5A 70; D5C 306; D6B 76; D6C 48; D6D 38; D6E 88; D6E boundaries 128; D7 82. D7's assertion count depends on which concurrent stored-fact reads return snapshots rather than allowed conflicts; its unchanged scenarios all passed. Monetary expectations were not adapted. Original live scenario files remain unchanged; the wrapper supplies approved D8D schema counts, explicit synthetic equivalent minutes, and the new Accepted-evidence prerequisite to D8C fixtures. Final focused verification preceded the complete D8D and prior-regression suites; those complete suites ran once against the final capped implementation.

Focused tests covered wrong parent/type, immutable acceptance, multiple required types, supersession/history, UTC timestamps, no sensitive storage identifiers, full/partial/split schedule coverage, working Saturday/ExceptionalWorkingDay interruptions, same/two requests, mixed classifications, variable debit units, tracked/nontracked cases, detection/review/reserve/charge/release, missing/zero/insufficient budgets, cross-year allocation, and legacy/missing/conflicting frozen facts. The corrected tests additionally verify zero pending/use before review, first-boundary approval, final-boundary 409 without mutation (including Accepted evidence/approval associations), both outcomes for Paid and Unpaid, strict/stale review contracts, preserved normal ChargeableMinutes/Days, and removal of the exception route.

Concurrency covered evidence acceptance/approval, supersession/approval, second request/approval, review/final approval, competing HR outcomes, boundary rejection/cancellation versus review/reservation or final charge, duplicate formation, and adjustment/review reservation. Four review/final-approval races cover both HR outcomes: review-first permits approval; approval-first returns 409 until review completes. Competing decisions produce one successful review and one stale-state 409, with one review event. No duplicate reservation/charge/release, approval of an already-superseded version, or overspending survived.

Payroll regressions covered Supplement/ReplaceAssignment, monetary/statutory boundaries, failed new-generation rollback, failed regeneration preservation, provenance/snapshots, manual adjustments, and payroll/period/payslip lifecycles. PayrollCalculationService, PayrollGenerationService, and PayrollPreviewService were not modified.

## Final Development baseline

All temporary fixtures were removed. Exact row content and timestamps were compared across all 73 application tables; the original 66-table pre-migration data was preserved. TEST-EMP-001 remains the original inactive employee (`433f2c1a-6222-494f-a64f-cd0c31126dc4`).

| Nonempty table | Rows |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| Departments | 12 |
| Designations | 20 |
| EmploymentTypes | 6 |
| EmploymentStatuses | 9 |
| ContractTypes | 7 |
| Locations | 5 |
| HiringSources | 10 |
| Genders | 4 |
| MaritalStatuses | 6 |
| Nationalities | 13 |
| DocumentTypes | 15 |
| LeaveTypes | 10 |
| AttendanceStatuses | 11 |
| PayTypes | 6 |
| PayrollComponents | 17 |
| PerformanceRatings | 5 |
| AddressTypes | 3 |
| Countries | 13 |

Master data totals 172 rows. All other 53 application tables contain zero rows, including all seven D8D tables, all leave/calendar/entitlement data, Attendance, PayrollRules/Targets/Settings/Periods, compensation/assignment/payroll/line/statutory/PIT/payslip operational data, and employee child resources. There are no other employees.

## Final checks

- dotnet restore: succeeded.
- Release solution build: succeeded, 0 warnings and 0 errors.
- EF has-pending-model-changes: no pending model changes.
- git diff --check: passed.
- Local migration recorded; live schema checks passed.
- Exact Development baseline cleanup passed.
- No Attendance rows or Payroll monetary behavior changes.
- No commit or push.

The final read-only SQL/API check independently confirmed all three D8D migrations, the exact pre-migration rows/timestamps, zero temporary operational rows, and GET /api/employees HTTP 200 with totalCount=1. The task-owned Development API was stopped after verification for the final Release build; it can be started again with the existing SIAMIS.Api launch profile.

Ignored machine-readable results/logs are under `tests/SIAMIS.Payroll.RegressionTests/bin`: d8d-capped-focus-results.json (including both HTTP responses and persisted race facts), d8d-live-results.json, d8d-regressions-results.json, d8d-final-verification.json, and d8d-regression-*.log. Diagnostic/retry runs are not double-counted in the totals above.

## Deferred boundaries and concerns

Before real medical files are used, Production Hardening must implement authenticated upload/download, RBAC/resource authorization, private binary storage, MIME/signature validation, upload size enforcement, malware scanning/quarantine, access audit, and retention policy. Current Accepted external receipts are Development foundation metadata; they do not establish secure SIAMIS binary verification or production approval authorization.

Also deferred: historical sandwich backfill, post-charge exception correction, carry-forward, mixed-type/paid sandwich cases, union of partial boundary requests, Attendance and Payroll integration. School policy owners must explicitly configure equivalent minutes in valid policy versions. Read enrichment currently issues per-item queries; batch optimization can be considered later if leave-history volumes warrant it.

## Complete changed-file list

### Modified (14)

- src/SIAMIS.Api/Program.cs
- src/SIAMIS.Application/Employees/EmployeeLeaveContracts.cs
- src/SIAMIS.Application/Leave/LeaveFoundationContracts.cs
- src/SIAMIS.Domain/Entities/Leave/LeaveFoundation.cs
- src/SIAMIS.Infrastructure/Configurations/LeaveFoundationConfiguration.cs
- src/SIAMIS.Infrastructure/Configurations/LeaveRequestConfiguration.cs
- src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs
- src/SIAMIS.Infrastructure/Services/EmployeeLeaveReadService.cs
- src/SIAMIS.Infrastructure/Services/EmployeeLeaveService.cs
- src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs
- src/SIAMIS.Infrastructure/Services/LeaveRequestCalculator.cs
- src/SIAMIS.Infrastructure/Services/LeaveSnapshotIntegrity.cs
- tests/SIAMIS.Payroll.RegressionTests/D8CLeaveCalculationTests.cs (synthetic fixture supplies its required document type)
- tests/SIAMIS.Payroll.RegressionTests/Program.cs

### Created during implementation (19)

- D8D-REPORT.md
- src/SIAMIS.Api/Controllers/LeaveEvidenceSandwichController.cs
- src/SIAMIS.Application/Leave/LeaveEvidenceSandwichContracts.cs
- src/SIAMIS.Domain/Entities/Leave/LeaveEvidenceSandwich.cs
- src/SIAMIS.Infrastructure/Configurations/LeaveEvidenceSandwichConfiguration.cs
- src/SIAMIS.Infrastructure/Migrations/20261003121650_AddLeaveEvidenceAndSandwichFoundation.cs
- src/SIAMIS.Infrastructure/Migrations/20261003121650_AddLeaveEvidenceAndSandwichFoundation.Designer.cs
- src/SIAMIS.Infrastructure/Migrations/20261003132846_AddLeaveSandwichReviewLifecycle.cs
- src/SIAMIS.Infrastructure/Migrations/20261003132846_AddLeaveSandwichReviewLifecycle.Designer.cs
- src/SIAMIS.Infrastructure/Migrations/20261003164508_AddCappedLeaveSandwichConsumption.cs
- src/SIAMIS.Infrastructure/Migrations/20261003164508_AddCappedLeaveSandwichConsumption.Designer.cs
- src/SIAMIS.Infrastructure/Services/EmployeeLeaveEvidenceService.cs
- src/SIAMIS.Infrastructure/Services/EmployeeLeaveSandwichService.cs
- src/SIAMIS.Infrastructure/Services/LeaveEvidenceRules.cs
- tests/SIAMIS.Payroll.RegressionTests/D8DEvidenceContractTests.cs
- tests/d8d_capture_baseline.py
- tests/verify_d8d_live.py
- tests/verify_d8d_capped_focus.py
- tests/verify_d8d_regressions.py

### Preserved pre-existing analysis artifact (1)

- D8D-ANALYSIS.md (already untracked at the start; not modified during implementation)

Git working-tree inventory: 34 paths, including this report and the preserved analysis artifact. Generated build outputs/results and temporary diagnostic/construction scripts are not source changes. The policy corrections changed only the sandwich domain/contracts/controller/configuration/service, its read/accounting integration, focused tests, model snapshot, focused follow-up migrations, and this report. Evidence implementation, original applied migration, and Payroll services were preserved.

## Approved D8C boundary and concurrency investigation

Actual scheduled leave with insufficient paid entitlement currently remains subject to the D8C 409 insufficient-balance contract. Paid-to-unpaid exhaustion or splitting requires a separate approved design covering calculation, classification, historical snapshots, lifecycle behavior, and eventual Payroll integration. D8D does not implement it.

The interrupted adjustment/review race was investigated from the preserved working tree. The test expected HTTP 200 for adjustment creation; the existing endpoint returns HTTP 201 Created. Captured adjustment-first responses proved this was a test expectation issue, rather than a production locking defect. No production concurrency workaround was introduced. The subsequently approved capped-consumption policy changes financial behavior deliberately: adjustment-first yields adjustment 201 and review 200 with a partial applied amount; review-first yields review 200 and adjustment 409 if the proposed reduction would breach existing commitments. Twenty focused races captured both HTTP responses, persisted review history, case facts, applied/unabsorbed amounts, adjustments, and balances: 17 adjustment-first outcomes and 3 review-first outcomes. Every result had one review event and nonnegative availability. Exact cleanup followed. Earlier diagnostic/retry runs are excluded from final assertion totals.
