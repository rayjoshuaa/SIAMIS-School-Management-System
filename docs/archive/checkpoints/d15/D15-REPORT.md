# D15 — HR Backend V1 closure audit and freeze gate

## 1. Executive summary

The repository, actual local SQL schema, HTTP contracts, domain implementations and security gates were inspected before repairs. The five initial unambiguous application/documentation repairs are preserved. D15-06 was stopped for contract approval, then repaired using the explicitly approved event-specific authorization boundary. No financial calculations, schema, migrations or role grants changed. Fresh verification covers existing domains and composed cross-domain workflows.

**The originally reproduced salary-history conflict is repaired.** Salary Change now requires Payroll.Read/Manage independently; ordinary history retains Employee.Read/Manage. Unauthorized categories are omitted by SQL predicates before sorting/projection. Direct unauthorized IDs return 404; unauthorized creates return 403. Existing data and true authenticated audit attribution remain intact. Focused proof passed 29 checks; all required fresh suites and exact cleanup passed. No additional unresolved blocker or material defect was identified.

This report distinguishes passing current-route enforcement from whether the route's capability boundary is appropriate. A successful role matrix cannot establish confidentiality of fields inside an otherwise authorized HR response.

## 2. Freeze recommendation

**HR BACKEND V1 — READY TO FREEZE**

SIAMIS HR Backend V1 is ready to freeze.

Approved and implemented D15-06 contract:

- Salary Change list/detail reads require Payroll.Read; create/delete require Payroll.Manage, independently of Employee capabilities.
- Ordinary events retain Employee.Read/Employee.Manage.
- A mixed list returns only the event categories the actor may read: HR-only omits Salary Change; Payroll-only sees Salary Change; combined capabilities see both. Direct-ID access to an unauthorized event is denied.
- No role/capability grants are expanded. PayrollAdmin can operate financial history under its existing financial capabilities; it does not gain ordinary Employee history access.

No redacted financial placeholders; no inference from free-form text. The user explicitly approved independent PayrollAdmin financial-history access and the union-filtered list. The earlier stop was respected before implementation.

Final fresh verification and the freeze manifest are recorded below. All six repairs remain reviewable in the existing working tree. No commit or push.

## 3. Repository/database baseline

| Item | Captured baseline |
|---|---|
| Git HEAD | ebe172386e916bfd412d30051befa7bf9fdc3a61 |
| SDK / EF Core | 10.0.401 / 10.0.12 |
| Provider / target | SQL Server 2025; localhost/SIAMIS; Windows integrated authentication; TrustServerCertificate |
| Application tables / migrations | 85 / 42 |
| Latest migration | 20261004115720_AddSecureHrDocumentFoundation |
| Employees / EmploymentRecords | 1 / 1 |
| Original employee | TEST-EMP-001; 433f2c1a-6222-494f-a64f-cd0c31126dc4; inactive |
| Reference rows / Roles | 172 / 5 |
| Other operational, Identity-user and audit rows | 0 |
| Private document root | Absent |

Full rows, including timestamps, were captured before fixtures in ignored `tests/SIAMIS.Payroll.RegressionTests/bin/d15-baseline.json`. Schema inventory is in adjacent `d15-inventory.json`. These potentially confidential runtime artifacts are ignored and are not report deliverables.

## 4. Audit methodology

Read actual entities, EF configurations, migrations, controllers, application contracts, services, authorization filters, Identity and storage code. Compare actual SQL columns, constraints, indexes and applied migration history. Inspect historical checkpoint contracts when repository intent is unclear. Use real Identity cookies, CSRF and SQL persistence for live tests; isolated synthetic fixtures only. Compare complete baseline rows and private storage after cleanup. Production-mode HTTP probes use only localhost/SIAMIS with an explicitly trusted loopback relay; they do not contact a Production/VPS database or validate a real external TLS deployment.

## 5. Finding classification summary

| Finding | Classification / disposition |
|---|---|
| D15-01 Unhandled failures expose Development stack/SQL details | B Defect; repaired and isolated failure tests passed |
| D15-02 Payroll Swagger remarks falsely defer existing auth/actor support | B Defect (documentation); five comments corrected |
| D15-03 Employee page offset overflows Int32 | B Defect; Application DTO now rejects offset with 400 |
| D15-04 Embedded null child elements produce 500 | B Defect; rejected with 400 before mutation |
| D15-05 Core Employee emergency contact bypasses required Phone | B Defect; Required restored on existing DTO field |
| D15-06 Salary Change history exposed outside Payroll boundary | Original A/F stop; approved event-specific boundary implemented and focused proof passed |
| Bounded account-list per-user role/readiness reads | E Future enhancement; not a demonstrated frontend blocker |
| Production transport, delivery, storage/backup and operations | D Operational requirements; not implemented implicitly |

Five B findings repaired; D15-06's A/F boundary resolved by explicit human approval, repair and passing verification. No additional proven C frontend-contract gap requires implementation. Deferred items are categorized in section 38. Final fresh regression, exact cleanup and build/EF checks passed; no unresolved A/B correctness/security/history finding remains.

## 6. Database/migration audit

Actual 42 migration IDs match repository ordering. All 112 foreign keys and 131 check constraints are enabled/trusted. Employee/personnel/historical/document foreign keys use NoAction. Seven cascades are limited to mutable PayrollRuleTargets and Identity-owned children. Payroll has unique EmployeeId/PayrollPeriodId; current EmploymentRecord has a filtered unique employee index; User.EmployeeId is optional/unique. EmployeeContract ownership uses employee/document composite identity. No D15 migration, entity or schema change; no migration applied.

## 7. Employee identity audit

EmployeeNumber belongs to Employees, uniquely indexed; EmployeeId is stable GUID. No employee hard-delete API. Omitted create manager is valid, self-reporting update is rejected, references remain validated. Profile omission preserves optional TeacherProfile. Supporting Contacts/Addresses/EmergencyContacts enforce ownership and primary promotion. The intentional original inactive employee/open employment baseline was preserved, not silently repaired. Salary history now uses the approved separate financial boundary in sections 20 and 28.

## 8. Employment lifecycle audit

Date-effective employment uses inclusive dates and explicit lifecycle commands with expected current-record protection. End/rehire retain historical records and the same Employee identity. Core profile correction does not rewrite ended employment history. D12 requires an explicit linked-account disable/retain decision; rehire never automatically reactivates an account. No automatic future Leave cancellation or calendar removal was introduced.

## 9. WorkCalendar audit

Explicit EmployeeWorkCalendarAssignment is authoritative. No automatic IsDefault fallback or organization-default history. ExceptionalWorkingDay intervals replace weekly intervals. Published Leave policy and date-effective assignments govern calculation; no assumed eight-hour day. Archiving a live source does not silently rewrite historical snapshots.

## 10. Leave audit

Whole-minute precision, calendar-year entitlement, versioned Draft-to-Published policies and append-only adjustments remain intact. Pending/Approved lifecycle uses expected-status protection and Employee-first locking. Frozen approval facts are retained. Approved cancellation remains allowed after attendance finalization; current Attendance becomes stale without mutation callbacks. No Attendance/Payroll integration or new statutory entitlement selected.

## 11. Paid/Unpaid Leave audit

D11 freezes paid/unpaid minute allocation and segments. Paid tracked balances reserve/consume only paid minutes under the approved exhaustion policy; unpaid coverage remains Leave coverage. Cancellation releases balances according to existing lifecycle. Legacy snapshot compatibility remains explicit. No unpaid-minute payroll deduction is inferred.

## 12. Evidence/Sandwich audit

ExternalReceipt acceptance/review remains independent of binary storage. Attaching, replacing or archiving a supporting private binary cannot change receipt acceptance or frozen approval evidence. Sandwich review is explicit, capped according to approved policy, and retained historically. No new document requirement, entitlement or payroll interpretation introduced.

## 13. Attendance evidence audit

Raw timestamps remain datetime2(7); evidence/corrections are separately attributable. Equal-time ambiguity is not resolved by GUID order. Manual corrections require established reasons/version/source protection. Legacy Attendance mutation surfaces remain retired (410), not resurrected. No device import or facial-recognition integration added.

## 14. Attendance calculation audit

Exact tick interval partition is authoritative; wide integer millisecond aggregates truncate independently. CoverageTruncationResidualMilliseconds is precision residue, never unexplained/worked/Leave/payroll time. Exact five-minute grace boundary is tested, including plus one tick. Paid and unpaid frozen Leave intersect coverage without manufacturing sub-minute Leave.

## 15. Attendance finalization/reporting audit

Finalized revisions are immutable. Current/review reads compare deterministic calculation-relevant source fingerprints and return structured stale findings; reads do not reopen. Explicit reasoned reopen and refinalization append revision history. Reporting is bounded and batched (500 employees, 2,000 employee-days, 20,000 source rows); established query-count tests remain in use. Staleness is not misconduct or a deduction.

## 16. Payroll configuration audit

Supplement and ReplaceAssignment remain explicit; replacement suppresses only the matching component assignment. Multiple replacement conflicts fail. Rule targets/exclusions and effective dates are unchanged. SSO classification remains Unknown for existing components; no legal Included/Excluded interpretation assigned. PIT and typed published statutory configuration stay versioned/fail closed.

## 17. Payroll calculation audit

No calculation implementation changed. D3 date-effective salary entitlement/proration, D5 SSO and D6 PIT formulas/recognition/rounding retain approved contracts. Percentage min/max checks are eligibility on the original resolved base, not clamping. Generated lines snapshot BasicSalary/Assignment/PayrollRule provenance and classifications; SourceId is historical data without live-source FK. Attendance lateness, unexplained time and unpaid Leave alone do not change payroll amounts.

## 18. Payroll lifecycle/payslip audit

EmployeePayroll integrity uniqueness, parent-period/lifecycle guards, immutable approved/paid data and authenticated actor audit remain intact. Failed new generation rolls back header/lines; failed regeneration preserves the complete old snapshot. Manual entries require remarks and maintain Manual/null provenance; reconciliation preserves derived totals and established statutory restrictions. Employee self-service is restricted to own Approved/Paid payroll/payslips via User-to-Employee linkage.

## 19. Identity/authentication audit

ApplicationUser uses GUID Identity independently of Employee; optional unique linkage is authoritative for ownership. Real cookie/CSRF flow, per-request account/stamp/roles validation and explicit capabilities are retained. Provisioning/activation/recovery use short-lived trusted verified contacts, single-use credential state, enumeration-resistant responses and session revocation. Development delivery is explicit; Production delivery remains unavailable/fail closed. No account is inferred from employment position.

## 20. Authorization matrix audit

[D15-AUTHORIZATION-MATRIX.md](D15-AUTHORIZATION-MATRIX.md) documents all 236 non-Auth HR routes. Six actual role combinations passed with valid request carriers (1,416 requests): SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin. Own-record exceptions are documented and exercised separately. History route admission allows either appropriate domain capability, while service predicates protect the category. Dedicated D15-06 tests also verified HRAdmin+PayrollAdmin and real event IDs; route-admission probes alone are not used to claim financial confidentiality.

The original broad controller mapping was reproduced before changing it. HrAuthorizationFilter now admits either Employee or Payroll capability; EmployeeHistoryService uses ICurrentActor for database-level list/detail/delete predicates and canonical EventType create validation. All event fields, not just salary numbers, are hidden when the category is unauthorized. Existing type canonicalization remains, so case/space variants cannot bypass the gate. No text-based classification or role grants changed.

| Category / action | Required capability | Unauthorized result |
|---|---|---|
| Ordinary list/detail | Employee.Read | Category omitted from list; direct ID 404 |
| Salary Change list/detail | Payroll.Read | Category omitted from list; direct ID 404 |
| Ordinary create/delete | Employee.Manage | Create 403; existing unauthorized direct delete 404 |
| Salary Change create/delete | Payroll.Manage | Create 403; existing unauthorized direct delete 404 |
| No relevant history capability | None granted | Route 403; anonymous 401 |

HR-only lists contain ordinary events; Payroll-only lists contain financial events; union capabilities contain both. The existing API is an ordered array, without pagination/count metadata. Filtering occurs before ordering/projection/materialization, so there are no financial placeholders, raw total counts or pagination gaps. SQL command inspection confirmed the EventType predicate executes in SQL Server. Normal existing Employee/parent integrity and 404 conventions remain.

## 21. Account lifecycle/offboarding audit

Explicit disable revokes an existing cookie; explicit retain leaves security active while employment ends. Pending future Leave is retained rather than silently cancelled. Rehire retains identity/history, but credential/account state remains separately controlled. Last-SystemAdmin protection serializes the role decision and is covered by a final-two-admin race.

## 22. HR Documents audit

Only HRDocuments.Read/Manage grants authorize confidential metadata/content/lifecycle routes. HRAdmin receives them; SystemAdmin alone has no bypass; explicitly combined roles work. Opaque server keys/private abstraction, 20 MiB limits, PDF/JPEG/PNG validation, SHA-256 and immutable binary versions are retained. Replacement supersedes; normal removal archives. Database/storage failure compensation preserves consistency and retains binaries on uncertain commit. Private paths and keys are absent from DTOs.

## 23. Audit trail audit

SecurityAuditEvents use authenticated actor context; sensitive workflows reject client spoofing according to their established contracts. Created/Updated timestamps use UTC conventions. EmployeeHistory ChangedBy remains an originally approved descriptive field, not proof of authenticated actor; independent security audit supplies the actual actor. Administrative history deletion was explicitly allowed by its original contract, so no new retention rule was invented.

## 24. Cross-domain integrity audit

Composed live fixtures verify actual nonempty Leave/Attendance/Paid payroll/payslip/document history across offboarding/rehire, binary links with frozen Leave evidence, and stale/reopen source changes. Independent isolated component tests were supplemented with a complete hire-to-finalize and payroll-to-offboard chain. D15-06 was an authorization contradiction and is now resolved; no financial calculation change was needed.

## 25. Concurrency/transaction audit

Established Employee-first locking/order, expected versions/statuses, source protection and serializable publication remain unchanged. Last-admin role gate precedes account locks as approved. Existing tests exercise conflicting generation, failed regeneration preservation, Leave transition races, finalization sources and document replace/archive races. No catch-and-ignore transaction or hidden historical rewrite was added.

## 26. Date/time/precision audit

UTC DateTime/datetime2 instants, inclusive date-only employment/policy bounds and Asia/Bangkok attendance business dates remain distinct. Leave whole minutes; Attendance exact ticks then independent integer-ms conversion; payroll decimals, not floating point. No global timezone conversion or guessed future termination scheduling. Existing generic offset-less DTO instants rely on the documented UTC convention; newer workflow DTOs make UTC explicit.

## 27. API/frontend-readiness audit

DTOs avoid EF navigation exposure; controllers do not access DbContext. Swagger includes workflow response schemas/statuses, corrected Payroll remarks and explicit category-specific EmployeeHistory descriptions. Paging now rejects overflow; malformed embedded child requests return validation errors. Conflicts/ownership/nonexistent references stay typed. No UI, generic dynamic engine, new CRUD workflow or production rollout. Approved salary-history semantics are implemented without changing the DTO/array contract.

## 28. Privacy/data-exposure audit

Swagger schemas exclude PasswordHash, SecurityStamp, ConcurrencyStamp, StorageKey and physical paths. Document/SystemAdmin separation and ownership checks pass. Isolated unexpected SQL failure yields generic ProblemDetails rather than Development internals. **Salary Change history is now protected:** the original reproduction has become a passing confidentiality regression. Ordinary Compensation endpoints retain their separate Payroll gate.

Focused live proof used an isolated synthetic employee and actual activated HR-only, Payroll-only, combined, Management and linked Employee accounts. Auth/me confirmed role/capability independence; all fixture rows were removed in finally and exact baseline comparison passed.

| HRAdmin-only request | Before repair | After repair |
|---|---|---|
| GET employee compensations | 403 | 403 |
| POST history, EventType Salary Change | 201 | 403 |
| GET employee history list | 200; salary event exposed | 200; financial event completely absent |
| GET salary history by ID | 200; amounts exposed | 404 |
| DELETE salary history by ID | Broad Employee.Manage gate | 404; row retained |

Payroll-only financial create/detail/delete succeeds, ordinary create returns 403 and ordinary direct read/delete returns 404. Combined readers see both categories; anonymous access remains 401. HR-only list is identical before and after inserting a hidden financial event, proving no placeholder/count/order artifact. Forged actorUserId/createdByUserId request fields are ignored by the unchanged contract, while SecurityAuditEvents records the actual authenticated Payroll user; descriptive ChangedBy is not treated as actor evidence. Amounts were synthetic; existing history was never migrated/rewritten.

## 29. Production fail-closed audit

Production-mode local probes cover secure cookies, trusted forwarding, CSRF and unavailable delivery/private-storage configuration. No real Production database was contacted. Required host obligations include explicit database target, external TLS/proxy validation, persistent Data Protection keys, credential delivery, private storage ACLs, backups and monitoring. Development configuration is not a deployment credential policy.

## 30. Legacy/dead-surface audit

Retired Attendance routes remain explicit 410; no legacy mutation bypass reopened. Generic EmployeeHistory retains approved descriptive fields/admin correction, now with category-specific D10-compatible financial confidentiality. Existing inactive/open-employment test state is intentional compatibility state. No opportunistic deletion of unused-looking source or DTOs.

## 31. Test-quality audit

Fresh D15 regression wrapper disables earlier resume/result skipping. Original financial tests/expectations remain unchanged; established adapters update schema prerequisites and authenticated actor/CSRF setup. One existing document test fixture was corrected to use UTC lifecycle dates, matching the established D1 cutoff after Bangkok midnight; no lifecycle behavior/expected response was weakened. Live API tests use real cookie identities and SQL persistence. Fault interceptors are confined to the explicit regression executable. Role matrix probes test gates, not all business behavior. The old salary-history observation is retained only as ignored historical evidence; the current test requires denied financial access and exact cleanup.

## 32. D15 cross-domain scenario results

| Scenario | Evidence / coverage |
|---|---|
| A Hire/calendar/Leave/Attendance/finalize | D15 composed test: mixed paid/unpaid Leave and real finalization |
| B Leave cancellation/stale/reopen/new revision | D15 composed test preserves exact Revision 1 and creates Revision 2 explicitly |
| C Offboard/disable/session/history | D15 composed test uses nonempty Leave/Attendance/Paid payroll/payslip/Documents; D12 races |
| D Offboard/retain security | Existing D12 live explicit retain scenario |
| E Rehire/history/account control | D15 composed and D12 live |
| F Provision/activate/login/reset/revocation | D13 live and D10 advanced/security suites |
| G Final-two-SystemAdmin race | D10 advanced security suite |
| H Document upload/download/replace/archive | D14 live and D15 composed |
| I SystemAdmin-only document denial | Six-role D15 matrix and D14 live |
| J Evidence binary/frozen D8 facts | D14 live and D15 composed |
| K Paid payroll then offboard/history | D15 composed |
| L No automatic Attendance deduction | D15 composed: actual lateness/unexplained/unpaid coverage, gross/net 30,000 and deductions 0 |

## 33. Full regression results

All final suites ran against the repaired Release build, with fresh results and original monetary expectations. Per-suite totals are reported separately; wrapper bookkeeping and HTTP probe counts are not represented as distinct business scenarios.

| Final suite | Result / total |
|---|---|
| Pure calculator/domain/security/storage tests | PASS; 1,024 assertions |
| D9B / D9C / D9D / D9E live | PASS; 121 / 116 / 174 / 201 |
| D8B-D1 / D8C / lifecycle race | PASS; 117 / 246 / 230 |
| D8D / capped sandwich | PASS; 527 / 392 |
| D5A / D5C / D6B / D6C / D6D / D6E / D6E boundaries / D7 | PASS; 70 / 306 / 76 / 48 / 38 / 88 / 128 / 80 |
| Nested domain total | 2,958 assertions; additional 18 wrapper/baseline checks |
| D13 live / Production / D12 / D11 / D10 security / Production / advanced / anonymous routes / rate limit | PASS; 78 / 7 / 53 / 73 / 45 / 26 / 28 / 237 / 2 = 549 |
| D14 documents / storage faults / configuration guards | PASS; 93 / 19 / 2 |
| Focused D15 Employee / composed cross-domain | PASS; 36 / 25 |
| D15 role matrix | PASS; 236 routes × 6 roles = 1,416 probes; 7 inventory/check assertions |
| Safe unexpected failures | PASS; 2 environments, generic 500 ProblemDetails without internal details |
| Final existing SQL/storage verifier | PASS; 17 checks |
| Repository/schema inspection | PASS; 334 source/inventory checks, not 334 business scenarios |
| D15-06 history privacy repair | PASS; 29 focused capability/data/ownership/audit/cleanup checks |

Results/logs reside under ignored `tests/SIAMIS.Payroll.RegressionTests/bin/`: `d15-all-results.json`, `d15-domain-regressions-results.json`, `d15-employee-results.json`, `d15-cross-domain-results.json`, `d15-routes-results.json`, `d15-failure-results.json`, `d15-history-privacy-results.json`, `d15-final-check-results.json`, and existing D14/security/domain result files. The old `d15-history-privacy-observation.json` documents pre-repair evidence only and is not used as a final passing result.

Transparent harness setup corrections across D15: document login once hit the unchanged one-minute login limiter after security tests (429); cleanup passed, then the suite passed after waiting. The extended TeacherProfile duplicate fixture once exceeded EmployeeNumber's approved 30-character limit (400); only that synthetic number was shortened, then 36 checks passed. The new privacy fixture initially omitted the required Employee-role linkage (400); it now links to the isolated employee and 29 checks passed. The fresh document suite crossed Bangkok midnight and attempted a local-today rehire that was future-dated under D1's UTC cutoff (400); cleanup passed, and three fixture date expressions were aligned with UTC before rerun. Source comparison initially compared Git LF bytes with Windows CRLF bytes; normalized text reads confirm protected implementations unchanged. None prompted an application, security-limit, schema or financial-expectation workaround. The coordinator waits one minute before documents. Conditional lifecycle concurrency assertions can change totals between successful runs without changing expectations; the table reports the actual fresh pass.

## 34. Exact Development cleanup

All fixture scripts used finally cleanup of their recorded synthetic IDs. Final read-only comparison passed against the original full rows/timestamps across all 85 tables; all 42 migration rows are unchanged. TEST-EMP-001 is the only employee and remains inactive with its original current EmploymentRecord. No temporary users, operational audit events, Leave/Attendance/payroll/doc fixtures or private binaries remain. Private root is absent, matching initialization state. Both owned API processes in the final continuation (6840 Development, 23476 local Production-mode) were stopped after PID/command verification; no SIAMIS API process remained. Git HEAD is unchanged. Full final table counts are listed below.

| Application table | Final rows |
|---|---:|
| AddressTypes | 3 |
| Attendance | 0 |
| AttendanceEvents | 0 |
| AttendanceReviewActions | 0 |
| AttendanceReviewCases | 0 |
| AttendanceStatuses | 11 |
| ContractTypes | 7 |
| Countries | 13 |
| Departments | 12 |
| Designations | 20 |
| DocumentTypes | 15 |
| EmergencyContacts | 0 |
| EmployeeAddresses | 0 |
| EmployeeCompensations | 0 |
| EmployeeContacts | 0 |
| EmployeeContracts | 0 |
| EmployeeDocuments | 0 |
| EmployeeHistory | 0 |
| EmployeeLeave | 0 |
| EmployeeLeaveAllocations | 0 |
| EmployeeLeaveApprovalEvidence | 0 |
| EmployeeLeaveEntitlementAdjustments | 0 |
| EmployeeLeaveEntitlements | 0 |
| EmployeeLeaveEvidence | 0 |
| EmployeeLeaveEvidenceEvents | 0 |
| EmployeeLeaveSandwichAllocations | 0 |
| EmployeeLeaveSandwichCases | 0 |
| EmployeeLeaveSandwichDates | 0 |
| EmployeeLeaveSandwichEvents | 0 |
| EmployeePayrollComponentAssignments | 0 |
| EmployeePayrollLines | 0 |
| EmployeePayrollPitResults | 0 |
| EmployeePayrollSocialSecurityResults | 0 |
| EmployeePayrollStatutoryResults | 0 |
| EmployeePayrolls | 0 |
| EmployeePayslips | 0 |
| EmployeePerformance | 0 |
| EmployeePitPaymentScheduleEntries | 0 |
| EmployeePitPaymentScheduleSelections | 0 |
| EmployeePitPaymentSchedules | 0 |
| EmployeeStatutoryEnrollments | 0 |
| EmployeeTaxClaims | 0 |
| EmployeeTaxDeclarationSelections | 0 |
| EmployeeTaxDeclarations | 0 |
| EmployeeTaxOpeningBalances | 0 |
| EmployeeTaxProfiles | 0 |
| EmployeeWorkCalendarAssignments | 0 |
| Employees | 1 |
| EmploymentRecords | 1 |
| EmploymentStatuses | 9 |
| EmploymentTypes | 6 |
| FinalizedAttendanceRevisions | 0 |
| Genders | 4 |
| HiringSources | 10 |
| LeavePolicies | 0 |
| LeaveTypes | 10 |
| Locations | 5 |
| MaritalStatuses | 6 |
| Nationalities | 13 |
| OrganizationProfiles | 0 |
| PayTypes | 6 |
| PayrollComponents | 17 |
| PayrollPeriods | 0 |
| PayrollRuleTargets | 0 |
| PayrollRules | 0 |
| PayrollSettings | 0 |
| PerformanceRatings | 5 |
| PitPolicyConfigurations | 0 |
| PitTaxBrackets | 0 |
| RoleClaims | 0 |
| Roles | 5 |
| SecurityAuditEvents | 0 |
| SocialSecurityPolicyConfigurations | 0 |
| StatutoryPolicyVersions | 0 |
| StatutorySchemes | 0 |
| TeacherProfiles | 0 |
| UserClaims | 0 |
| UserLogins | 0 |
| UserRoles | 0 |
| UserTokens | 0 |
| Users | 0 |
| WorkCalendarDateOverrides | 0 |
| WorkCalendarOverrideIntervals | 0 |
| WorkCalendarWeeklyIntervals | 0 |
| WorkCalendars | 0 |


## 35. Fixes made during D15

1. Program.cs: common generic 500 ProblemDetails handler before Swagger/endpoints; unexpected details remain in server logs.
2. EmployeePayrollsController / PayrollPeriodsController: five obsolete lifecycle remarks corrected to Payroll.Manage/authenticated actor.
3. EmployeeListQuery implements IValidatableObject and uses Int64 intermediate to reject offsets above Int32.MaxValue.
4. EmployeeService rejects null embedded Contacts/Addresses/EmergencyContacts before property validation.
5. EmergencyContactRequest.Phone has Required plus its existing 30-character length limit, matching dedicated workflow.
6. Approved D15-06: history route admits Employee or Payroll capability; EmployeeHistoryService filters reads/deletes by authorized explicit category in SQL and validates canonical create category; controller maps forbidden create to 403 and documents category behavior in Swagger.

No entities/configurations/migrations/schema/role-grant changes. No payroll/SSO/PIT/Leave/Attendance calculation or lifecycle behavior changed. Existing EmployeeHistory data remains intact.

## 36. Complete changed-file list

Modified:

- src/SIAMIS.Api/Program.cs
- src/SIAMIS.Api/Controllers/EmployeePayrollsController.cs
- src/SIAMIS.Api/Controllers/PayrollPeriodsController.cs
- src/SIAMIS.Api/Controllers/EmployeeHistoryController.cs
- src/SIAMIS.Api/Security/HrAuthorizationFilter.cs
- src/SIAMIS.Application/Employees/EmployeeContracts.cs
- src/SIAMIS.Infrastructure/Services/EmployeeService.cs
- src/SIAMIS.Infrastructure/Services/EmployeeHistoryService.cs
- tests/verify_d14_live.py (three lifecycle fixture dates now use the approved UTC date)

Created:

- D15-REPORT.md
- D15-FINDINGS.md
- D15-AUTHORIZATION-MATRIX.md
- tests/inspect_d15.py
- tests/verify_d15_repository.py
- tests/verify_d15_regressions.py
- tests/verify_d15_failures.py
- tests/verify_d15_employee.py
- tests/verify_d15_cross_domain.py
- tests/verify_d15_routes.py
- tests/verify_d15_all.py
- tests/verify_d15_history_privacy.py

Ignored generated test logs/results/runner files and temporary process logs are not source changes or deliverables.

## 37. Freeze manifest

**HR V1 freeze manifest — verification complete.** Freeze scope is the existing working tree against `ebe172386e916bfd412d30051befa7bf9fdc3a61`, including the complete changed-file list in section 36; no new Git commit or release deployment is claimed. Controller names below identify the current API groups; exact routes and capabilities are in the authorization matrix.

| Frozen domain | Current API boundary | Persistence boundary | Key invariant | Intentional deferral |
|---|---|---|---|---|
| Employee | Employees, Contacts, Addresses, EmergencyContacts, History, Performance; master-data GETs | Employees; supporting records; TeacherProfiles; EmployeeHistory | Stable unique employee identity; owned child records; category-specific financial history | Teacher KPI; broader analytics |
| Employment | EmploymentLifecycle, EmploymentStatuses | EmploymentRecords / status master | Inclusive date-effective history; one current record; explicit end/rehire | Future scheduled termination; final pay |
| WorkCalendar | AttendanceFoundation / LeaveFoundation calendar and assignment APIs | WorkCalendars, weekly/override intervals, EmployeeWorkCalendarAssignments | Explicit assignment only; exceptional intervals replace weekly schedule | Organization-default history; device integration |
| Leave | EmployeeLeave / LeaveOperations | EmployeeLeave / allocations / frozen calculation | Versioned policy, whole-minute intervals, lifecycle/concurrency protection | New statutory policy or payroll integration |
| Leave entitlement | LeaveFoundation / LeaveOperations balances | EmployeeLeaveEntitlements / adjustments | Calendar-year minute accounting; append-only adjustments | New entitlement formulas |
| Leave evidence | LeaveEvidenceSandwich and explicit binary association | Evidence / events / approval evidence | Accepted receipt and frozen approval facts independent of supporting bytes | New evidence rules; employee document self-service |
| Sandwich Leave | LeaveEvidenceSandwich review APIs | Sandwich cases / dates / allocations / events | Explicit capped review; approved timing; append-only history | New sandwich policy |
| Paid/Unpaid exhaustion | Existing Leave preview/create/approve/balance surfaces | Frozen paid/unpaid segments and policy | D11 allocation/precision; no inferred payroll deduction | New school payment policy |
| Attendance evidence | AttendanceFoundation / AttendanceReview corrections | AttendanceEvents / adjudications | Exact immutable source timestamps; attributable corrections | Facial-recognition/biometric import |
| Attendance calculation | AttendanceDays | Read calculations over authoritative sources | Exact tick partition; integer-ms residue is not absence | Payroll/KPI/disciplinary consequences |
| Attendance review/finalization | AttendanceReview | Review cases / actions / finalized revisions | Immutable revision; structured stale reads; explicit reasoned reopen | Automatic reopen/refinalization |
| Attendance reporting | AttendanceReporting | Batched read-only authoritative/historical sources | Bounded queries; current validity distinct from historical finalization | Advanced analytics |
| Payroll | Payroll configuration, rules, settings, assignments, preview/generation, EmployeePayrolls/Periods | Components/rules/settings/assignments/periods/headers/lines | Existing formulas; unique employee-period; rollback/preservation; snapshots; guarded lifecycle | Attendance/Leave deductions; maker-checker; final pay |
| Payslips | PayrollOperations / authenticated SelfService | EmployeePayslips / immutable paid payroll snapshots | Own Approved/Paid self-service only; historical identity/data retained | New delivery/payment interfaces |
| Statutory/tax implementation | Existing Statutory, Section33, Tax/PIT controller groups | Versioned schemes/policies, employee enrollment/declarations, SSO/PIT historical results | D3/D5/D6 approved contracts; explicit supported method version; no legal classification guessing | Provident Fund; new legal interpretation/configuration |
| Identity/authentication | Auth / current-user endpoints | GUID ApplicationUser / Identity children | Authenticated principal; CSRF; session/stamp/account validation; no employee ID from client as proof | Production transport/provider rollout |
| RBAC/ownership | Existing filters, service capability gates and self-service checks | Explicit Role/UserRole grants; optional unique User-to-Employee linkage | No department/title inference; HR Documents separated; Salary Change Payroll-only | Per-user overrides; new role ownership |
| Account lifecycle | AdminUsers / account-lifecycle review | Users / role grants / security audit | Explicit provision/status/roles; last-admin protection | Automatic accounts from employment |
| Credential lifecycle | Auth activation/password/recovery; explicit Development delivery | Identity token/stamp/recovery state | Trusted verified contact; short-lived single-use credential state; old-session revocation | Production email provider |
| Offboarding/rehire | EmploymentLifecycle / account lifecycle | Closed/new EmploymentRecords; separately controlled Users | Explicit account disable/retain; history unchanged; rehire does not enable account implicitly | Automatic future Leave cancellation; final pay |
| HR Documents | EmployeeDocuments / HrDocuments | EmployeeDocuments version/lifecycle metadata + private binary abstraction | Confidential gates; opaque keys; SHA-256; immutable versions; archive; compensated failures | Storage vendor; purge duration; malware scanning integration |
| Audit/security foundation | Established authenticated workflow attribution | SecurityAuditEvents and immutable domain histories | Actor server-derived; preserved historical provenance; descriptive ChangedBy not actor proof | New universal retention/purge policy |

After freeze, changing these business semantics requires a new explicitly approved checkpoint. School Management work must not opportunistically reinterpret them. Deployment/operational obligations in section 38 remain required before Production use.

## 38. Deferred register

| Category | Explicitly deferred |
|---|---|
| Deployment/operations | TLS/reverse proxy; persistent keys; credential delivery; private path/ACL; malware scanning; coordinated SQL/binary backup; monitoring; disaster recovery |
| Integrations | Facial-recognition/biometric import; selected email vendor; accounting/payment interfaces |
| Future HR | Employee document self-service; future scheduled termination; final pay/severance; retention/purge automation; teacher KPI/appraisal; advanced analytics; Provident Fund |
| Business policy unapproved | Attendance-to-Payroll deductions; automatic Leave cancellation at termination; maker-checker approval; disciplinary consequences |

None is silently implemented or treated as an existing claimed V1 feature. D15-06 is resolved and is not a deferred enhancement.

## 39. Remaining risks

No newly identified blocker is being accepted implicitly. Financial history is classified only by approved EventType, not free-form text; arbitrary sensitive text in ordinary notes cannot be automatically detected, as explicitly acknowledged by the approval. Production validation remains separate from local tests. Bounded account-list query debt and generic UTC DTO convention remain documented. Historical state is frozen by approved workflows, not by invented universal retention/purge rules. All required fresh regression/cleanup evidence passed; deployment/operations and explicitly deferred policies remain the practical limits of acceptance.

## 40. Final verification/status table

| Check | Final result |
|---|---|
| dotnet restore | PASS; all projects up to date |
| dotnet build .\\SIAMIS.sln -c Release --no-restore | PASS; 0 warnings, 0 errors |
| dotnet ef migrations has-pending-model-changes --project .\\src\\SIAMIS.Infrastructure --startup-project .\\src\\SIAMIS.Api --configuration Release --no-build -- --environment Development | PASS; no changes since last migration |
| git diff --check | PASS |
| Tracked + untracked source/config secret-pattern scan | PASS; no credential/token/private-key matches; runtime artifacts excluded/ignored |
| Exact 85-table baseline / 42 migration rows / private storage | PASS |
| Protected Payroll/SSO/PIT/salary/Leave/Attendance implementations | Unchanged against HEAD using normalized source text |
| Owned API processes | Stopped; no development test server left running |
| No Production/VPS database, schema/migration change, commit or push | Confirmed |
| Salary-history confidentiality | PASS; approved D15-06 boundary, 29 focused checks, SQL filtering and real actor audit verified |
| Freeze gate | HR BACKEND V1 — READY TO FREEZE |

Final verification date: 2026-10-05 Asia/Bangkok (2026-10-04 UTC), local Development localhost/SIAMIS only. D15-06 is resolved; no unexplained financial regression, migration inconsistency, additional unresolved blocker or material defect was found. All 12 coordinator suites completed with error = null, including the current privacy regression and exact final baseline. Protected calculation/lifecycle sources, role grants, schema and migration history are unchanged. No commit/push; no test API left running. This is backend contract freeze acceptance, not Production deployment approval.
