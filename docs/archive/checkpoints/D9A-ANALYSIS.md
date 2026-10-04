# D9A — Attendance & Time Management architecture and contract review

Date: 2026-10-04 (Asia/Bangkok). Mode: repository and local Development SQL inspection only.

**Approval status:** recommendations below are proposed contracts, not approved policy or implemented behavior. D9B should begin only after its foundation decisions in sections 29–30 are approved. Later monetary, disciplinary and irreversible behavior has separate gates.

## 1. Executive summary

SIAMIS has a small manual Attendance CRUD API, not an attendance calculation engine. It accepts one status and one optional clock pair per employee/date. It has useful employee ownership validation, DTO projection and a database uniqueness constraint, but no schedules, event provenance, leave reconciliation, audit timestamps, revisions, corrections or finalization.

Recommend retaining the existing scheduling and Leave foundations, adding immutable attendance events and versioned daily results, and replacing legacy write behavior through an explicitly approved compatibility transition. Do not derive salary, deductions, KPI penalties or confirmed absence directly from missing clocks.

Live local inspection confirmed **Attendance = 0**, **AttendanceStatuses = 11**, **Employees = 1**, **EmploymentRecords = 1**. All calendar, assignment, leave and payroll transaction tables are empty. There is no legacy Attendance data to convert locally; other deployments must be checked separately before any future destructive migration.

The smallest safe foundation is explicit Asia/Bangkok interpretation; existing effective employment/calendar contracts; immutable, replay-safe events; and readiness diagnostics. Daily reconciliation, correction/finalization, then external adapters and operational reads can follow. No legal or HR policy has been inferred.

## 2. Current repository Attendance state

| Layer | Actual implementation |
|---|---|
| Domain | `EmployeeAttendance`: AttendanceId, EmployeeId, AttendanceDate, AttendanceStatusId, nullable CheckIn/CheckOut, nullable Remarks; employee and status navigation properties |
| Application | `EmployeeAttendanceRequest`, `EmployeeAttendanceDto`, `IEmployeeAttendanceService`; required nullable DateOnly date and Guid status; optional TimeOnly clocks; Remarks maximum 2,000 characters |
| Infrastructure | `EmployeeAttendanceService`; async EF queries; AsNoTracking DTO projections; employee existence and child ownership checks |
| API | `EmployeeAttendanceController`, `/api/employees/{employeeId:guid}/attendance`; collection GET with inclusive fromDate/toDate, item GET, POST, PUT, DELETE |
| Registration | Program.cs registers interface/service and SQL Server DbContext; XML-comment Swagger in Development |
| Storage | DbSet `Attendance`; `EmployeeAttendanceConfiguration`; AddEmployeeAttendance migration and current model snapshot |

GET collection orders date descending then ID descending; there is no pagination. POST creates one row and returns 201; duplicate employee/date returns 409 including SQL unique races. PUT can replace date, status, clocks and remarks. DELETE physically removes the row. Invalid clocks are rejected only when both exist and CheckOut < CheckIn. Equal clocks, missing clocks and any active status are accepted independently; “Present” without clocks or “Absent” with clocks is therefore permitted. Overnight CheckOut < CheckIn is rejected. Existing employee existence, not active/effective employment, authorizes recording.

Preserve architecture, ownership checks, DTOs, HTTP error conventions, server-side filtering and unique day identity. Replace caller-selected authoritative status, single clock pair, unrestricted historical PUT and hard DELETE for the future authoritative workflow. Deprecate legacy writes deliberately; do not silently reinterpret the current DTO or route. Preserve legacy reads until compatibility retirement is approved.

## 3. Current database Attendance state

Read-only `sqlcmd` used Windows authentication against **localhost / SIAMIS**; SQL reports server name **Ray**. No passwords were used or exposed. Development appsettings targets localhost/SIAMIS with Trusted_Connection and TrustServerCertificate true. This SQL connection establishes local access, not a live API test or proof that every environment override matches the file.

| Actual column | SQL type | Nullable |
|---|---|---|
| AttendanceId | uniqueidentifier | No |
| EmployeeId | uniqueidentifier | No |
| AttendanceDate | date | No |
| AttendanceStatusId | uniqueidentifier | No |
| CheckIn | time(0) | Yes |
| CheckOut | time(0) | Yes |
| Remarks | nvarchar(2000) | Yes |

Actual indexes: PK_Attendance on AttendanceId; unique UX_Attendance_EmployeeId_AttendanceDate on (EmployeeId, AttendanceDate); IX_Attendance_AttendanceStatusId. Employee and AttendanceStatus FKs are **NO_ACTION**, enabled and trusted. Attendance has **no check constraints and no triggers**. SQL does not independently enforce clock order or status/time coherence.

| Baseline fact | Actual |
|---|---:|
| Application tables, excluding migration history | 73 |
| Applied migrations | 37 |
| Attendance / orphan Attendance / duplicate employee-date groups | 0 / 0 / 0 |
| AttendanceStatuses | 11, all active |
| Employees / EmploymentRecords | 1 / 1 |
| WorkCalendars / weekly intervals / overrides / override intervals / assignments | 0 / 0 / 0 / 0 / 0 |
| LeavePolicies / EmployeeLeave / allocations / entitlements / adjustments | All 0 |
| All seven D8D evidence/sandwich tables | All 0 |
| OrganizationProfiles / TeacherProfiles | 0 / 0 |
| PayrollComponents | 17 |
| Payroll rules, targets, settings, periods, headers, lines, assignments and compensation | All 0 |
| Other statutory, PIT and payslip transaction/configuration tables | All 0 |

The one employee is TEST-EMP-001, ID `433f2c1a-6222-494f-a64f-cd0c31126dc4`, IsActive=false. Its single current EmploymentRecord is `f54e469a-1bf3-4fa5-92dd-76b3e96086f7`, HireDate=2026-09-29, StartDate=null, EndDate=null. This deliberately retained test state cannot be interpreted as absence or changed during D9A.

Seeded status codes ATT-001–011: Present, Late, Absent, Half Day, Early Leave, On Leave, Holiday, Rest Day, Business Trip, Work From Home, Other. They mix presence, variance, calendar and duty concepts; they are not an adequate authoritative single-state model.

Applied Attendance migration: `20260929050408_AddEmployeeAttendance`. Latest applied migration: `20261003164508_AddCappedLeaveSandwichConsumption`. All 18 master tables total **172 rows**: Departments12, Designations20, EmploymentTypes6, EmploymentStatuses9, ContractTypes7, Locations5, HiringSources10, Genders4, MaritalStatuses6, Nationalities13, DocumentTypes15, LeaveTypes10, AttendanceStatuses11, PayTypes6, PayrollComponents17, PerformanceRatings5, AddressTypes3, Countries13. Only these tables plus Employees and EmploymentRecords are nonempty; the other 53 application tables are empty.

## 4. Existing problems and risks

- One IN/OUT pair cannot represent split work, lunch clocks or multiple departures.
- Editable date/time/status and physical deletion erase original evidence. Attendance does not implement IHasTimestamps, so DbContext's UTC timestamp convention does not apply to it.
- TimeOnly has no timezone, offset, source or overnight date context; time(0) retains seconds, whereas calendars/leave require whole minutes.
- Caller-selected status cannot express Present + Late or partial presence + approved leave without losing facts.
- No calendar or leave dependency, current-input snapshot, calculation version, finalization or reconciliation exists.
- Unique employee/date prevents duplicate headers, not provider replay. Current updates have no ExpectedStatus/revision guard or shared employee lock; last write can win.
- Legacy GET joins current status code/name, so changing master data changes presentation of historical rows.
- No authentication, actual user identity or role policy exists. “Authorized manual” must not be represented as an authenticated fact today.
- Empty Development tables make migration easy locally, but do not prove that legacy rows elsewhere have trustworthy times or provenance.
- New material ambiguity: approved leave freezes schedules, but calendar intervals are not published immutable revisions. Later schedule changes can disagree with approved leave. Attendance must surface this conflict.
- Existing test employee is inactive with open current employment. IsActive and date-effective employment are separate signals; no automatic repair or attendance exclusion is justified.

## 5. Work-calendar integration

Reuse `EmploymentIntegrity.Start(record) = StartDate ?? HireDate`; employment and assignment effective bounds are inclusive. Resolve exactly one valid employment record for the business date, then exactly one explicit effective EmployeeWorkCalendarAssignment. Do not use IsCurrent as historical selection, or IsActive as the sole historical eligibility gate.

Resolve WorkCalendar and that date's override. PublicHoliday, SchoolHoliday and RestDay yield no working intervals. ExceptionalWorkingDay intervals **replace** weekly intervals. With no override, resolve sorted weekly intervals. Preserve multiple nonoverlapping intervals and unpaid gaps; no fixed daily school hours or 480-minute assumption.

`LeaveFoundationService.ResolveCalendarAsync` already resolves assignment/override and deliberately ignores IsDefault and current calendar IsActive for historical assignments. It does **not** establish effective employment first and does not expose all interval IDs. Attendance needs a purpose-specific combined resolver; calling this HTTP API or the Leave request calculator is not a substitute. Reuse underlying contracts/helpers without tying schedule resolution to a LeaveType, published LeavePolicy or entitlement.

| Condition | Proposed readiness/result |
|---|---|
| Employee absent | 404 |
| No effective employment | NotEmployed, no absence; retain received evidence if any |
| Multiple/invalid employment records | ConfigurationConflict; cannot finalize |
| No explicit assignment | WorkCalendarNotConfigured; scheduled time unknown, not zero work |
| Multiple assignments / missing calendar / invalid intervals | ConfigurationConflict; cannot finalize |
| Valid calendar, no intervals on date | NonWorking with zero scheduled minutes |
| Future assignment | Apply only within its inclusive bounds |
| Inactive current calendar with established historical assignment | Preserve existing resolution convention; no implicit reassignment |

Assignments are effective-dated; SQL date checks plus employee locking protect service overlap checks, not a SQL interval-exclusion constraint. Calendar metadata is mutable; weekly intervals/overrides are currently added through focused POST routes. A late override/addition can change a past day's schedule. Freeze resolved facts per result revision and require explicit revision for historical updates. No organization-default history is needed.

## 6. Leave integration

Consume only **Approved**, integrity-valid D8C leave using `LeaveSnapshotIntegrity.Read` with its relational allocations. The snapshot stores business timezone, employment/assignment/calendar identifiers, resolved schedule, per-date **ChargedIntervals**, policy evidence, IsPaid and BalanceTracked. Do not interpret StartDate/EndDate or compatibility Days as full-day coverage.

Clip frozen approved charged intervals to the attendance schedule only after validating schedule consistency. Union multiple disjoint requests without double counting. Pending leave is visible as a review warning but does not excuse working time; Rejected/Cancelled contributes no current coverage. Missing/legacy/corrupt evidence must produce a readiness/review finding, not guessed paid classification or silently ignored approved leave.

If frozen leave employment/calendar/schedule evidence disagrees with attendance's candidate schedule, recommend ConfigurationConflict and explicit HR review. Do not choose the latest mutable calendar, substitute leave's schedule for an entire day automatically, or recalculate Leave. Finalized attendance retains its observed leave state/intervals; cancellation later requires a proposed attendance revision, not editing history.

Example: schedule 08:00–12:00,13:00–16:00; presence 08:00–12:00,13:00–14:00; Approved charged leave 14:00–16:00. Scheduled=420 minutes; scheduled presence=300; approved leave=120; unexplained missing=0. Lunch adds no scheduled minutes. Entire approved schedule coverage yields no attendance requirement for those intervals.

Work during approved leave is a review discrepancy; preserve presence and paid/unpaid leave facts independently. Coverage union avoids double subtraction, but a report must not pretend overlapping presence and leave are disjoint. Attendance never writes entitlements, allocations, leave status or evidence acceptance. D8D approval prerequisites remain Leave's responsibility.

## 7. Timezone contract

Recommend **Asia/Bangkok**, matching `LeaveRequestCalculator.BusinessTimeZone`. OrganizationProfile currently contains display/contact metadata only, no timezone configuration. Reuse a single explicit business-time contract when implementing; do not add speculative multi-timezone organization configuration.

- Device instants normalized to UTC; event OccurredAtUtc and ReceivedAtUtc are different server/source facts.
- Preserve original source timestamp text, declared timezone/offset and normalization provenance. Reject/quarantine ambiguous local source times rather than use the machine timezone.
- Store business date and timezone used at normalization; local display is explicit UTC conversion. DateOnly/TimeOnly schedule is combined with an Unspecified local DateTime and converted using the approved timezone, as Leave already does.
- Audit fields use DateTime.UtcNow/datetime2 and explicit UTC kind on materialization; datetime2 itself does not encode timezone.
- Determine “today” from UTC converted to Asia/Bangkok. EmployeeService's existing birth/hire validation uses the UTC date; it is not an attendance clock contract and should not be changed in this checkpoint.
- Preserve original timestamp precision; exact duration-to-minute presentation/rounding remains an approval item. Do not truncate device seconds to Leave's whole-minute schedule precision.

No machine-local DateTime.Now/Today/ToLocalTime usage was found in the inspected source search. Existing Attendance timestamps are unspecified local clock values, not normalized instants.

## 8. Proposed raw event model

Immutable `AttendanceEvent` is observation evidence, not final presence/discipline. Suggested fields: Guid ID, EmployeeId, OccurredAtUtc, BusinessDate, BusinessTimeZone, Direction, Source, SourceKey, nullable ExternalEventId, OriginalSourceTimestamp, SourceOffset/zone metadata, ReceivedAtUtc, nullable applied CorrectionId and nullable original-event reference. Manual authorized entry requires a reason and server-determined provenance.

Initial Direction: In, Out, Unknown. Unknown is stored but cannot be guessed into a pair. Source: ManualAuthorized, Device, Imported; Web/Mobile only when their contracts exist. SystemDerived belongs to result provenance, not an assertion that a clock was observed. Device/Imported require a stable source namespace and external ID at the future ingestion boundary.

Unique filtered (SourceKey, ExternalEventId) across employees for external delivery: same identity/same normalized payload is idempotent; same identity/different employee, direction or instant is a conflict. Do not include EmployeeId in this uniqueness key, which would permit reassignment of a replay. Manual requests need their own required idempotency key. No heuristic “within two minutes” deletion or uniqueness on employee/time.

Append evidence only. Replacing/voiding a clock requires an approved correction; keep original evidence. A late-delivered event does not mutate a finalized result. No vendor payload graph or biometric material belongs here.

## 9. Proposed daily attendance model

Separate `AttendanceDay` identity/current pointers from immutable `AttendanceDayRevision` calculations. Unique employee/business date; revisions numbered deterministically. A revision contains input cutoff, calculator version, timezone, schedule/leave/event evidence, reconciled intervals, objective quantities and findings. A finalized revision cannot be edited.

Use independent dimensions:

| Dimension | Smallest proposed meaning |
|---|---|
| Readiness | Ready, NotEmployed, WorkCalendarNotConfigured, ConfigurationConflict |
| Schedule kind | Working, PublicHoliday, SchoolHoliday, RestDay, WeeklyNonWorking, ExceptionalWorkingDay |
| Clock quality | Complete, Incomplete, Ambiguous, NoEvents |
| Lifecycle | Open, Finalized |
| Findings | Late variance, unexplained gap, missing IN/OUT, duplicate direction, pending correction, leave/schedule conflict, presence during leave, late new input |

RequiresReview is derived from findings and unresolved review decisions, rather than an overloaded presence status. Presence, leave coverage and variance can coexist. “Absent” should only be a reviewed classification for uncovered scheduled time after the day is complete; it is not a raw absence-of-events enum.

Suggested factual quantities: ScheduledDuration, ObservedPresenceDuration, ScheduledPresenceDuration, ApprovedLeaveDuration with paid/unpaid breakdown, overlap duration, UnexplainedMissingDuration and arrival/departure variances. Presence outside schedule is reported separately and is **not overtime or payable time**. Prefer precise duration storage; section 30 proposes a decision on seconds/minute presentation.

## 10. Event reconciliation and calculation pipeline

Proposed deterministic pipeline, subject to pairing approval:

1. Begin coherent read/write transaction and lock employee; establish business date, input cutoff and existing revision.
2. Resolve effective employment, assignment, calendar and override before loading interval facts; fail readiness without guessing.
3. Load immutable events plus approved correction effects through cutoff. Order by occurred UTC instant, stable source identity and event ID; ingestion order must not determine chronology.
4. Validate approved leave snapshots and observed lifecycle states; detect schedule conflicts and coverage overlap.
5. Pair authoritative IN then OUT into positive half-open intervals. Preserve rejected/ambiguous evidence IDs and findings.
6. Union valid presence intervals; intersect with schedule. Union approved charged leave intervals and intersect with the consistent schedule.
7. Compute uncovered set `S \ (P union L)`. Preserve overlap `P intersection L`; do not sum the two as if mutually exclusive.
8. Derive objective start/end variance against schedule portions remaining after approved leave, plus internal unexplained gaps.
9. Produce readiness/clock findings and a versioned snapshot; automatic calculation is not finalization or confirmed absence.

| Event case | Recommended treatment |
|---|---|
| IN/OUT, IN/OUT across lunch | Two presence intervals; lunch excluded by schedule intersection |
| Exact external replay | Return original receipt/event, no new event |
| Distinct IN, IN, OUT | Ambiguous segment; preserve both INs; no silent nearest/first pairing |
| IN with no OUT / OUT with no IN | Incomplete; do not invent schedule boundary as missing clock |
| Delivery OUT before IN | Re-sort by source instant and recalculate open candidate; preserve received chronology |
| Equal timestamps / contradictory simultaneous directions | Deterministic diagnostics; ordering alone does not resolve meaning |
| Unknown direction | Requires review or later approved provider direction mapping |
| Cross-midnight pair | Unsupported/review initially; never pretend same-day TimeOnly order resolves it |

Unambiguous valid segments can be shown as partial observed evidence, but incomplete/ambiguous days cannot be silently finalized as complete. Preserve all raw rows even when excluded from authoritative reconciliation.

## 11. Late, undertime and absence analysis

**Objective variance:** first required scheduled start 08:00 and unambiguous first presence 08:07 gives seven minutes of start variance. Approved leave covering the beginning shifts the attendance-required boundary. ExceptionalWorkingDay uses its replacement intervals. Missing clock-in means unknown arrival, not seven minutes or a full-day lateness.

Recommend calculate start/end variance per remaining required interval so lunch and hourly leave are respected. Internal uncovered gaps are a separate quantity; do not sum Late + EarlyDeparture + UnexplainedMissing as three independent losses. They describe overlapping subsets of uncovered time. Whether afternoon restart lateness counts as another late occurrence is policy, not interval geometry.

**Undertime:** leave-covered scheduled gaps create no unexplained undertime. Uncovered internal gaps and uncovered tail intervals are factual findings. Full-day paid or unpaid approved leave creates no absence. Zero schedule means no required time even if no events.

**Absence:** missing required coverage is a candidate unexplained interval. Before end of schedule, call it outstanding/not yet observed, not a day's completed absence. Device outages, incomplete clocks, pending correction and off-site duty must remain review findings. HR confirms facts after the day has ended and after all required evidence checks.

Grace periods, rounding, “late count,” partial/full absence labels, cutoff lateness of imported events and permitted off-site proof remain unresolved. No default grace/late threshold, automatic disciplinary label or automatic deduction.

## 12. Paid/unpaid boundary

Use frozen Leave IsPaid, not current LeaveTypes.IsPaid. BalanceTracked is independent from paid classification; both tracked and nontracked approved requests can excuse schedule coverage. Multiple requests can have different classifications on one date; preserve per-request intervals and split covered totals without inventing fractional Days.

D8C normal tracked requests still reject insufficient available balance with 409. D8D did not introduce automatic exhaustion splitting. Attendance cannot split paid leave, generate unpaid leave, deduct balances or change leave classification. Actual scheduled unpaid-leave monetary treatment needs a later separate payroll contract.

## 13. Sandwich boundary

SQL D8D check CK_LeaveSandwichDate_Debit explicitly requires ScheduledMinutes=0. Frozen sandwich gap dates are non-working; potential, applied or unabsorbed entitlement debit does not create expected work, absence, undertime, unpaid work or deductions.

Daily attendance schedule/coverage should not import sandwich debit as leave minutes. A separate informational link to a case can explain an entitlement consequence; it cannot inflate covered scheduled duration. ReasonAccepted, capped ReasonNotAccepted, Charged or Released all preserve this separation. Changing a live calendar so a frozen non-working gap becomes working is a historical schedule conflict requiring review, not permission to convert a sandwich consequence into absence.

## 14. Correction model

Recommend explicit request -> review -> atomic apply. Request records employee/day, required reason, proposed typed event operation(s), affected original event IDs, requester placeholder and server UTC. Operations: add missing manual clock, replace/supersede a clock, or void observation for calculation; never edit/delete original events.

Review requires ExpectedStatus=Pending plus ExpectedRevision/input version for the affected day; accept/reject decision with reason and nullable future reviewer identity. Approved correction and appended authoritative event/effect records commit together under employee/day lock; no partly applied multi-clock correction. Review cannot trust a caller's actor ID or allow forged Device provenance. Reviewed proposals/outcomes are immutable; a subsequent change is a new request.

A finalized day's accepted correction requires explicit reopen/revision, retaining its original finalized snapshot. Requests may be submitted without immediately changing authoritative facts. A pending correction is visible in readiness; finalization must resolve or explicitly reject it. Off-site duty is not a fabricated IN/OUT event; defer until a distinct approved duty-evidence contract exists.

There is no actual authenticated user model today. Follow D8D's nullable Guid actor placeholder without an Employee FK, populate only from trusted identity later, and leave it null in Development. Name ManualAuthorized represents the intended workflow boundary, not a claim that current anonymous API calls are authorized. Recommend Development-only writes until auth/RBAC exists.

## 15. Finalization and locking model

Recommend **Open -> Finalized**, with explicit **reopen** action producing a new Open candidate while retaining the former final revision. RequiresReview is a finding/readiness condition, not a lifecycle transition that loses the last complete final result.

Finalization requires expected lifecycle and revision, day ended in business timezone, valid employment/calendar/leave evidence, no unresolved clock/correction/configuration findings and explicit review of unexplained time. Record UTC, reason where a finding is resolved, nullable actual-user identity and selected input cutoff. Do not auto-finalize a missing-clock day.

Events and leave cancellation can occur after finalization. Keep the original final result stable; expose NewInputSinceFinalization / SourceChanged and propose a new revision. Late evidence cannot silently replace the current final snapshot. During reopen, show the previous final revision and the open candidate separately; downstream consumers cannot mistake a stale final for a fresh one.

Future payroll must pin an accepted attendance revision. Reopening attendance cannot mutate Approved/Paid payroll; subsequent payroll adjustment needs its own approved workflow. The authoritative finality/cutoff and who may reopen remain approval decisions before D9D.

## 16. Device/facial-recognition integration boundary

`External provider -> adapter validation/mapping -> normalized import command -> immutable AttendanceEvent`. The Application contract should not contain camera SDK types. Keep integration credentials and provider payload decoding in Infrastructure adapters.

- Explicit external employee-ID mapping; never fuzzy-match names or assume external ID equals EmployeeNumber.
- Stable source namespace and external event identity, conflict detection and idempotent replay.
- Record source instant/offset and ReceivedAtUtc; delayed/out-of-order arrivals sorted by occurrence.
- Clock drift flagged for review; no silent timestamp correction or default permitted-drift tolerance.
- Unknown employee/malformed timestamp/direction goes to an auditable rejected/quarantined receipt, not a fake Employee or partial AttendanceEvent.
- Mapping revisions must be traceable so later mapping changes do not reassign accepted historical events.
- Batch receipt outcome tracks accepted, replayed and rejected item references; one malformed item must have a stated batch atomicity contract.

Provider registration, effective employee mappings and import receipts are **later adapter entities**, not necessary for an initial manual event foundation. Their shape depends on actual provider capabilities. No vendor integration, biometric images/templates, credentials or raw biometric payload storage in D9.

## 17. Daily operational dashboard reads

Propose a bounded paged roster for an explicit business date/asOfUtc with effective department/location context, readiness, expected intervals, approved leave intervals, last observed clock state, unresolved findings and final/candidate revision identifiers.

Views can distinguish expected today, not scheduled, approved leave now, observed checked-in without subsequent OUT, no IN observed yet, objective late variance, early departure candidate and incomplete clocks. **“Present now” is last observed open presence**, with evidence timestamp/freshness; it cannot guarantee physical location. Do not invent a missing OUT on an open current-day interval.

No-schedule and NotEmployed rows are different from “not checked in.” Do not use Employee.IsActive alone to rewrite historical roster. Live dashboard names may be current, explicitly marked; finalized history uses frozen identity. Query bounded intervals/events for the roster in batches, not a query per employee/date; DTO projections only. No disciplinary ranking or UI in this design checkpoint.

## 18. Monthly and history reads

Return dates/revisions with exact schedule, presence, approved paid/unpaid leave and uncovered durations, finality and unresolved-day counts. Aggregate latest applicable finalized revisions separately from provisional candidates; never mix them without explicit labels.

Objective totals: count dates with positive schedule, count dates with observed scheduled presence, duration sums and quality-finding counts. Present-day count is not an entitlement/payable-day formula. “Late count,” absence-day fractions, tolerance thresholds and KPI gates require approved definitions. Do not divide by eight hours. Keep repeated lateness per interval distinct from count of dates with a positive objective start variance.

Provide revision history/as-of reads for audit. Monthly date bounds follow Asia/Bangkok, not UTC-midnight slicing. Paid/unpaid minutes preserve frozen classification and overlap findings. Exports and large history queries need pagination, access restrictions and explicit retention policy later.

## 19. School Management/substitution boundary

Future School Management should consume employee/TeacherProfile identity, business date/timezone, affected intervals of approved leave or reviewed unavailability, readiness, candidate/final state, revision and observed timestamp. It then intersects those intervals with its own timetable/classes/duties.

No raw camera event dependency, invented substitute allocation or attendance absence-to-class mapping. Provisional late/missing evidence is distinguishable from confirmed unavailability. Leave approval supplies planned future unavailability before attendance finalization; attendance supplies reviewed operational facts. Changes need stable revision IDs and later polling/event integration, not premature timetable or messaging infrastructure.

## 20. Payroll boundary

Actual source inspection: BasicSalaryEntitlementService explicitly implements ThirtyDay Monthly compensation independent of attendance/leave. PayrollGenerationService resolves period-effective employment, compensation entitlement, assignments/rules, SSO/PIT and historical line/payslip evidence. PayrollCalculationService and PayrollPreviewService do not read Attendance/WorkCalendar/EmployeeLeave. Seeded “Late Deduction” and “Absence Deduction” component names do not implement attendance deductions.

Operational ObservedPresenceDuration is observed elapsed time, not proof of productive work or salary entitlement. No WorkedMinutes -> BasicSalary, overtime, unpaid leave or absence deduction mapping is approved. Preserve D1–D7 formulas, statutory contracts, generation atomicity, line provenance and payroll/period lifecycles unchanged.

Later monetary integration must consume explicitly pinned **final attendance revision facts**, with approved unpaid-leave treatment, salary/pay-type rules, rates/rounding, correction handling and no-double-deduction rules. It must not query live devices or recompute past Attendance from mutable calendars. No automatic relationship to PayrollPeriod is required in the initial Attendance model.

## 21. Security/privacy boundary

Program.cs has no authentication/authorization registration or middleware; source search found no Authorize, ClaimsPrincipal/user-ID retrieval, ApplicationUser or IdentityUser convention. Current employee ownership checks enforce resource parentage, not caller authorization. D8D evidence/sandwich routes are Development-only, but existing Attendance CRUD is not.

Future hardening: employee self-view/request correction vs HR review/finalize/reopen vs integration ingestion; no caller-supplied reviewer identity; least-privilege source credentials; rate/batch limits; scoped exports and audit access; retention/deletion/legal hold policy; transport and secret management. Separate device identity from employee identity. Do not store face images/templates in attendance snapshots or logs. No auth/RBAC implementation or deployment-security claim in D9A.

For initial verification recommend Development-only new attendance mutation routes with explicitly null actor placeholders, following D8D. Production authoritative finalization must wait for real identity/authorization or a separately approved trusted operational boundary.

## 22. Historical reproducibility

Freeze compact **resolved input evidence**, not full mutable entity graphs:

- Employee ID/number/display name and effective employment/context IDs plus relevant displayed labels.
- Business date/timezone, observed input cutoff, calculator contract version.
- Assignment/calendar identifiers and display code/name; override kind/ID and exact sorted working intervals, including original interval IDs.
- Event IDs with the actual instant/direction/source used and applied correction IDs/effects; ordering/quality findings.
- Approved leave IDs, observed status/review/cancel timestamps, frozen snapshot version, clipped charged intervals, IsPaid/BalanceTracked and schedule evidence necessary to prove consistency.
- Reconciled presence/overlap/uncovered intervals, objective durations, findings and explicit reviewed resolutions.
- Finalization/reopen decision, UTC timestamp, reason and nullable actual-user identity.

Use typed versioned snapshot serialization as existing Leave/payslip contracts do; this is immutable evidence, not generic AttendanceSettings JSON. Reject unreadable/unknown-version evidence from finalization rather than reinterpret it. Historical configuration identifiers are provenance and should not force old display/calculation to read live entities. Stable employee/day/revision ownership FKs use NoAction; source identifiers in snapshots need not become mutable configuration FKs.

Changing name, organization display, calendar, assignment, override, leave type/policy or cancellation never rewrites an existing final snapshot. New revision explicitly records the new input set and reason. Calendar evidence comparison must include interval contents, not only IDs, because calendar rows are not published versions.

## 23. Concurrency and locking

Existing employment/Leave writers use **Serializable + Employees UPDLOCK first**. Calendar writers lock calendar parent; Leave creation then locks sorted calendars and LeaveType. Recommend Attendance mutations acquire employee first, relevant calendars sorted, then attendance day and correction/revision rows in deterministic order. Do not acquire employee after holding a calendar/day lock.

Payroll generation has a different established outer lock: **PayrollPeriod -> Employee**. Attendance-only operations must never acquire PayrollPeriod after Employee. Any future combined payroll operation must preserve period-first ordering; no lock-order change is proposed now.

| Race | Proposed guarantee |
|---|---|
| Concurrent external replay | Source identity uniqueness plus payload comparison returns one accepted event |
| Different simultaneous events | Employee lock serializes a coherent input set; both retained |
| Correction vs calculation | Shared employee lock + ExpectedRevision/input token; calculation sees before or after, never partial effects |
| Leave approve/cancel vs calculation/finalize | Existing shared employee lock makes observed status consistent at commit |
| Calendar write vs calculation | Calendar parent lock/range reads protect resolved schedule; final snapshot remains immutable afterward |
| Finalization vs new clock/correction | Final input token checked under lock; later input retained and flagged, no hidden replacement |
| Two reviewers | ExpectedStatus plus ExpectedRevision/input version; second stale action 409 |
| Reopen/finalize race | Explicit expected final revision and lifecycle; retain history |

ExpectedStatus alone is insufficient when two different Open revisions exist. Require a revision/input token that changes when effective events/correction facts change. Cross-domain leave/calendar changes must be rechecked under locks against the submitted input fingerprint; merely incrementing an AttendanceDay revision does not notice those changes. No new writes to Leave are necessary to accomplish comparison.

Do not let ingestion silently create a result revision. A short ingestion transaction may append evidence; deterministic recalculation is a separate command. Read dashboards may be provisional as-of views; finalization needs locked coherent reads. Map uniqueness/deadlock/stale input to explicit retry/conflict results; no silently successful partial writes. Keep bulk ingest/operations per employee and sorted/bounded rather than locking an entire school.

## 24. Proposed data model

All names are proposed. GUID keys, UTC audit datetime2, business date and explicit timezone follow SIAMIS. This design uses **five core entities**; approval can reduce initial D9B to events while D9C/D9D add result/correction entities. No generic resource framework or settings blob.

| Entity | Purpose/key fields | Constraints/indexes | Relationships/delete | Mutability/history/concurrency |
|---|---|---|---|---|
| AttendanceEvent | Immutable normalized observation; ID, EmployeeId, UTC instant, business date/zone, direction/source, source namespace/external or manual request ID, original timestamp metadata, ReceivedAtUtc, reason for manual entry, optional correction/original event reference | PK; filtered unique source namespace + external ID; unique manual request key; employee/date/instant/ID lookup; controlled direction/source and source-required field checks | Employee FK NoAction; same-employee original-event/correction ownership validated, preferably composite FK where practical | Never PUT/DELETE; approved correction appends replacement/effect; employee lock and idempotency guard; external raw row never falsely changed to ManualAuthorized |
| AttendanceDay | Stable employee/date identity and current candidate/final pointers, lifecycle/input sequence | Unique employee/date; date/lifecycle queue index; validated current pointer ownership | Employee NoAction; optional composite day/revision pointers NoAction if implemented without cascade/cycle complications | Only lifecycle/pointers change under employee/day lock and expected token; former final remains addressable |
| AttendanceDayRevision | Immutable calculation evidence; ID, DayId, revision number, captured UTC/cutoff, calculator/snapshot version, typed snapshot JSON, objective quantities/readiness/findings | Unique DayId/revision; alternate key DayId/ID for owned pointers; valid JSON/version, nonnegative duration checks; useful day/readiness lookup | Day FK NoAction; configuration IDs in snapshot are provenance, not mandatory live joins | Append only, including rejected/readiness calculations where useful; finality recorded by day event; no overwrite/recalc in place |
| AttendanceCorrection | Proposed typed clock operations, employee/day, reason, Pending/Approved/Rejected, expected revision, requested/reviewed UTC, nullable requester/reviewer IDs and review reason | Employee/day/status/requested index; required reason/status-transition integrity; optional request-key uniqueness; operations format explicitly versioned | Day and Employee ownership NoAction; original IDs validated for same parent; future user identifiers not Employee FKs | Pending request reviewed once; approved effects atomically append events and history; reviewed payload immutable; follow-up correction is new request |
| AttendanceDayEvent | Append-only finalize/reopen/correction-applied decisions; ID, DayId, relevant RevisionId/CorrectionId, action, previous/new state, reason, UTC and nullable ActorId | Day/time/ID index; controlled action/state; required reason for reopening/exception resolution | Day and owned revision/correction NoAction; no actor Employee FK | Immutable decision trail; employee/day lock; explicit expected lifecycle and revision |

Optional current revision pointers must not create unavoidable insertion cycles: create day, insert revision, then assign owned pointer in one transaction; deleting history is not supported. A simpler verified latest-revision query may replace candidate pointer if that lowers implementation cost. FinalizedRevisionId must resolve to that same day's revision.

Value structures: resolved schedule interval, normalized observation, paired presence interval, approved-leave coverage with classification, uncovered interval, typed finding and reviewed resolution. Pure calculator accepts these structures, independent of EF/controllers/device SDK.

Future adapter-only additions: provider registration/source namespace, effective EmployeeExternalAttendanceIdentity mapping with unique source/external employee ID + nonoverlap validation under mapping parent locks, and append-only import receipts with unique source/external event ID and rejection/replay outcomes. Preserve mapping/version used on acceptance; FKs NoAction where applicable. They require actual provider/mapping approval before design is finalized.

Typed immutable published AttendancePolicy revisions are needed only when grace, classification/rounding or finalization policy is approved. Do not create empty speculative policy tables in D9B or repurpose PayrollSettings/LeavePolicy for attendance.

## 25. Proposed API surface

Routes below follow existing nested employee APIs and separate operational queues. They are recommendations, not implemented routes; existing Attendance CRUD remains unchanged in D9A.

| Method / proposed route | Meaning |
|---|---|
| GET /api/employees/{employeeId}/attendance-days/{date} | Read candidate/final detail, readiness, intervals, evidence references and findings |
| GET /api/employees/{employeeId}/attendance-days?fromDate=&toDate=&page=&pageSize= | Bounded history; explicit provisional/final distinction |
| GET /api/employees/{employeeId}/attendance-summary?year=&month= | Factual monthly totals and unresolved coverage |
| GET /api/attendance-days?date=&asOfUtc=&departmentId=&page=&pageSize= | Daily operational roster |
| GET /api/attendance-days/review?fromDate=&toDate=&page=&pageSize= | Exceptions and stale-final queue |
| GET /api/employees/{employeeId}/attendance-events?fromDate=&toDate= | Evidence read, bounded/paged |
| POST /api/employees/{employeeId}/attendance-events/manual | Server-controlled ManualAuthorized source, explicit instant/direction/reason/idempotency key |
| POST /api/employees/{employeeId}/attendance-days/{date}/calculate | Produce open candidate revision; not finalization |
| POST /api/employees/{employeeId}/attendance-days/{date}/corrections | Submit typed correction with reason |
| POST /api/employees/{employeeId}/attendance-corrections/{id}/approve or /reject | Explicit review/atomic effects with expected status/revision |
| POST /api/employees/{employeeId}/attendance-days/{date}/finalize or /reopen | Explicit protected lifecycle commands |
| POST /api/attendance-imports/{sourceKey}/events | Later authenticated vendor-neutral ingestion; no direct vendor route in core |

Do not expose generated metrics, statuses or actor/source provenance as client-selectable authoritative fields. DTOs/interfaces in Application, controllers/routing in API, EF projection/transactions in Infrastructure, pure interval calculation apart from I/O. No controller DbContext.

Use existing 200 read/update-result, 201 created evidence/request, 400 contract validation, 404 missing/wrong-parent and 409 stale/conflict conventions. A readiness detail can be 200 with explicit inability to calculate; an attempted finalize requiring absent configuration is 409. No DELETE raw/finalized history. Document all parameters, timezone, provisional/final distinctions and DTO response/error types with XML Swagger comments. Reject unknown mutation fields, as newer Leave DTOs do. Pagination and date-range limits must be explicit.

## 26. Existing-data migration strategy

No migration created/applied here. Local zero Attendance rows means no local conversion is required. Future additive D9B migration can introduce events without touching D1–D8 tables or master status seeds; verify NoAction, unique replay keys, precision/checks and row counts before approval/application.

Recommend a staged transition: add authoritative tables and focused APIs; explicitly retire old Attendance POST/PUT/DELETE before new authority is enabled; retain legacy read-only history until separate retirement approval. Existing old routes must not keep mutating parallel authoritative facts. Do not silently map old “Present/Late/Absent” statuses into computed truth or double-read legacy and new results.

For any nonempty deployment, retain legacy rows as labeled manual summaries. Their clock pair/status does not prove device events, timezone, split schedule, paid leave or authorization. Import only through an approved audited conversion producing LegacyUnverified readiness; never fabricate source/reviewer/finalization. If all environments are proven empty and compatibility retirement approved, a later focused migration can drop obsolete table/entity/routes; do not rewrite an already applied migration. Retain AttendanceStatuses until dependency retirement is separately reviewed.

New readiness errors are intentional compatibility changes to callers that currently supply a manual status; obtain approval and document route retirement/response. No existing local data is automatically repaired.

## 27. Test strategy (designed, not executed)

Use pure calculator tests, focused local API/SQL tests, race tests, then bounded existing Leave/payroll regressions in their implementation checkpoints. Every live fixture captures exact baseline including timestamps/JSON and restores it; temporary employees may be used only with later explicit test authorization. No data created here.

| Case | Required assertions |
|---|---|
| Normal day | Exact schedule intersections, provenance/version, zero unexplained variance |
| Multiple intervals/lunch | Gaps excluded, multiple clock pairs, no invented eight-hour total |
| Arrival late / restart late | Objective variance exact; count/classification only after policy approval |
| Early departure / internal gap | Correct uncovered intervals; metrics not double summed |
| Timed approved leave | Start/tail/middle coverage subtracts only charged intervals |
| Full-day leave | Full schedule covered, paid/unpaid snapshot preserved, no false absence |
| Disjoint multiple requests | Union without double counting, per-request classification retained |
| Presence overlapping leave | Review finding plus separate overlap quantity |
| Pending/rejected/cancelled leave | No current excuse; cancellation after final leaves original unchanged |
| Insufficient normal leave | Existing D8C 409 unchanged; no automatic unpaid split |
| Holiday/rest/weekly non-working | Schedule zero, no absence, any observed presence outside schedule separate |
| Exceptional workday | Override replaces weekly intervals completely |
| Employment gap / ambiguous employment | NotEmployed/conflict, not guessed absence |
| Missing/overlapping calendar assignment | Readiness conflict, no IsDefault fallback |
| Historical inactive employee/calendar | Date-effective facts preserved; operational intake policy explicit |
| Missing IN/OUT / unknown direction | Incomplete/review, no fabricated clock or day duration |
| Distinct duplicate directions/equal time | Preserve evidence, deterministic ambiguity finding |
| External duplicate delivery | Single row/idempotent response; changed replay payload 409 |
| Out-of-order delivery | Occurrence-based reconciliation, received times retained |
| UTC day boundary / source offset | Correct Bangkok business date, no machine timezone reliance |
| Seconds/fractions | Original precision preserved, approved duration contract enforced |
| Overnight | Explicit unsupported result until approved; no same-day wrap guessing |
| Manual correction | Reason required, server source, originals retained, atomic effects, wrong-parent 404 |
| Stale reviewer/input | ExpectedStatus/revision/input fingerprint 409 |
| Sandwich date | ScheduledMinutes0 across every case state, no absence/unpaid/deduction |
| Calendar/assignment/name/policy changes | Final snapshot identical; mismatch readiness; explicit new revision |
| Late arrival after final | New evidence retained and surfaced; no final overwrite |
| Concurrent ingestion | One replay row, all different events retained, deterministic latest candidate |
| Concurrent correction/calculation/finalization | Before/after coherent sets, no partial effects, stale losers conflict |
| Concurrent Leave transition/calendar write | Coherent evidence under shared locks; no mixed schedule/state |
| Dashboard/current day | AsOf explicitly bounded; no premature full-day absence or guaranteed physical presence |
| Monthly/KPI boundary | Provisional/final separate; no default fractions, penalties or eight-hour divisor |
| Payroll regression | Salary/proration, earnings/deductions/rules, SSO/PIT, rollback, snapshots, manual adjustment and lifecycles unchanged |
| Leave regression | D8B assignment/override; D8C minutes/balances/concurrency; D8D evidence/review/capped sandwich unchanged |
| Cleanup | Exact original single inactive TEST-EMP-001 and all original tables/rows/timestamps restored; migration history changes only explicitly approved focused migration |

Existing useful regression harnesses: `verify_d8b_live.py`, `verify_d8b_regressions.py`, `verify_d8c_live.py`, `verify_d8c_regressions.py`, `verify_d8c_transition_race.py`, `verify_d8d_live.py`, `verify_d8d_capped_focus.py`, `verify_d8d_regressions.py`, and SIAMIS.Payroll.RegressionTests. Do not claim these passed in D9A; none were run.

## 28. Recommended D9 sub-checkpoints

| Checkpoint | Narrow scope | Approval gate |
|---|---|---|
| D9B — evidence and expected-work foundation | Combined effective employment/calendar readiness resolver, timezone contract, immutable manual event evidence/idempotency, focused reads and schema checks | Approve source/direction/precision, active-vs-employment intake and legacy compatibility contracts; review focused migration before application |
| D9C — daily reconciliation and Leave coverage | Pure interval calculator, candidate revisions, approved snapshot reads, conflict findings and factual history | Approve conservative pairing, seconds presentation and schedule/leave mismatch behavior; no discipline/money |
| D9D — correction and finalization | Audited requests/review/apply/reopen, stale-input protection, preserved finals | Approve reviewer authority, day completion/cutoff/finality, late input and correction rules; production auth boundary |
| D9E — operational reads and provider adapter foundation | Daily roster/month facts, freshness, effective external mapping/import receipts/idempotency | Actual provider timestamp/direction/ID capabilities; mapping, drift, batch, privacy and ingestion security contracts |
| Later separately approved integration | Payroll unpaid-leave/absence/overtime; School Management substitution; KPI/discipline | Independent monetary, timetable and performance contracts; never implicit |

No large attendance policy engine, default organization calendar history or generic settings module. API-level manual event tests can start before device selection. D9B should not include absence finalization, payroll effects or vendor implementation.

## 29. Explicit unresolved decisions

The identifiers below map one-to-one to recommendations in section 30. Decisions already approved in D8B–D8D (explicit assignment, override replacement, frozen minute-based Leave, capped non-working sandwich consequences) are not reopened.

| ID | Decision requiring approval | Blocking stage |
|---|---|---|
| A1 | New event/result model and retirement of legacy write routes | D9B compatibility/model |
| A2 | Timestamp/duration precision vs whole-minute calendars | D9B storage, D9C output |
| A3 | Normalized Direction/source taxonomy, manual idempotency and unknown direction | D9B |
| A4 | Employee.IsActive vs effective employment for event intake/roster, including current inactive/open baseline | D9B intake/roster |
| A5 | Conservative ambiguous pairing; duplicate direction/ties/missing pair | D9C |
| A6 | Approved frozen leave schedule disagrees with later calendar/assignment | D9C; material repository ambiguity |
| A7 | Grace, minute rounding, late occurrence definition/restarts | Any HR classification beyond objective variance |
| A8 | Undertime/internal gaps/off-site duty and presence during leave | D9C findings; later authoritative exceptions |
| A9 | Absence confirmation and operational “not yet checked in” timing | D9D/daily operational labels |
| A10 | Correction requester/reviewer authority and auditable manual source without current auth | D9B mutation exposure/D9D |
| A11 | Finalization cutoff, reopening authority, late device/Leave changes and downstream stale finals | D9D |
| A12 | Overnight schedule/day allocation | Before overnight acceptance; not required for same-day D9B |
| A13 | Actual provider mapping, reliable direction/IDs/timezone, permitted drift, batch/replay/retention | D9E external ingestion |
| A14 | Automatic paid-to-unpaid exhaustion/splitting | Future Leave contract, remains deferred |
| A15 | Payroll time/unpaid-leave/overtime/absence monetary integration | Future payroll checkpoint |
| A16 | Monthly day/late/absence count definitions, KPI/discipline | Later policy/report interpretation |
| A17 | Production privacy/access/retention/export and real user identities | Production deployment/integration |

D9A stops after this report. **D9B is technically feasible but not unconditionally approved to begin**: A1–A4 and the Development-only authority boundary in A10 require acceptance. Later decisions can remain deferred with explicit unsupported/readiness behavior. No legal statutory policy is selected by these recommendations.

## 30. Smallest recommended decision for each unresolved item

| ID | Smallest recommendation |
|---|---|
| A1 | Add new AttendanceEvent foundation; preserve old GET as legacy, retire old POST/PUT/DELETE before new authority begins. Leave table removal to separate compatibility approval. Use versioned daily revisions for later calculation. |
| A2 | Preserve raw timestamp precision in UTC datetime2(7). Calculate exact durations without rounding; retain precise units and expose decimal minutes via duration/60, not truncation. Approve a specific persisted duration unit/precision before D9B migration; do not inherit whole-minute Leave rounding for clocks. |
| A3 | In/Out/Unknown; ManualAuthorized/Device/Imported only initially. Server sets manual source. Required manual request ID; external source namespace+event ID uniqueness. Unknown produces review, never inferred alternation. |
| A4 | Employment intervals determine expected work historically. Retain evidence for an existing employee even if currently inactive/outside employment, flag intake anomaly rather than infer absence. Show inactive flag separately. Baseline open/inactive mismatch remains untouched; approve if operational intake should instead reject. |
| A5 | Pair only unambiguous alternating positive IN/OUT segments; duplicate directions/ties/unmatched events remain review findings. No “closest event” tolerance or fabricated boundary. |
| A6 | Compare frozen approved-leave schedule/assignment evidence to candidate schedule; conflict blocks finalization. Preserve prior finals. HR resolves with explicit correction/revision; no automatic preference or leave recalculation. |
| A7 | Report objective start variance per required interval; no grace, rounding or policy late-count until a typed versioned policy is separately approved. |
| A8 | Report uncovered and overlap intervals as findings, distinguishing start/tail/internal gaps. No off-site paid-duty inference or automatic leave cancellation; add authorized duty-evidence contract later if needed. |
| A9 | Missing clocks -> incomplete/review. Only reviewed complete ended-day facts can confirm absence. Live view says no IN observed as-of, never final absence. |
| A10 | New mutation/review routes Development-only initially, actor null, required reasons and stale tokens. Never accept claimed user ID/source from body. Production awaits real auth/RBAC; reviewer identity is not EmployeeId. |
| A11 | Open/Finalized with explicit reopen and immutable prior finals; ended-day requirement; expected revision plus full input fingerprint. Late clocks/Leave cancellation flag source change and require new reviewed revision. No automatic reopen/final overwrite; no assumed provider delivery cutoff. |
| A12 | Remain unsupported for scheduled overnight work; preserve cross-date raw observations but flag ambiguous day allocation. Approve overnight contracts separately before pairing/calculation. |
| A13 | Require provider sample contract and stable IDs before adapter implementation. Quarantine unknown mapping/timezone/direction; don't alter clock drift. Keep batch policy explicit and storage bounded. |
| A14 | Keep D8C insufficient tracked leave 409 and D8D cap behavior; no exhaustion conversion in Attendance. |
| A15 | No payroll change. Later approve pinned final revisions, explicit formulas/no-double-deduction and correction handling before any money moves. |
| A16 | Publish clearly named factual date/duration counts; no “attendance gate,” fractions or disciplinary thresholds. Policy counts need distinct approved definitions. |
| A17 | No biometric data; production access/retention/auth review separately. Use nullable future actual-user IDs without fake FK, trusted server attribution later. |

## 31. Repository evidence and file references

Paths below are relative to `C:\Users\USER\Documents\ChatGPT\SIAMIS School Management System`. Line numbers refer to the inspected clean working tree. These are implementation evidence, not external legal sources.

| File / verified lines | Evidence |
|---|---|
| src/SIAMIS.Domain/Entities/Employees/EmployeeAttendance.cs:5–17 | Entire manual Attendance entity; no audit or event fields |
| src/SIAMIS.Application/Employees/EmployeeAttendanceContracts.cs:5–33 | DTO/request/interface and validation attributes |
| src/SIAMIS.Infrastructure/Services/EmployeeAttendanceService.cs:10–21,33–101,105–123 | Date filters/order; create/update/delete; active status and clock validation; projection/ownership/unique SQL error mapping |
| src/SIAMIS.Api/Controllers/EmployeeAttendanceController.cs:6–77 | Existing route, XML comments, five actions and documented HTTP/error mapping |
| src/SIAMIS.Infrastructure/Configurations/EmployeeConfigurations.cs:216–232 | Attendance date/time/remarks/index/NoAction mapping |
| src/SIAMIS.Infrastructure/Migrations/20260929050408_AddEmployeeAttendance.cs:12–54 | Original table, FKs and unique day index |
| src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs:202–235 | Current Attendance model corroboration |
| src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs:34,70,76–104,128–145 | Status/Attendance DbSets; configuration discovery; UTC IHasTimestamps handling |
| src/SIAMIS.Infrastructure/Data/MasterDataSeeds.cs:43,47 | 11 mixed Attendance statuses; named deduction components without attendance formulas |
| src/SIAMIS.Domain/Entities/Employees/Employee.cs:7–23,37 | Employee identity/IsActive/timestamps and legacy attendance collection |
| src/SIAMIS.Domain/Entities/Employees/EmploymentRecord.cs:5–28 | Effective employment/context representation |
| src/SIAMIS.Infrastructure/Configurations/EmployeeConfigurations.cs:87–111 | Employment interval checks, unique current record, NoAction context FKs |
| src/SIAMIS.Infrastructure/Services/EmploymentIntegrity.cs:10–27 | Shared employee UPDLOCK; inclusive effective intervals and StartDate fallback |
| src/SIAMIS.Infrastructure/Services/EmploymentLifecycleService.cs:24–25,54–68,92–107 | Serializable lifecycle writer, terminal close/change records and historical resolution |
| src/SIAMIS.Infrastructure/Services/EmployeeService.cs:100,114,125,185–188,280 | Employee lock/inconsistent state handling; existing UTC-date validation |
| src/SIAMIS.Domain/Entities/Leave/LeaveFoundation.cs:5–52,54–89 | UTC base, calendars/split intervals/overrides/explicit effective assignments; policy/entitlement |
| src/SIAMIS.Infrastructure/Configurations/LeaveFoundationConfiguration.cs:12–49 | datetime2, interval/category checks, calendar/default and assignment indexes, NoAction |
| src/SIAMIS.Infrastructure/Services/LeaveFoundationService.cs:28–42,54–62,73–86,95–125 | Mutable calendar metadata; whole-minute/no-overnight inputs; override replacement; employee/calendar locks; no default/historical-active fallback |
| src/SIAMIS.Api/Controllers/LeaveFoundationController.cs:14–126 | Focused calendar/interval/override/assignment/resolution route conventions |
| src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs:41–79,99–117 | Published immutability, effective overlap rejection, entitlement employee locking |
| src/SIAMIS.Application/Employees/EmployeeLeaveContracts.cs:15–59,66–100 | Strict mutation contracts, cancellation expected status, typed schedule/charged intervals/paid snapshot and DTO |
| src/SIAMIS.Domain/Entities/Employees/EmployeeLeave.cs:5–35 | Frozen calculation, lifecycle timestamps/status and compatibility Days |
| src/SIAMIS.Domain/Entities/Leave/EmployeeLeaveAllocation.cs:3–10 | Immutable calendar-year allocations |
| src/SIAMIS.Infrastructure/Configurations/LeaveRequestConfiguration.cs:10–25,34–37 | UTC lifecycle, owned alternate key, authoritative snapshot/status/lifecycle checks and allocations |
| src/SIAMIS.Infrastructure/Services/LeaveRequestCalculator.cs:17,33–68,90–103 | Explicit Bangkok; employment/assignment/policy resolution; override replacement; minute intersection; frozen IsPaid and overlap |
| src/SIAMIS.Infrastructure/Services/LeaveSnapshotIntegrity.cs:12–54 | Validates frozen structure/header/allocations without live configuration reinterpretation |
| src/SIAMIS.Infrastructure/Services/EmployeeLeaveService.cs:26–46,50–64,86–124 | Shared parent locking, snapshot overlap and insufficient-balance409; approve/reject/cancel coherent frozen evidence |
| src/SIAMIS.Infrastructure/Services/EmployeeLeaveReadService.cs:19,28,108–120 | Integrity reads, UTC materialization and status-derived balance totals |
| src/SIAMIS.Api/Controllers/LeaveOperationsController.cs:12–33 | Separate paged history/queue/balance APIs |
| src/SIAMIS.Domain/Entities/Leave/LeaveEvidenceSandwich.cs:8–36,39–85 | Immutable external receipts/actor events, case snapshot and potential/applied debit |
| src/SIAMIS.Infrastructure/Configurations/LeaveEvidenceSandwichConfiguration.cs:22–43,61–95 | Owned NoAction relationships, zero-scheduled gap check, capped allocation check |
| src/SIAMIS.Infrastructure/Services/EmployeeLeaveEvidenceService.cs:43–44,68–69,86 | Serializable employee lock and Accepted approval evidence |
| src/SIAMIS.Infrastructure/Services/EmployeeLeaveSandwichService.cs:15,29–75,107–124,180–187,208–232 | Frozen gap formation, lifecycle/commitment effects, capped review and expected state |
| src/SIAMIS.Api/Controllers/LeaveEvidenceSandwichController.cs:18,28,40,49,57,66 | Development-only sensitive evidence/review boundary |
| src/SIAMIS.Infrastructure/Migrations/20261003164508_AddCappedLeaveSandwichConsumption.cs | Latest live applied migration; applied debit added without changing scheduled work |
| src/SIAMIS.Domain/Entities/OrganizationProfile.cs:7–19 | Display/contact singleton only; UTC timestamps, no timezone/calendar/auth |
| src/SIAMIS.Infrastructure/Services/BasicSalaryEntitlementService.cs:8–46 | ThirtyDay Monthly salary expressly independent of attendance/leave |
| src/SIAMIS.Infrastructure/Services/PayrollEmploymentContextService.cs:10–48 | Period-effective context, overlap/inconsistent history detection |
| src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs:37–42,77–110 | Employment candidate selection; period-before-employee lock; protected historical payroll and salary contract |
| src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs; PayrollPreviewService.cs | Searches of these full files found no Attendance/WorkCalendar/EmployeeLeave integration |
| src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs:10–33 | Source snapshot, classification and salary calculation evidence pattern |
| src/SIAMIS.Domain/Entities/Payroll/EmployeePayroll.cs:16–24 | Payroll lifecycle/UTC audit, no authenticated actor identity fields |
| src/SIAMIS.Infrastructure/Services/PayrollOperationsService.cs:18–45,58,95–98 | Parent period read locking, frozen payslip identity/evidence and review readiness |
| src/SIAMIS.Api/Program.cs:11–28,66–86 | Swagger XML, SQL Server/service registrations and pipeline; no auth registration/middleware |
| src/SIAMIS.Api/appsettings.Development.json:2–4 | Localhost/SIAMIS integrated Development connection target |
| tests/verify_d8b_*.py; verify_d8c_*.py; verify_d8d_*.py; tests/SIAMIS.Payroll.RegressionTests | Existing focused verification assets inspected by file inventory; not executed |

Searches covered authentication/identity/current-user APIs; machine-local time; attendance-related references in payroll service/application files; timestamps/actor fields; locks; Attendance configurations and migrations. SQL evidence used sys.tables/partitions, sys.columns/types, sys.indexes/index_columns, sys.foreign_keys, sys.check_constraints/triggers, migration history and SELECT counts/integrity queries. Counts from metadata were corroborated by COUNT_BIG for Attendance/statuses; baseline employee and employment facts were directly selected. No INSERT/UPDATE/DELETE/DDL or migrations were executed.

## 32. Verification/status table

| Check | Result / limits |
|---|---|
| Initial working tree | Clean; git status --short returned no entries |
| Repository inspection | Completed across Attendance, employees/employment, calendar/Leave/evidence/sandwich, payroll, organization, API/audit/locks |
| Local SQL inspection | Completed read-only, localhost/SIAMIS, Windows auth; schema/indexes/FKs/counts/history checked |
| Attendance integrity | 0 rows, 0 orphans, 0 duplicate employee/date groups; 2 enabled/trusted NoAction FKs, no checks/triggers |
| SQL diagnostic error | Initial SELECT alias RowCount caused SQL syntax error; corrected to RecordCount; subsequent queries succeeded. No mutation involved. |
| Database mutations | None; no test fixtures, core employee changes or schema changes |
| Migrations | None created, modified or applied; existing 37 history rows observed |
| Production code | Unchanged |
| Files created | D9A-ANALYSIS.md only |
| Live API/build/regression tests | Not run; analysis-only scope did not require them. No runtime/API behavioral pass is claimed. |
| Git whitespace verification | git diff --check plus report whitespace inspection completed; new untracked report checked explicitly |
| Git commit/push | Neither performed |
| Decisions / readiness | A1–A4 plus A10 Development authority boundary gate D9B; later financial/disciplinary/finality/provider decisions separately gated |

**D9A is complete. No D9B implementation has begun.**
