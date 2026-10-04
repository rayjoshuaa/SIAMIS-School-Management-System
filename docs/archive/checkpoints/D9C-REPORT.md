# D9C â€” Daily Attendance Calculation and Leave Reconciliation

## 1. Executive summary

Implemented a calculated, read-only daily attendance API. It combines date-effective employment, explicit assigned work calendars, immutable D9B events and integrity-validated Approved D8C Leave snapshots. No persistence or migration is needed. No attendance finalization, confirmed absence, payroll effect or disciplinary behavior was introduced.

The initial precision stop was resolved by the user's approved exact-tick partition contract. Millisecond display totals are independently truncated, with conversion residue exposed separately.

## 2. Repository state inspected

Inspected actual D9B contracts/controller/service/resolver and event configuration; D8B calendar/assignment services; D8C calculator, frozen snapshot, integrity reader and lifecycle services; D8D sandwich configuration; employment lock conventions; legacy attendance controller; and payroll generation lock order.

The repository began clean before the earlier D9C inspection report was created. Continuing implementation preserved that work. Development is localhost/SIAMIS with Windows integrated authentication and TrustServerCertificate=True. Actual SQL inspection confirmed 74 application tables and latest migration `20261003175821_AddAttendanceEvidenceFoundation`; AttendanceEvents has 16 columns and datetime2(7) event/receipt timestamps.

## 3. Daily calculation contract

`AttendanceDayDto` contains expected-work provenance, sorted raw event DTOs, reconciled presence pairs with IN/OUT event IDs, Approved Leave evidence (LeaveId, observed status, snapshot version, frozen IsPaid and date calculation), exact UTC interval endpoints, structured findings and current calculation/grace metadata.

`CoveragePartitionAvailable` identifies whether the schedule can be partitioned unambiguously. Ambiguous evidence, invalid Leave, overlapping sources and schedule mismatch retain diagnostic intersections but have null authoritative category totals/residual. Potential absence has a valid factual partition but still requires review; it is never confirmed absence.

## 4. Event reconciliation algorithm

Sort by OccurredAtUtc, then EventId for deterministic presentation. Pair explicit IN with its following unambiguous OUT. Multiple pairs are supported. Duplicate IN, orphan OUT, Unknown, unfinished IN and same-instant groups create review findings; ambiguous segments are not paired. An OUT closes an ambiguous segment, allowing subsequent independent valid pairs to be reported diagnostically. GUID order never assigns temporal meaning to ties. Source evidence is never rewritten or fabricated.

Only the requested Bangkok business date is queried. Cross-date/overnight continuation is not inferred from another date; unresolved direction evidence requires review. Calendars already prohibit overnight work intervals.

## 5. Precision/duration contract

All interval union, intersection, subtraction and partition checks operate on exact DateTime ticks. Original datetime2(7) values remain unchanged. No floating-point duration arithmetic is used.

Each aggregate interval duration is summed exactly, then divided by TimeSpan.TicksPerMillisecond using integer truncation. Conversion is per aggregate, not per fragment. Exact tick coverage is authoritative:

ScheduledTicks = PresenceTicks + ApprovedLeaveTicks + UnexplainedTicks.

The API identity is:

ScheduledMilliseconds = PresenceCoveredScheduledMilliseconds + ApprovedLeaveCoveredScheduledMilliseconds + UnexplainedScheduledMilliseconds + CoverageTruncationResidualMilliseconds.

Residual is nonnegative precision metadata. It is not attendance, Leave, absence, undertime, payroll, KPI or disciplinary time and does not itself trigger review. A 100 ns arrival gap can report 0 unexplained milliseconds and a 1 ms residual while its exact endpoints and raw variance of 1 tick remain visible. Multiple partition fractions are aggregated before truncation.

## 6. Schedule coverage

The existing expected-work resolver is reused inside the caller's coherent transaction. There is no IsDefault fallback or assumed eight-hour workday. Weekly/split intervals, lunch gaps and replacement ExceptionalWorkingDay intervals retain their existing meaning. Observed presence outside the schedule is shown in observed pairs/totals, never counted as scheduled coverage or interpreted as overtime. No normal-school calendar was seeded; 07:30â€“16:00 appears only in temporary verification fixtures.

## 7. Leave reconciliation

Only Approved date-overlapping headers are loaded, followed by one batched allocation query. LeaveSnapshotIntegrity validates each frozen snapshot against its header/allocations. ChargedIntervals for the requested date are consumed unchanged; Days and date ranges do not manufacture coverage. Invalid/legacy snapshots produce LeaveSnapshotInvalid and unavailable category totals.

D8C already prevents overlapping Pending/Approved charged intervals. A defensive ApprovedLeaveOverlap finding protects against inconsistent stored evidence without choosing a winner. PresenceLeaveOverlap preserves both sources and exact overlap; no automatic cancellation or precedence is applied. LeaveScheduleMismatch preserves the frozen schedule and current expected work, exposes diagnostic intersections, and withholds authoritative partition totals.

## 8. Paid/Unpaid treatment

Classification comes only from frozen snapshot IsPaid. Paid and unpaid intersected scheduled totals are separate factual dimensions. BalanceTracked, available balance and sandwich debits do not change classification. No Leave balance query/write, policy recalculation, entitlement conversion or manufactured sub-minute Leave was introduced.

## 9. Grace/late calculation

The current approved five-minute policy applies to the first schedule boundary not covered by Approved Leave. Exact tick comparisons implement the inclusive grace boundary: 07:35:00.0000000 is not late; 07:35:00.0000001 is late for a 07:30 start.

RawStartVarianceTicks preserves the exact nonnegative arrival delay; RawStartVarianceMilliseconds independently truncates it. Early arrival has zero delay, with the exact first presence timestamp retained. Grace is not subtracted. Null timing/classification means no usable required boundary/presence or conflicting evidence, not a late/no-late guess.

Morning Leave ending 09:00 shifts the boundary to 09:00. An 08:55 arrival is not late; its overlap with Leave still requires coverage review. Arrival facts remain available where that overlap does not make the arrival boundary ambiguous. Metadata identifies D9C-v1 and FiveMinuteClockInGrace-v1 as current calculation contracts, not persisted historical policies.

## 10. Unexplained-time calculation

Subtract the exact union of scheduled presence and Approved Leave from expected work. Interval details preserve start gaps, internal gaps and departure tails. Scheduled lunch/nonworking gaps are excluded. Early departure covered by Leave is not unexplained time. Conversion residue is never added to unexplained coverage.

## 11. Potential-absence boundary

An unambiguous scheduled day with zero scheduled presence and zero Approved Leave coverage exposes PotentialAbsence and RequiresReview. Full-day Approved Leave and Ready zero-work days never become potential absence. Reads are provisional as of ObservedAtUtc, including current/future dates; they do not assert that attendance evidence is final or that an absence has occurred.

## 12. Review/readiness findings

Implemented findings: MissingClockIn, MissingClockOut, DuplicateDirection, UnknownDirection, ExactTimestampConflict, PresenceLeaveOverlap, ApprovedLeaveOverlap, LeaveScheduleMismatch, LeaveSnapshotInvalid and PotentialAbsence. Existing expected-work readiness findings are propagated, including NotEmployed, WorkCalendarNotConfigured and ConfigurationConflict. No disciplinary finding was introduced. Intake anomalies remain visible in the raw event DTOs.

## 13. API/read model

`GET /api/employees/{employeeId}/attendance-days/{date}`.

200 returns the calculated DTO including readiness; 404 for an unknown employee; 400 for malformed dates or 0001-01-01, which cannot safely represent Bangkok midnight in UTC; 409 asks for a retry if SQL reports a deadlock. Swagger documents purpose, parameters, DTO and all four response codes. There are no daily-result mutation routes. Inactive employees can be inspected for historical employment.

## 14. Historical limitations before D9D

Each response is a current coherent read, not a stored/finalized historical attendance document. Frozen Leave evidence and immutable event IDs/timestamps are retained, but mutable calendar configuration or new/cancelled evidence can change a later read. ObservedAtUtc and contract identifiers explain that boundary. D9D must own any finalization, historical policy/schedule freeze, correction and reopening design.

## 15. Concurrency/coherent-read behavior

One Serializable transaction locks Employee first, then assigned calendars in sorted order, and reads bounded date employment/assignments/schedule/events and Approved Leave/allocations. No nested transaction or payroll-period lock is acquired. Existing D9B expected-work code was extracted into an internal transaction-sharing reader without changing its query/resolution behavior.

Event intake and Leave lifecycle writers already lock Employee first; their writes serialize with this read. Calendar locks and Serializable range reads protect configuration consistency. Live races verified coherent event intake and Approved Leave cancellation views. SQL deadlock 1205 returns a retryable 409; no partial result is returned.

## 16. Migration status

No migration created, modified or applied. No entity, EF mapping, DbContext or database schema change. Application tables remain 74; no AttendanceDay/finalization table was introduced.

## 17. Test results

Final pure suite: 86 D9C assertions; 777 total existing-plus-D9C regression assertions, no SQL connections/writes.

Final focused live D9C suite: 116 checks, including exact source precision, grace endpoints, zero/100 ns/multiple-fragment residue, mixed presence/Leave/unexplained coverage, full-day paid/unpaid Leave, disjoint Approved requests, Pending/Cancelled exclusion, malformed evidence, ties, out-of-order ingestion, holidays/replacement/split schedules, midday/tail gaps, frozen/current mismatch, invalid legacy evidence, coherent event/Leave races, Swagger and read non-mutation. Exact cleanup passed.

D9B live suite: 122 checks, exact cleanup passed.

| Regression suite | Passed checks |
|---|---:|
| D8B/D1 | 117 |
| D8C | 246 |
| D8C lifecycle races | 230 |
| D8D evidence/sandwich | 523 |
| D8D capped-debit focus | 391 |
| D5A payroll classification | 70 |
| D5C Section 33 integration | 306 |
| D6B PIT foundation | 76 |
| D6C contracts | 48 |
| D6D PIT calculator | 38 |
| D6E PIT integration | 88 |
| D6E boundaries | 128 |
| D7 operations/lifecycle/payslips | 84 |

All suites passed. Payroll subtotal: 838. Together with 777 pure, 116 D9C live and 122 D9B live checks, the primary total is **3,360**, with additional wrapper baseline guards. Existing regression scenario expectations were unchanged; the existing D9B wrapper retains its approved table-count/prerequisite adaptations. Full and nested wrappers all verified exact cleanup.

Restore succeeded; final Release build succeeded with 0 warnings/0 errors. EF reports no pending model changes. git diff --check passed. New source/report files were also checked for trailing whitespace, since untracked files are outside ordinary git diff output.

The API log includes the inherited EF warning about unordered Take(2) in D9B ambiguity probes. Those probes reject multiple matches rather than selecting an authoritative winner; this existing behavior/query shape was preserved. This is not a build warning or a new calculation failure.

An initial live run correctly encountered the existing Leave integrity guard because a deliberately corrupt fixture remained present during a subsequent Leave test. The fixture was isolated and removed before the later test; the full suite then passed. No production code workaround was introduced.

## 18. Leave non-impact

No Leave implementation changed. Existing whole-minute boundaries, historical IsPaid, validation, approval/cancellation, evidence prerequisites, entitlement balances and sandwich contracts remain intact. Live repeated daily reads compared every row/timestamp unchanged. D8D ScheduledMinutes=0 sandwich dates are not read as attendance missing work; the calculator has no sandwich-debit dependency.

## 19. Payroll non-impact

PayrollCalculationService, PayrollGenerationService, PayrollPreviewService, D3 entitlement and D4â€“D7 policy/statutory/operation behavior were not changed. D9C references no payroll service or table. Live SQL confirmed no payroll mutation from daily calculations; established monetary/lifecycle suites provide regression verification.

## 20. Legacy Attendance non-impact

No legacy Attendance write or status assignment. Existing read compatibility and retired writes remain unchanged; D9B regressions verify them. Baseline legacy Attendance remains empty.

## 21. Deferred D9D/D9E decisions

Attendance finalization/reopening, correction workflow, confirmed absence, immutable historical policy snapshots, disciplinary/KPI actions, deductions, overtime pay, biometric/vendor integration and production authentication/RBAC remain deferred. No new unresolved stop decision remains from this implementation.

## 22. Exact baseline cleanup

D9C and D9B suites restored all 74 application-table row snapshots, including original timestamps and employee core data. No changes to TEST-EMP-001 were needed; it remains inactive, ID 433f2c1a-6222-494f-a64f-cd0c31126dc4.

| Table | Final rows |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| PayrollComponents | 17 |
| AttendanceEvents | 0 |
| Attendance | 0 |
| EmployeeWorkCalendarAssignments | 0 |
| WorkCalendars | 0 |
| WorkCalendarWeeklyIntervals | 0 |
| WorkCalendarDateOverrides | 0 |
| WorkCalendarOverrideIntervals | 0 |
| LeavePolicies | 0 |
| EmployeeLeave | 0 |
| EmployeeLeaveAllocations | 0 |
| EmployeeLeaveEntitlements | 0 |
| EmployeeLeaveEntitlementAdjustments | 0 |
| PayrollRules | 0 |
| PayrollRuleTargets | 0 |
| PayrollSettings | 0 |
| PayrollPeriods | 0 |
| EmployeeCompensations | 0 |
| EmployeePayrollComponentAssignments | 0 |
| EmployeePayrolls | 0 |
| EmployeePayrollLines | 0 |

All Leave evidence/sandwich tables and other employee/statutory/payroll transactional tables are also empty. The 18 original master tables retain 172 total rows. There are 38 applied migrations, latest still `20261003175821_AddAttendanceEvidenceFoundation`. Attendance event orphan count is 0. Final baseline API reads confirmed WorkCalendarNotConfigured (no assignment was seeded) and empty legacy Attendance. The task-owned Development API process was stopped after verification.

## 23. Complete changed-file list

- D9C-REPORT.md
- src/SIAMIS.Api/Program.cs
- src/SIAMIS.Api/Controllers/AttendanceDaysController.cs
- src/SIAMIS.Application/Employees/AttendanceDayContracts.cs
- src/SIAMIS.Infrastructure/Services/AttendanceDayCalculator.cs
- src/SIAMIS.Infrastructure/Services/AttendanceDayService.cs
- src/SIAMIS.Infrastructure/Services/AttendanceFoundationService.cs
- tests/SIAMIS.Payroll.RegressionTests/Program.cs
- tests/SIAMIS.Payroll.RegressionTests/D9CDailyAttendanceTests.cs
- tests/verify_d9c_live.py

Generated logs/results and baseline snapshots are ignored artifacts beneath tests/SIAMIS.Payroll.RegressionTests/bin, not additional source changes.

## 24. Verification/status table

| Check | Result |
|---|---|
| D9C implementation / Swagger | Complete / verified |
| Exact tick partition / millisecond residual / grace | Passed pure and live verification |
| Pure tests | 777, including 86 D9C |
| D9C / D9B live checks | 116 / 122 |
| D1/Leave regressions | 1,507 |
| Payroll live regressions | 838 |
| Primary total | 3,360, plus baseline wrapper guards |
| Exact Development cleanup | All 74 tables match original rows/timestamps |
| Restore / final Release build | Succeeded / 0 warnings, 0 errors |
| EF pending-model-changes check | None |
| git diff --check / new-file whitespace check | Passed |
| New migration / model change / attendance persistence | None |
| Payroll / Leave / legacy behavior changes | None |
| New unresolved stop condition | None |
| Remaining boundaries | D9D/D9E finalization, correction and downstream behavior; explicit calendar configuration needed |
| Changed source/report files | 10 |
| Commit / push | Neither performed |

No commit or push performed.
