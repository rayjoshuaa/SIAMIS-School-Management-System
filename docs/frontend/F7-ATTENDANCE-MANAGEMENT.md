# F7 — Attendance Management

Status: **F7.1 ready for product-owner review. Not committed. Not pushed.**

Verified: 2026-10-08. Review URL: <http://localhost:5175/hr/attendance>.

## Scope and preservation

Frontend presentation of the frozen attendance contracts. F7 uses the V2.1 design system and V2.3 authenticated shell. V2.4 dashboard, F5 employee management, F6 lifecycle and V2.2 authentication implementations are preserved. No backend, model, database, migration, role, capability, account or payroll changes. No real attendance records were created or changed during verification. No Git commands were run.

A SHA-256 inventory of 507 existing source/test/frontend-documentation files was captured before implementation. The closing comparison found only the three intended routing/navigation modifications below; all other inventoried existing files were unchanged. New attendance files are additive. Build artifacts are not source changes.

## Frozen contract inventory

Base employee paths below are `/api/employees/{employeeId}`.

| Operation                          | Existing contract                                            | Required capability                                                    |
| ---------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------- |
| Server Bangkok today               | `GET /api/attendance/today`                                  | Reporting.Read                                                         |
| Specified daily overview           | `GET /api/attendance/days/{date}`                            | Reporting.Read                                                         |
| Attention queue                    | `GET /api/attendance/review-queue?from&to`                   | Attendance.Read                                                        |
| Calculated employee history        | `GET /attendance-history?from&to`                            | Reporting.Read, or authenticated linked-employee SelfService ownership |
| Official employee summary          | `GET /attendance-summary?from&to`                            | Reporting.Read, or authenticated linked-employee SelfService ownership |
| Raw evidence list/detail           | `GET /attendance-events`, `GET /attendance-events/{eventId}` | Attendance.Read                                                        |
| Expected work                      | `GET /attendance-expected-work?date`                         | Attendance.Read                                                        |
| Live day calculation               | `GET /attendance-days/{date}`                                | Attendance.Read                                                        |
| Day review/current source metadata | `GET /attendance-days/{date}/review`                         | Attendance.Read                                                        |
| Immutable finalized revisions      | `GET /attendance-days/{date}/history`                        | Attendance.Read                                                        |
| Authorized manual observation      | `POST /attendance-events/manual`                             | Attendance.Manage                                                      |
| Append correction evidence         | `POST /attendance-days/{date}/corrections`                   | Attendance.Manage                                                      |
| Include/exclude evidence decision  | `POST /attendance-days/{date}/adjudications`                 | Attendance.Manage                                                      |
| Confirm potential absence          | `POST /attendance-days/{date}/confirm-absence`               | Attendance.Finalize                                                    |
| Finalize day                       | `POST /attendance-days/{date}/finalize`                      | Attendance.Finalize                                                    |
| Explicit reopening                 | `POST /attendance-days/{date}/reopen`                        | Attendance.Finalize                                                    |
| Legacy attendance list/detail      | `GET /attendance`, `GET /attendance/{attendanceId}`          | Attendance.Read                                                        |

Legacy attendance mutations are retired with HTTP 410; the frontend implements no legacy editor. Expected work and calculated intervals are exposed in the review DTO, so the review page does not issue redundant expected-work/day queries. No bulk period finalization contract exists.

### Query boundaries

- Daily and queue filters are sent to the server: DepartmentId, DesignationId, Scheduled, Late, HasApprovedLeave, ConfirmedAbsent, RequiresReview, IsStale, Unfinalized and RecordState. Counts are matching server totals before pagination, with overlapping categories explicitly explained.
- Daily/queue contracts do not offer EmployeeId filtering. The separate employee history workflow implements bounded employee lookup, including inactive employees, and employee-specific date queries. It does not filter only the current daily page and pretend that is an employee report.
- List/evidence paging uses bounded page size 20. Queue requires an explicit inclusive range, maximum 31 days; existing server candidate/source budgets remain authoritative. Calculated history/summary support up to 366 days. Backend validation/source-budget failures are displayed with retry support.
- Employee lookup requires Employee.Read. Without it, an authorized attendance reader may enter a known Employee GUID; this does not grant employee-directory access.
- Official summaries include only currently validated frozen revisions. No fallback to live facts, day equivalents, salary amounts or assumed eight-hour days.

## Implemented workspace

The placeholder is replaced by Daily overview, Review queue and Employee history & evidence views. Reporting.Read gates the daily report and calculated history/summary. Review queue, evidence, legacy reads and full review use Attendance.Read.

Daily/queue query filters and pagination persist in the URL. The record-inspection drawer retains the filtered list and returns focus to its invoking action. It distinguishes live calculations from currently validated official evidence, shows structured findings/changed sources, and links to the substantial full-page review workflow.

Employee history uses server-backed lookup, explicit employee/range selection, official completeness/configuration/review counts and read-only legacy records. Missing data is displayed honestly. The overview and queue use server-produced counts, never client-derived substitute totals.

## Review, corrections and audit

The full review page separates reviewed calculation, raw calculation/events, paged evidence detail, review audit and historical finalized revisions. It displays work-calendar readiness, exact schedule/presence/leave/unexplained intervals, approved leave facts, raw arrival variance, calculation/grace versions and structured findings.

Consequential actions use the existing accessible AlertDialog as a focused confirmation. Required reasons, explicit-offset occurrence timestamps, directions and owned evidence selections follow the actual contracts. Source timestamps with seven fractional digits remain strings; they are not silently converted through JavaScript Date and truncated. Raw observations are not labelled self-service clocking.

Corrections and adjudications append evidence/decisions. They do not edit or delete original events. Frozen revision snapshots retain their original calculation, evidence and audit actors/reasons; live metadata is not substituted into historical evidence.

Review mutations freeze the server ExpectedVersion and ExpectedSourceFingerprint when the dialog opens. The server retains employee-first locking and source-version protection. HTTP 409 preserves entered context, blocks further submission and instructs the operator to close/reload; there is no force-overwrite path. Manual intake keeps a stable request GUID for explicit retry and uses the existing backend idempotency contract. Mutation retries are not automatic; double submission is blocked while pending. Existing API client/session/CSRF handling is reused.

## Finalization and reopening

The list shows actual server ReadyToFinalize counts. Full review states backend prerequisites rather than recreating a frontend eligibility engine. Finalize and potential-absence confirmation require Attendance.Finalize, explicit reason and confirmation; backend coverage/blocking findings remain authoritative.

A finalized revision is immutable. Correction/adjudication/absence/finalize actions are hidden while it remains frozen. Explicit reopening is available only through the supported reasoned backend command. Historical finalization and present validity are displayed separately, including IsStale, RequiresReopen and structured changed-source findings. Read-time staleness does not mutate anything.

The independent authorized manual-observation endpoint remains available because the backend explicitly permits append-only intake after finalization; the UI warns that new observations can make historical evidence stale without rewriting or automatically reopening it.

## Payroll, leave and precision boundaries

Attendance findings do not authorize absence, establish misconduct, confirm a deduction or change payroll. Potential absence stays provisional until the supported review decision. Approved leave coverage is displayed as authoritative source information, not financial punishment. No early-release permission workflow is invented.

No payroll, leave, entitlement, sandwich, KPI or disciplinary command is issued. CoverageTruncationResidualMilliseconds remains neutral conversion metadata. Durations are independently truncated integer milliseconds supplied by the backend; exact tick intervals remain authoritative. Null coverage is unavailable, not zero. Zero is displayed only where the server actually returns zero. Unsafe JavaScript integer precision is reported rather than approximated.

## Authorization

- Organization-wide attendance and nested review routes retain the existing Attendance.Read protected-route requirement.
- Reporting.Read controls reporting queries; Attendance.Manage controls manual/correction/adjudication actions; Attendance.Finalize controls confirm/finalize/reopen.
- No role names or QA usernames are used to authorize functionality. Employee-only SelfService and Payroll-only personas cannot enter attendance administration.
- Backend authorization and ownership remain authoritative. Existing 401/session-expiry redirects, 403 feedback, cookies and CSRF remain unchanged.
- Employee self-service ownership exceptions apply only to the existing history/summary endpoints, not raw intake or organization administration.

## F7.2 — Employee Clock In / Clock Out (requires separate approval)

| Requested functionality                     | Frozen backend finding                                                                                                                                                  |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Employee clock-in/out                       | No employee-owned clocking command; manual intake is HR-authorized evidence entry                                                                                       |
| Server-generated clock occurrence timestamp | ReceivedAtUtc is server-generated intake metadata, but OccurredAt is supplied by the authorized caller; no server-stamped clocking contract                             |
| On-campus / online-class / remote-work      | No matching attendance classification fields or validated values                                                                                                        |
| Employee-owned history                      | Existing calculated history and summary ownership checks support linked employees; raw evidence/review are not employee self-service                                    |
| Missing clock-out                           | Calculation findings and HR append-only correction/adjudication exist; no employee clock-session, auto-close or correction-request workflow                             |
| HR review/audit                             | Existing D9 review, source concurrency, immutable revisions and authenticated audit actors can support HR review, but do not supply missing employee clocking contracts |

Therefore F7 does not add clock buttons or invented endpoints. A separately approved F7.2 must establish occurrence-time semantics, work classification, employee ownership/commands, correction-request rules and audit/concurrency contracts. No GPS, webcam monitoring or automatic payroll deduction is implemented or implied. The separate proposal is [F7.2-EMPLOYEE-CLOCKING-PROPOSAL.md](F7.2-EMPLOYEE-CLOCKING-PROPOSAL.md).

## Verification

| Gate                                                   | Result                                                                                                                                     |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Complete frontend suite                                | 245 tests passed across 13 files, including 34 focused attendance tests                                                                    |
| Production frontend build                              | Passed                                                                                                                                     |
| ESLint                                                 | Passed, zero warnings                                                                                                                      |
| Prettier formatting check                              | Passed                                                                                                                                     |
| Backend Release solution build                         | Passed, zero warnings/errors; isolated temporary artifacts used to avoid disturbing the running API                                        |
| Existing backend/domain/security regression executable | 1,024 assertions passed, isolated fixtures; no live database connections                                                                   |
| EF pending-model check                                 | No changes detected                                                                                                                        |
| Preservation inventory                                 | Only three existing routing/navigation files changed; inventoried auth, HR dashboard, F5/F6, backend and migration source hashes unchanged |
| Repository checks                                      | No Git commands, per instruction; changed text files checked for trailing whitespace/conflict markers                                      |

Focused tests cover filters/paging/server counts, queue ranges, drawer/focus return, employee history, null and precise duration handling, raw timestamp preservation, owned evidence detail, source concurrency, mandatory reasons, safe correction/adjudication/finalization/reopening, immutable historical snapshots, capability denial, session expiry, HTTP 400/403/409/503 and loading/empty states. Fixtures are mocked HTTP responses isolated from Development records.

### Real Development browser verification

Used the existing authenticated qa-systemadmin session through the normal application. Read-only daily/queue/history/summary/review/revision requests succeeded. Both existing employees were displayed; their calendars are not configured, so the actual page reports ConfigurationRequired/WorkCalendarNotConfigured, unknown timing, no currently validated revision and zero ready-to-finalize days. The dedicated QA employee history/official summary and legacy empty state were checked. No fake data was added to make the workspace appear populated.

Chromium checks covered 1440×900, 1280×800, 1024×768, 768×1024, 430×932 and 390×844. Page scroll widths did not exceed the viewport. Tables scroll within their labelled region; filters adapt from multiple columns to one, tabs wrap, and the shell uses its existing mobile navigation. Native attendance selects were corrected to 44px minimum height during verification. Correction confirmation was opened/cancelled without submission; cancel returned focus to the invoking button. The mobile inspection drawer was opened/closed with focus return and retained list context. Existing reduced-motion behavior and shared primitives are preserved.

Browser date automation required actual keyboard date changes to trigger React handlers; URL-backed queue filtering and keyboard-backed employee history selection were verified. No live mutation was submitted. Correction/finalization/stale states use isolated automated fixtures because changing Development attendance is prohibited. Other role sessions and Firefox/WebKit were not claimed as live-tested. Product-owner approval remains pending.

## Changed files

Created:

- `frontend/src/features/attendance/attendance.css`
- `frontend/src/features/attendance/command.tsx`
- `frontend/src/features/attendance/contracts.ts`
- `frontend/src/features/attendance/format.ts`
- `frontend/src/features/attendance/presentation.tsx`
- `frontend/src/features/attendance/review.tsx`
- `frontend/src/features/attendance/workspace.tsx`
- `frontend/src/test/attendance.test.tsx`
- `docs/frontend/F7-ATTENDANCE-MANAGEMENT.md`
- `docs/frontend/F7.2-EMPLOYEE-CLOCKING-PROPOSAL.md`

Modified:

- `frontend/src/app/router/navigation.ts` — nested attendance review context/capability metadata.
- `frontend/src/app/router/router.tsx` — lazy attendance workspace and review routes.
- `frontend/src/components/layout/shell-navigation.tsx` — retain active Attendance destination on nested review routes.

## Remaining boundaries

F7.2 needs separate backend approval; it is not implemented. Work-calendar/policy configuration UI, device ingestion, employee clocking, automatic current-presence interpretation, bulk finalization, early-release authorization, attendance/payroll deductions and F8 leave management are outside F7. The existing Development calendar gap is displayed, not repaired. No new dependency was added. Frontend and API remain running for manual review.
