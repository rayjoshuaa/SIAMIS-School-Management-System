# D9E — Operational Attendance, History & Reporting Foundation

## 1. Executive summary

Implemented five read-only APIs for daily/today organization attendance, employee history, official period summaries and a combined attention/finalization queue. Reuses D9B expected-work resolution, D9C interval calculation and D9D source fingerprints, review decisions and finalized snapshots. No schema changes, migration, frontend, authentication, payroll or Leave behavior changes.

Current presence is explicitly `Unknown`; completed interval evidence is not repurposed into a new open-interval algorithm. No unresolved policy decision was required.

## 2. Repository/database state inspected

Started from clean commit `c55ebf8` (completed D9D). Inspected attendance foundation/calculation/review services, contracts, entities/configurations, employment integrity, Leave snapshots, API conventions and existing regression runners.

Local Development target: SQL Server `localhost`, database `SIAMIS`, integrated authentication. Initial inspection captured exact rows, including timestamps, from all 77 application tables and all 39 applied migrations. Last applied migration: `20261004033520_AddAttendanceReviewFinalization`.

Baseline: one inactive `TEST-EMP-001` (`433f2c1a-6222-494f-a64f-cd0c31126dc4`), one current EmploymentRecord, 172 master-data rows across 18 tables; remaining 57 tables empty. Actual D9D tables are `AttendanceReviewCases`, `AttendanceReviewActions`, `FinalizedAttendanceRevisions`; conceptual table names in the request were mapped to these existing tables.

## 3. Live vs finalized vs stale semantics

Every row separates `Live` facts from nullable `Official` facts. Official facts exist only for the latest finalized revision whose frozen source fingerprint still matches current authoritative sources and which has not been reopened. Official facts come from that frozen calculation, not a current recalculation.

Stale revisions retain their historical ID/number/time and structured changed-source findings. Reopened revisions remain historical but are excluded from official totals until a new valid revision exists. No fallback to an older revision. Malformed/unsupported frozen snapshots produce `SnapshotInvalid` and a review finding instead of failing the entire dashboard or using unsafe totals. Reads never reopen, refinalize, create review cases or repair snapshots.

## 4. Operational status model

Orthogonal typed states:

- Work: `Scheduled`, `NotScheduled`, `ConfigurationRequired`.
- Timing: `Unknown`, `NotApplicable`, `OnTime`, `Late`.
- Record: `Live`, `UnfinalizedPastDay`, `Finalized`, `Stale`, `Reopened`, `SnapshotInvalid`.
- Leave: `None`, `Paid`, `Unpaid`, `Mixed`; extent separately indicates None/Full/Partial/Unknown.

Separate factual fields expose day relation, schedule/arrival window, provisional potential absence, confirmed absence, review requirements, readiness to finalize, stale/reopen flags and attention categories. Counts overlap and must not be summed as mutually exclusive populations.

## 5. Daily organization overview

Selected-date and today APIs return business date/timezone, one UTC read timestamp, effective population count, matching pre-pagination counts and paged employee rows. Rows include employee number/name, effective department/designation, schedule, privacy-minimized event observations, first observed IN/last observed OUT, grace/variance, Leave coverage, review and latest historical revision metadata.

Today is derived from UTC through the existing Asia/Bangkok resolver, never server-local date. Schedule and arrival windows distinguish before-start, active interval, unscheduled gap and after-end without introducing an absence-onset policy.

## 6. Employee history

Required inclusive `from`/`to`, up to 366 dates; returns effective-employment dates only, ascending and unique. Each date exposes the same live/official distinction and structured configuration/review findings. Existing employee outside employment returns no manufactured dates; unknown employee returns 404.

## 7. Period/month summary

Same bounded inclusive range. Returns requested/effective date counts, scheduled/finalized/unfinalized dates, stale/reopened/configuration/review counts and official OnTime/Late/ConfirmedAbsence/Leave/activity counts. Duration totals are wide integer milliseconds, including scheduled, presence, paid/unpaid Leave, unexplained and precision residual. No arbitrary hours-to-days conversion.

## 8. Official summary/completeness contract

Only latest currently validated frozen revisions contribute official attendance totals. Missing finalized scheduled dates, configuration failures, stale/reopened or invalid snapshots make `IsComplete=false`. Non-working dates do not require finalization merely to establish completeness; any official non-working activity count still requires a valid finalized revision.

`RequestedDates` and `EffectiveEmploymentDates` make an empty effective population explicit. Completeness does not assert employment outside those dates. Operational quality counts and official duration metrics remain separate; no live-to-official fallback.

## 9. Review/finalization queue

One computed queue serves missing/incomplete evidence, potential absence, configuration, stale/reopened state, past unfinalized work and ready-to-finalize days. Returns attention categories, findings, changed-source details and existing case state. It creates no persistent queue/case records. ReadyToFinalize is a factual projection, not a replacement for D9D finalization validation. Stale revisions require explicit D9D reopening.

## 10. Effective employee population

Uses existing D1 inclusive employment start/end and overlap rules. Historical employment inclusion ignores present Employee.IsActive. Before-start, after-end and future employment are excluded; closed employment includes its ending date. Ambiguous effective contexts remain structured configuration findings. Department/designation filters and labels come from employment effective on each date, not an invented frozen personnel history.

## 11. Leave visibility

Reads D8 frozen Approved Leave allocations and Paid/Unpaid classification via existing snapshot integrity. Pending/rejected/cancelled requests do not supply current coverage. Outputs only contributing Leave ID, Paid flag and charged whole-minute intervals. Contributing cancellation surfaces D9D changed-source findings and excludes stale official facts. No Leave reason, medical metadata or document details exposed.

## 12. Late/absence semantics

Reports existing D9C exact 5-minute grace and independent raw start variance. Exact boundary is not late; one tick beyond is late. PotentialAbsence is provisional and may be visible before a shift with factual window metadata. It never becomes ConfirmedAbsence automatically. Confirmed absence requires existing source-bound D9D review. Unpaid Approved Leave is separate from absence; no warnings, deductions or performance score.

## 13. Non-working/calendar semantics

Existing explicit EmployeeWorkCalendarAssignment only; no IsDefault fallback, assumed weekday schedule or 8-hour day. Reuses weekly schedules, holidays, non-working overrides and exceptional replacement intervals. Non-working activity is factual and never overtime/extra pay. Missing configuration is not NotScheduled. Later calendar changes stale historical finalizations rather than rewriting their frozen expected schedule.

## 14. Filtering/pagination/range limits

Typed organization filters: DepartmentId, DesignationId, Scheduled, Late, HasApprovedLeave, ConfirmedAbsent, RequiresReview, IsStale, Unfinalized and RecordState. Fact filters operate on the documented live/current state; Official remains separate. `scheduled=false` includes only resolved NotScheduled, not unknown configuration; `late=false` excludes unknown timing.

- Daily: maximum 500 candidate employees.
- Queue: required range up to 31 inclusive dates, maximum 500 candidates and 2,000 employee/date calculations.
- Employee history/summary: required range up to 366 inclusive dates.
- Page defaults to 1; PageSize defaults to 20, allowed 1–100.
- Bounded source collections: 20,000 rows each; excess returns validation error, never silent truncation.

Narrow department/designation/range before exceeding budgets. Daily order is EmployeeNumber then EmployeeId; queue adds BusinessDate first. Counts cover matched rows before pagination. Queue EffectiveEmployees counts distinct people, other counters count employee/dates. Empty/invalid ranges, IDs, enums and pagination return 400.

## 15. Performance/query behavior

Bounded candidate selection precedes a serializable bulk read transaction. Employee rows are locked first in deterministic PK order; cohort is rechecked inside the transaction, then calendar/source rows loaded in batches. Read projections use AsNoTracking and bounded ordered queries. In-memory employee/date and calendar/date lookups reuse authoritative resolution/calculation; no per-employee/date database queries.

Measured live SQL command counts: 17 for one employee/date, 17 for eleven employees/date, and 17 for eleven employees over twenty dates (220 calculations). This demonstrates constant query count for the exercised cohort, not a production load benchmark. Source volume and calculated cohort remain explicitly bounded. No cache or durable summaries. Concurrent cohort change/deadlock can return 409 for retry rather than inconsistent source results. Reads intentionally take coordination locks but issue no data mutation.

## 16. Precision

Reuse exact D9C ticks, original datetime2(7) event timestamps and independently truncated bigint millisecond aggregates. Period totals sum frozen aggregates consistently. Residual remains a separate precision field and never becomes unexplained time, Leave, presence, absence, Payroll or KPI. Pure/live tests exercise one-tick grace and residual, coverage identity and totals above Int32 capacity. D8 whole-minute Leave remains unchanged.

## 17. Privacy/security boundary

GET-only controller, no fake authenticated identity or new mutation. Existing D9D Development-only mutation guards untouched. Response contracts omit salary, bank data, private documents, medical evidence, actor/reason audit fields and Leave policy/private details. Future authentication must enforce HR/management access for organization views and scoped employee access for individual views; supplied employeeId is not identity. These endpoints do not establish production RBAC.

## 18. API contracts

| Method | Route | Response |
|---|---|---|
| GET | `/api/attendance/days/{date}` | AttendanceOverviewDto |
| GET | `/api/attendance/today` | AttendanceOverviewDto |
| GET | `/api/employees/{employeeId}/attendance-history?from=&to=` | AttendanceHistoryDto |
| GET | `/api/employees/{employeeId}/attendance-summary?from=&to=` | AttendanceSummaryDto |
| GET | `/api/attendance/review-queue?from=&to=` | AttendanceAttentionQueueDto |

Application DTOs/interface, Infrastructure batched service/projection, API routing only. Success 200, invalid queries 400, unknown employee 404, genuine read coordination conflicts 409. Swagger verifies all five GET operations, response schemas and nullable Official. POST/PUT/PATCH/DELETE on the new day route return 405. Structured contracts are reusable for a future export; no CSV/Excel/PDF export added.

## 19. Database/migration status

No entities, EF configurations, DbContext, model snapshot or migration files changed. No migration created or applied. Existing 77-table schema and 39-entry migration history retained. EF pending-model verification recorded below.

## 20. Leave/sandwich non-impact

No entitlement, reservation, lifecycle, Paid/Unpaid policy, sandwich case or debit changes. Reads consume charged Approved Leave boundaries only; sandwich non-working dates remain non-working Attendance, not absence. Prior D8B/C/D and sandwich tests rerun unchanged in expectations.

## 21. Payroll non-impact

No Payroll service/model/API behavior changes, payroll calls from reporting, deductions or overtime. Existing monetary/statutory/snapshot/lifecycle regression suites rerun. Transactional tables restored to empty baseline.

## 22. KPI/disciplinary non-impact

No KPI gate, points, disciplinary escalation, lateness conversion or termination logic. Counts are attendance facts and data quality only; stale/reopened state is not an HR violation.

## 23. Legacy Attendance non-impact

Legacy Attendance remains non-authoritative and empty. D9E does not read it for calculations or restore writes; D9B retirement regressions preserved.

## 24. Test results

Focused D9E live verification passed 201 assertions covering daily/history/summary/queue, employment boundaries, filters/paging, exact grace/residual, Paid/Unpaid/mixed Leave, incomplete/corrected/adjudicated evidence, confirmed absence, immutable revisions, cancellation staleness, reopen/revision 2, calendar changes, privacy, Swagger, query counts and exact cleanup.

All suites completed successfully, sequentially against local Development. Totals below count assertions, not independent test cases; the 39 new D9E pure assertions are included in the 852 pure total.

| Suite | Passed assertions |
|---|---:|
| Pure regression executable, including 39 D9E | 852 |
| Focused D9E live API/SQL | 201 |
| D9D review/finalization | 174 |
| D9B evidence/calendar/legacy retirement | 122 |
| D9C calculation/reconciliation | 116 |
| D8B/D1 foundation | 117 |
| D8C lifecycle/balance | 246 |
| D8C ExpectedStatus concurrency (20 races) | 230 |
| D8D evidence/sandwich | 523 |
| D8D capped sandwich | 391 |
| D5A classification | 70 |
| D5C SSO integration | 306 |
| D6B PIT treatment | 76 |
| D6C claims/policy | 48 |
| D6D PIT calculation | 38 |
| D6E persistence | 88 |
| D6E boundaries | 128 |
| D7 operations/lifecycles/payslips | 84 |
| **Scenario assertion total** | **3,810** |

Live scenario subtotal: 2,958; prior live regression subtotal excluding new D9E: 2,757. Regression orchestration additionally passed 20 baseline/wrapper checks; final independent SQL comparison passed three checks. Swagger and performance assertions are included in focused D9E, not counted twice. All result artifacts report `error=null`.

Obsolete baseline table-count assertions are adapted by the regression wrapper; original scenario files, monetary and Leave expectations remain unchanged. The focused fixture initially used an overlength temporary employee number and was corrected to respect the existing 30-character constraint; that run restored baseline before rerunning successfully. The ignored final-check helper's source path was corrected before its successful read-only rerun. Neither required production behavior or schema changes.

Ignored local verification artifacts reside under `tests/SIAMIS.Payroll.RegressionTests/bin/`: d9e baseline/migrations snapshots, live results, regression results/logs and final database results. They contain Development verification data and are not source changes.

## 25. Exact baseline cleanup

Temporary fixtures include UUID-tagged employees/employment, explicit calendars/assignments/overrides, Leave policies/requests/allocations, raw evidence and D9D review/revision rows. Cleanup targets recorded fixture IDs and respects FK order. A deliberate malformed snapshot fixture is restored in a finally block before cleanup. No employee core baseline fields are changed.

Each regression suite and final verification compared complete contents of all 77 application tables, including original IDs/values/timestamps. Final equality succeeded. TEST-EMP-001 remains inactive, PreferredName Test Updated, with the original EmployeeId and timestamps. All 39 migration identifiers remain identical.

Final nonempty table counts:

| Table | Rows | Table | Rows |
|---|---:|---|---:|
| Employees | 1 | EmploymentRecords | 1 |
| Departments | 12 | Designations | 20 |
| EmploymentTypes | 6 | EmploymentStatuses | 9 |
| ContractTypes | 7 | Locations | 5 |
| HiringSources | 10 | Genders | 4 |
| MaritalStatuses | 6 | Nationalities | 13 |
| DocumentTypes | 15 | LeaveTypes | 10 |
| AttendanceStatuses | 11 | PayTypes | 6 |
| PayrollComponents | 17 | PerformanceRatings | 5 |
| AddressTypes | 3 | Countries | 13 |

All remaining 57 application tables have zero rows, including AttendanceEvents, AttendanceReviewCases, AttendanceReviewActions, FinalizedAttendanceRevisions, legacy Attendance, every Leave/calendar fixture table, every payroll transaction/rule/setting table and statutory configuration/result tables. Complete per-table counts are retained in the ignored `d9e-final-database.json` artifact. No temporary organization profile, payslip, enrollment, tax declaration or employment record remains. Total master rows remain 172.

## 26. Deferred device/import integration

No device polling, biometric enrollment, ingestion adapters, deduplication workflow or import UI. Existing immutable raw evidence provenance is only reported.

## 27. Deferred D10 work

React/other frontend, authenticated employee identity/RBAC, authorized production mutations, exports, device ingestion and any approved downstream payroll/KPI/disciplinary policy integration remain future checkpoints. Present-now semantics require a separately approved deterministic open-interval contract. No such policy was invented here.

## 28. Complete changed-file list

Modified:

- `src/SIAMIS.Api/Program.cs` — reporting service and TimeProvider registration.
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs` — run focused pure reporting assertions.

Created:

- `src/SIAMIS.Api/Controllers/AttendanceReportingController.cs`
- `src/SIAMIS.Application/Employees/AttendanceReportingContracts.cs`
- `src/SIAMIS.Infrastructure/Services/AttendanceReportProjection.cs`
- `src/SIAMIS.Infrastructure/Services/AttendanceReportingService.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D9EReportingTests.cs`
- `tests/verify_d9e_live.py`
- `tests/verify_d9e_regressions.py`
- `D9E-REPORT.md`

No commit or push performed.

## 29. Verification/status table

| Verification | Result |
|---|---|
| `dotnet restore .\SIAMIS.sln` | Passed; all projects up to date |
| `dotnet build .\SIAMIS.sln --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| Pure Release regression executable | 852 passed, including 39 D9E; no database writes |
| D9E live API/SQL | 201 passed |
| D9B/C/D, D8/Leave/sandwich, Payroll live regressions | 2,757 passed; exact restoration after each suite |
| Swagger and privacy | Five typed GET contracts verified; no leaked private fields |
| N+1 measurement | 17 commands for 1, 11 and 220 employee/date calculations |
| EF Release `has-pending-model-changes --no-build` with Development | No changes since last migration |
| Migration history | Exact same 39 entries; no migration created/applied |
| Independent SQL baseline comparison | Exact equality of all 77 application tables and timestamps |
| TEST-EMP-001 | Only employee; inactive; unchanged |
| Leave/calendar/Attendance/Payroll fixture cleanup | Complete; original empty baselines restored |
| `git diff --check` and new-file whitespace check | Passed |
| Payroll/Leave/KPI/legacy implementation changes | None |
| Git commit/push | Neither performed |

The Development API was run on localhost:5155 for verification and the agent-started process was stopped before the final build. Reporting coordination locks and bounded calculations are intentional; production load tuning and RBAC remain future work. No unresolved D9E stop condition remains.
