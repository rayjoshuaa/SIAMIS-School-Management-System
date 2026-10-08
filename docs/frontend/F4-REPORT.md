# F4 — HR Dashboard

## Scope and implementation

`/hr` now renders a real, read-only operational dashboard in the existing F2 shell. Its heading is Human Resources. No F5 employee management, approval commands, frontend business calculations or new infrastructure were implemented.

Sections:

- Employee record count, including inactive records.
- Today's attendance: effective employees, scheduled/not scheduled, late, approved leave and configuration-required counts.
- Today's attendance attention: RequiresReview, ReadyToFinalize, Stale and ConfigurationRequired. These categories overlap; they are not summed. Staleness is a source change, not a violation.
- Pending leave total and at most five recent Pending requests, with employee identity, type and date range. No reasons or evidence displayed.
- Permitted HR-area shortcuts to existing placeholders, explicitly labelled as not yet connected operational screens.

## Frozen APIs and capability mapping

| Request | Capability | Purpose / bound |
| --- | --- | --- |
| `/api/employees?page=1&pageSize=1` | `Employee.Read` | Server `totalCount`; one row maximum, no full-directory counting |
| `/api/attendance/today?page=1&pageSize=1` | `Reporting.Read` | Server counts before pagination; server business date and Asia/Bangkok timezone; one row maximum |
| `/api/leave-requests/pending?page=1&pageSize=5` | `Leave.Read` | Server Pending total and bounded recent list |

The `/hr` route keeps its established `Reporting.Read` gate. Each section independently checks its actual capability before enabling its query and rendering its data. Roles never infer capabilities. The backend remains authoritative, including 403 handling.

The D9E daily endpoint uses `Reporting.Read` (not `Attendance.Read`). Its existing 500-candidate bound is preserved; a 409 shows unavailable feedback instead of fabricated counts. The current implementation reuses today's response for attention without a duplicate queue request.

Shortcuts use existing route capability metadata: Employee.Read, Attendance.Read, Leave.Read, Payroll.Read, HRDocuments.Read and Security.Manage. Payroll and document data are never requested or displayed, even when those shortcuts are permitted. HRAdmin alone does not imply Payroll access; SystemAdmin does not imply document access.

## Query and state behavior

Three independent TanStack queries use user-scoped keys, cancellation signals and existing defaults: 60-second stale time, no focus refetch/polling and at most one retry for eligible failures. F3 cache clearing and central 401 session-loss handling are unchanged.

Each section has loading, success, factual empty and recoverable error states. A failed optional section does not replace healthy sections. Retry refetches only its query. Forbidden responses are explicitly denied, not displayed as empty. Raw server details are not exposed. Attendance attention uses the same query state as its overview. Zero counts are valid; no fixtures or fallback statistics exist in runtime code.

## Deliberate omissions / future reporting gaps

- No salary, payroll totals, document counts/content, paid/unpaid allocation or browser leave balance/entitlement calculation.
- No frontend attendance calculations, current-presence inference, confirmed absence inferred from missing evidence, payroll deductions or disciplinary consequences.
- No organization-wide historical review backlog or sum across overlapping attendance categories. The frozen review queue needs an explicit date range and bounded candidate scope; a future read screen should let users select/narrow that scope. Today's attention is labelled accordingly.
- No current-leave count inferred from the five Pending rows. Future leave-calendar reporting would need a clearly scoped Approved/date-overlap read and approved semantics.
- No workforce trends, department analytics or unbounded aggregation. Future aggregate reporting contracts should expose these explicitly if required.
- Inspected the privacy-minimized `staff-overview` and `leave-status` endpoints. They are not used: directory/status rows add no necessary metric to this dashboard; Pending operations require the existing Pending endpoint.
- Attendance mutation/finalization and leave approval screens remain future checkpoints; links do not claim those operations are implemented.

## Verification

- Frontend: 85/85 tests pass across five files (74 existing F1–F3 tests plus 11 focused F4 tests). Covers real response totals, bounded reads, zero/empty, independent loading/error/network states, targeted retry, 403/409, capability visibility and non-fetching, navigation/privacy, central 401 session loss/cache clearing and route admission.
- Production frontend build, lint, formatting and `git diff --check`: pass.
- Unchanged backend Release build: pass, zero warnings and errors.
- EF Release `has-pending-model-changes`: no changes since last migration. No migrations created or applied.
- Chromium (installed Google Chrome via Playwright): verified 1440×900, 1280×800, 1024×768, 768×1024, 430×932 and 390×844; screenshots inspected. No horizontal page overflow; cards stack, text wraps and touch-sized links remain usable. Mobile drawer/Escape, empty state, independent error/retry, keyboard navigation and no runtime errors verified.
- Firefox/WebKit launch attempted: executables unavailable locally. No Firefox, WebKit, Safari/macOS or real-device testing claimed.
- Browser verification uses intercepted frozen-contract responses only inside the test harness. No runtime mocks, fake sessions or data fixtures are shipped. This is not an authenticated live-backend end-to-end verification: Development contains zero Users and no account was provisioned.
- Initial sandbox test temp-file access and SQL/.NET access failures were resolved by rerunning the same verification with approved local access. A browser harness selector/matching issue was corrected in the harness; no application workaround was added.

Read-only SQL table counts before/after verification match exactly across all 86 tables: Employees 1, EmploymentRecords 1, PayrollComponents 17, Users 0, Roles 5, SecurityAuditEvents 15, applied migrations 42; Attendance/Leave/Payroll operational and document tables remain empty. No data writes or temporary accounts were performed. Backend source, schema, migrations, seeds, authorization, authentication, payroll/leave/attendance semantics and F1–F3 presentation remain unchanged. No secrets or permanent fixtures introduced. No commit or push.

## Complete changed-file list

- `frontend/src/app/router/navigation.ts`
- `frontend/src/app/router/router.tsx`
- `frontend/src/features/hr/dashboard-contracts.ts`
- `frontend/src/features/hr/dashboard.tsx`
- `frontend/src/test/hr-dashboard.test.tsx`
- `docs/frontend/F4-REPORT.md`

Preview: `http://localhost:5175/hr` (requires an authenticated session with Reporting.Read; existing fail-closed F3 behavior applies). F4 ends here; F5 has not started.
