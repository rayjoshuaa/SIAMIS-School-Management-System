# D15 inspection finding inventory

Read-only inspection completed before application repairs or mutation tests.

Baseline: clean `ebe172386e916bfd412d30051befa7bf9fdc3a61`; .NET SDK 10.0.401; EF Core 10.0.12; 85 application tables; 42 applied migrations, latest D14; 172 master rows; 5 roles; one original inactive employee/current employment record; all other operational/Identity/audit tables empty. Private storage root absent. Exact full-row and schema inventory saved in ignored test artifacts.

## Items requiring verification

- D15-01, potential B defect: Program.cs has no common safe exception boundary. D14 alone catches unexpected errors. An unexpected SQL/Identity error may expose the Development exception page; verify without touching SIAMIS by using an isolated API process pointed at an unavailable loopback port. Intended response must be safe ProblemDetails, retaining existing business status codes.
- D15-02, B documentation defect: EmployeePayrollsController approval/paid/cancel remarks still say authentication/actor attribution must be added, although D10/B1 implementation already supplies both. Correct those comments only after confirming actual lifecycle attribution. General Swagger does not name the existing route capabilities; produce an authoritative role/capability/route matrix from current routes and policy mapping.

## Inspected intentional boundaries, not feature defects

- ApplicationUser/Employee/employment are distinct; inactive baseline with open employment is explicitly legacy review state, not silently repaired.
- Manual EmployeeHistory descriptive ChangedBy is not an authenticated actor; D10 SecurityAuditEvents independently record the true actor. Administrative correction DELETE was explicitly approved in the original history contract. No new retention policy.
- Historical payroll/configuration provenance uses immutable IDs/JSON snapshots without live SourceId FKs. Removing live configuration cannot rewrite those snapshots.
- D12 permits retained future Pending Leave and Approved snapshots after ending employment. No automatic cancellation; future creation remains date-effective. Stale attendance requires explicit reopening.
- Only Identity-owned child records and mutable PayrollRuleTargets cascade; personnel/history/document FKs do not cascade.
- Account administration list performs bounded per-user role/readiness reads. Maintainability/performance debt; no evidence yet of a frontend blocker. Attendance reporting uses bounded batched sources and established query-count tests.
- Production email/private storage/key persistence, coordinated backup, malware scanning and TLS/proxy setup remain deployment obligations. No vendor, purge duration, deductions, KPI, school calendar or new legal policy is selected.

## Confirmed repairs allowed by the existing contracts

- D15-01 confirmed: isolated unavailable loopback SQL returned an 8,352-byte Development exception response containing SqlException/stack details. Production returned an empty 500. Added a safe common 500 ProblemDetails boundary; business failures remain untouched.
- D15-02: replaced five obsolete Payroll lifecycle authentication remarks with the existing Payroll.Manage/authenticated-actor contract.
- D15-03, B validation defect: `GET /api/employees?page=2147483647&pageSize=20` returned 500 due to Int32 offset overflow. Added Application DTO range validation using a wide integer intermediate, matching established bounded paging patterns.
- D15-04, B validation defect: embedded Contacts/Addresses arrays containing null returned 500. Added explicit null-element rejection in existing application validation; malformed requests return 400 before mutation.
- D15-05, B validation bypass: aggregate Employee POST accepted an embedded emergency contact without Phone (201), despite the approved Emergency Contacts required-Phone contract. Added Required to that existing request property. No schema change, no new field or business rule.

Each observed fixture was cleaned to the exact baseline. Final after-repair proof, cross-domain coverage and freeze decision belong in D15-REPORT.md.

## D15-06 — approved authorization/privacy repair

- Original classification: **A Blocker / F Business decision required**. Salary Change EmployeeHistory carried salary PreviousValue/NewValue under Employee.Read/Employee.Manage, which HRAdmin holds without Payroll capabilities.
- Repository evidence: HrAuthorizationFilter.cs:46; SecurityContracts.cs:46; EmployeeHistoryService.cs:16,84-93. The original EmployeeHistory request explicitly included salary amounts; the later approved D10 boundary excludes HRAdmin from Payroll and prohibits exposing payroll fields through Employee.Read alone.
- Human approval: Salary Change requires Payroll.Read/Manage independently; ordinary events keep Employee.Read/Manage; mixed lists omit unauthorized categories completely. No redacted placeholders, text guessing, role grant changes or schema changes.
- Implementation: route admission is the union; EmployeeHistoryService enforces event-specific capabilities in database queries and create validation. Direct unauthorized/nonowned IDs use existing 404 resource hiding; unauthorized creation is 403. Filtering precedes ordering/projection; existing array has no pagination or totals. Authenticated security-audit actor remains server-derived.
- Final proof: 29 real Identity/API/SQL privacy checks, 1,416 six-role route probes, all required fresh regressions and exact Development cleanup passed. Release build has 0 warnings/errors; EF has no pending model changes. D15-REPORT.md records READY TO FREEZE, the complete manifest and operational deferrals. No unresolved blocker; no schema/role grants/financial calculations changed; no commit/push.
