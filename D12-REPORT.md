# D12 - Employment Offboarding and Account Lifecycle

## 1. Executive summary

D12 adds explicit, authorized account-access resolution to the existing employment-ending command. HR employment, personnel identity and security identity remain separate. Offboarding preserves identity, linkage, roles and all historical domain data. No migration, scheduler, financial calculation, automatic Leave cancellation or Attendance mutation is introduced. No commit or push.

Implementation and all verification gates passed. Release build: zero warnings/errors. EF: no pending model changes. Exact Development baseline restored. All verification API processes started for this task were stopped.

## 2. Repository/database state inspected

Starting commit: `9463619` (completed D11). .NET SDK 10.0.401, EF Core 10.0.12, SQL Server 2025. Development target: `localhost/SIAMIS`, Windows integrated authentication, trusted development certificate. No production/VPS connection is used.

Inspected Employee CRUD/status, EmploymentLifecycleService/EmploymentIntegrity, UserManager/Identity and cookies, account administration, capability/ownership filter, actor audits, effective employment resolution, calendar assignments, Payroll generation/lifecycle, Leave allocation/lifecycle and Attendance finalization/stale reads. Identity and HR share SIAMISDbContext and its SQL transaction.

Initial SQL state: 85 application tables, 41 applied migrations (latest `20261004072245_AddLeavePaidUnpaidAllocation`), 172 master rows, five permanent roles, one inactive TEST-EMP-001 and one current/open EmploymentRecord. Users and operational workflow tables were empty. Every original application row and timestamp and the migration history were captured before fixture creation.

## 3. Employee vs Employment vs User lifecycle

- Employee is retained personnel identity; EmployeeId never changes during end/rehire.
- EmploymentRecord is dated employment context/history. EndDate remains inclusive; ending closes the current slot. Rehire creates a subsequent non-overlapping record for the same employee.
- User is authentication identity, linked optionally and uniquely to Employee. Enable/disable and role capabilities remain security administration decisions.
- User/Employee linkage, roles and audit attribution are retained during all lifecycle operations.

## 4. Offboarding contract

`POST /api/employees/{employeeId}/end-employment` retains its route and now requires `ExpectedEmploymentRecordId`. This identifies the exact current record the caller reviewed; a stale command cannot terminate a later rehire.

If an active linked account exists, also supply an explicit `DisableLinkedAccount` and current `ExpectedLinkedAccountVersion`. Obtain both expected identifiers from the safe account-lifecycle read. Both disable and retention require Employee.Manage plus Security.Manage. A valid combined command holds Employee then User locks in a Serializable transaction and commits HR, Identity and audit changes atomically.

Missing access decision returns 409 with ProblemDetails `code=active_linked_account_requires_offboarding_decision`. Stale/missing account version or stale employment ID returns 409 `code=conflict`; invalid input/date returns 400; insufficient combined capability returns 403; missing resource returns 404. Unknown lifecycle request properties are rejected, including forged actor IDs.

## 5. No-user behavior

Employment ends normally with Employee.Manage. No fake User, account mutation or security decision is required. The employment audit records `AccountAccess=NoLinkedAccount`.

## 6. Already-disabled behavior

Employment ends normally with Employee.Manage. Account state, security stamp, concurrency/administration version and roles remain unchanged. The employment audit records `AccountAccess=AlreadyDisabled`; there is no redundant account-disable event.

## 7. Active-user explicit decision behavior

`DisableLinkedAccount=true` disables the existing linked User and rotates its security stamp. `false` intentionally retains the active User and existing sessions. Both outcomes advance the administration version and create an explicit account-decision audit plus employment-end audit. Neither deletes User/linkage/roles or substitutes another identity. Missing decision produces no HR or User mutation.

## 8. Future-dated employment end

The inspected repository already rejects future-dated lifecycle actions and requires current records to be open-ended. D12 preserves that rule: future EndDate is 400 before account resolution. No immediate future disable, scheduler, persistent keep-access exception or automatic authentication-by-employment rule is needed or introduced. Future termination scheduling requires a separately approved contract.

## 9. Rehire behavior

The existing rehire command retains EmployeeId, adds non-overlapping employment history and restores the HR IsActive flag. It does not enable a disabled User, change its roles, rotate its identity or erase prior employment. Account activation remains explicit Security.Manage administration. A stale end command referencing the former record returns 409 after rehire.

## 10. Multiple-employment behavior

Existing SIAMIS rules prohibit overlapping intervals, multiple open records and multiple current records. SQL `UX_EmploymentRecords_CurrentPerEmployee` is a unique filtered EmployeeId index for IsCurrent=1. Thus a second legitimate current employment cannot remain when this end command succeeds. The command validates existing history and refuses inconsistent history rather than deciding which record to end or disabling access arbitrarily.

## 11. Employee.IsActive relationship

The existing end operation sets Employee.IsActive=false; rehire sets it true. This HR administrative flag is not a security trigger. D12 does not change the ordinary status endpoint or use it as a login condition. TEST-EMP-001's existing inactive/current-open legacy state is preserved exactly.

## 12. Account disable/reactivation

Existing D10 user status endpoints remain the sole independent enable/disable interface, requiring Security.Manage and the expected administration version. These operations preserve UserId, Employee linkage and employment history. Explicit activation after employment has ended can restore the User's existing D10 capabilities; it does not reopen employment. The normal role and resource ownership restrictions still apply.

## 13. Session invalidation

D10's existing cookie principal validation checks authoritative IsActive, lockout and security stamp on every request. Disable prevents new login and rejects the previous authenticated session on its next protected request, including cookies issued during a racing login. It does not attempt to revoke work already completed by an in-flight request. Reactivation rotates the stamp again; the old session stays invalid and fresh authentication is required. Retention deliberately preserves the stamp and session. No custom session framework or cookie validation change is introduced.

## 14. Authorization/capabilities

Employee.Manage authorizes HR lifecycle actions. Security.Manage additionally authorizes either explicit account decision for an active linked User. HRAdmin has no Security.Manage and cannot resolve active account access through this command. SystemAdmin already holds both; no new role, inferred job-title permission or maker-checker workflow is added. Ordinary Employee cannot end/rehire or administer/retain/reactivate an account. Account readiness uses Employee.Read and has no employee self-service exception.

## 15. Audit behavior

Existing append-only SecurityAuditEvents store authenticated ActorUserId and UTC OccurredAtUtc. The new account operations encode EmployeeId and EmploymentRecordId in the existing Operation field and target the retained UserId. The employment operation targets EmploymentRecordId and records linked UserId plus NoLinkedAccount/AlreadyDisabled/Disabled/Retained. Independent status audits now explicitly distinguish AccountEnabled and AccountDisabled. Existing generic authenticated mutation auditing also remains.

No new audit schema or actor FK is introduced. Request body actor IDs are rejected; headers do not supply actor identity. No credentials, hashes, tokens or security stamps are put in responses, operation text or reports.

## 16. Payroll non-impact

Payroll calculation, generation, preview, monetary formulas, statutory/entitlement methods, period/lifecycle protections and manual reconciliation source files are unchanged. A focused fixture generates and pays Payroll through the existing API, then compares header/totals/lines/statutory results/payslip snapshots exactly after offboarding. No final payroll, severance, deduction or period operation is triggered by ending employment.

## 17. Leave non-impact

All Leave sources, lifecycle, entitlement, evidence, sandwich and D11 paid/unpaid calculation implementations remain unchanged. Focused offboarding preserves Approved historical Leave and future Pending Leave, allocations and budgets byte-for-byte. No automatic cancellation, release, unpaid conversion or deletion is introduced. Existing effective employment checks continue governing subsequent operations.

## 18. Attendance non-impact

Raw events, adjudications, review actions and immutable finalized revisions remain unchanged. Source changes can be detected as stale by the existing D9D read-time contract; offboarding does not rewrite, reopen or refinalize history. No disciplinary/KPI/Payroll effect occurs. Future dates without effective employment do not become scheduled working time merely because a calendar assignment remains.

## 19. WorkCalendar behavior

Historical and future assignments remain stored unchanged. Existing date-effective employment resolution prevents future assignments from granting expected work after the inclusive employment end. Retention of those assignments is intentional non-destructive history, not an automatic cleanup policy. No default calendar fallback or Attendance/Payroll integration is introduced.

## 20. API/read-model changes

- Existing end request: required ExpectedEmploymentRecordId; optional DisableLinkedAccount and ExpectedLinkedAccountVersion, conditionally required for an active User.
- New `GET /api/employees/{employeeId}/account-lifecycle`: EmployeeId, AccountLinked, LinkedUserId, AccountStatus, LinkedAccountVersion, CurrentEmploymentRecordId, CurrentEmploymentStatus, HasCurrentEmployment and RequiresOffboardingDecision.
- Existing SecurityUserDto gains additive CurrentEmploymentRecordId, CurrentEmploymentStatus, nullable HasCurrentEmployment and RequiresOffboardingDecision. Existing IsActive, EmployeeId and Version remain.
- Employment change/end/rehire contracts reject unmapped JSON fields. Existing caller tests now obtain the current employment ID before ending.
- Swagger documents the readiness DTO and explicit end preconditions, response codes and access requirements.

The administration version is a concurrency token already exposed by D10, not a security stamp or credential. No EF entities or confidential Identity internals are exposed.

## 21. Concurrency behavior

Employment and linked account writers share Employee-first, then User UPDLOCK order under Serializable. Independent account status/role changes read the immutable linkage before starting the transaction, then lock Employee before User. Provisioning linked accounts also locks Employee first. The authoritative locked User refreshes an already tracked instance, important when the acting SystemAdmin is the linked target.

Expected employment ID and administration version reject stale competing operations. Deadlock victims/EF concurrency and uniqueness conflicts return 409 rather than an unhandled 500. Identity SaveChanges runs inside the outer HR transaction; any later failure rolls back its account and audit writes too.

Focused races cover termination versus disable, enable, rehire and duplicate termination, plus disable versus login/session reuse. Account disable winning first makes the HR command a valid already-disabled end; account enable/version changes require the losing HR decision to be reloaded. Old commands cannot target new employment after rehire.

## 22. Migration/schema changes

None. No entity, DbContext model, configuration, migration or snapshot changes. Existing User active state, versions, stamps, audit table and unique employment/linkage constraints represent this workflow. Migration history remains the same 41 entries. No migration was created or applied.

## 23. Pure/live/security test results

| Verification | Passed assertions |
|---|---:|
| All pure regression suites, no database writes | 955 |
| New D12 pure assertions, included above | 26 |
| D12 live Identity/API/SQL/session/ownership/concurrency matrix | 53 |
| D11 paid/unpaid exhaustion live matrix | 73 |
| D10 standard/advanced/Production-pipeline security | 99 (45 + 28 + 26) |
| All documented anonymous HR operations plus Swagger CSRF | 228 |

D12 covers A-T: no User, already-disabled preservation, unresolved-active 409/no mutation, explicit disable/retain, login/session denial, reactivation with retained identity and no recreated employment, disabled rehire, SQL rejection of a second current record, ordinary Employee and HRAdmin denial, linked SystemAdmin self-offboarding, unchanged historical paid Payroll/payslip/Leave/Attendance, future-end rejection, all requested race categories, forged actors, and D11 regressions. The safe response/Swagger contracts are verified. The retained account can read its own Paid payroll/payslip through existing D10 ownership.

Initial live runner failures were test setup issues: incorrect assumed route names, the generation result's actual `payrollId` field, missing organization-before-generation prerequisite, and audit queries initially restricted to SystemAdmin rather than including HRAdmin. Each failed run restored the exact baseline; the completed suite passes. No production-code regression fix was required after the initial D12 implementation.

Production-pipeline verification used an owned temporary process, loopback forwarding and an explicit connection to local `localhost/SIAMIS` only. It did not access a production database. All temporary credentials/cookies were generated at runtime; none are stored in source or this report.

## 24. Regression results

All fresh authenticated domain suites passed. Original financial assertions remain unchanged.

| Domain suite | Passed assertions |
|---|---:|
| D8B Leave/calendar foundation and D1 employment | 117 |
| D8C requests/calculation/entitlement/read models | 246 |
| D8C ExpectedStatus and lifecycle concurrency | 230 |
| D8D evidence/sandwich lifecycle | 527 |
| D8D capped consumption/concurrency | 392 |
| D9B Attendance evidence | 121 |
| D9C interval calculation and precision | 116 |
| D9D finalization, stale/reopen and concurrency | 174 |
| D9E operational reporting/history | 201 |
| Eight Payroll suites | 834 |
| **Domain total, excluding orchestration checks** | **2,958** |

Payroll breakdown from this run's result manifests: D5A 70; D5C 306; D6B 76; D6C 48; D6D 38; D6E 88; D6E boundaries 128; D7 80. Covers Supplement/ReplaceAssignment, conflict detection, failed-new rollback, failed-regeneration preservation, generated provenance/audit snapshots, manual reconciliation, D3 entitlement/proration, D4 statutory policy/enrollment behavior, SSO/PIT history and payroll/period lifecycle/payslips. No financial expectations were adapted.

The existing D10 runtime adapter supplies real cookies/CSRF and authenticated actors to original pre-authentication tests; it does not bypass application authorization. Original count/schema prerequisites reflect the currently applied schema. Two original employment callers now supply the expected record ID. Twelve outer D12 orchestration checks verified authenticated Attendance, Leave, sandwich and Payroll audits plus exact 85-table cleanup. Nested orchestration checks are not added to the domain total.

## 25. Exact Development cleanup

The final read-only SQL verifier passes all 10 assertions. Its complete 85-table row/timestamp comparison equals the captured pre-D12 baseline. All fixture scripts clean only recorded temporary IDs; the permanent roles and TEST-EMP-001 remain unchanged. Migration history contains the same 41 entries, with no D12 migration. Existing unique current-employment and optional linked-User indexes and NoAction User/Employee FK are retained.

| Development data | Final count |
|---|---:|
| Application tables | 85 |
| Applied migrations | 41 |
| Employees | 1, TEST-EMP-001 inactive |
| EmploymentRecords | 1, original current/open record unchanged |
| Permanent security roles | 5 |
| Master rows (includes PayrollComponents) | 172 |
| PayrollComponents | 17 |
| Users/UserRoles/UserClaims/UserLogins/UserTokens/RoleClaims/SecurityAuditEvents | 0 each |
| WorkCalendars/intervals/overrides/EmployeeWorkCalendarAssignments | 0 each |
| Leave policies/requests/allocations/entitlements/adjustments/evidence/sandwich tables | 0 each |
| Attendance events/review cases/actions/finalized revisions | 0 each |
| Payroll rules/targets/settings/periods/headers/lines/payslips/organization profiles | 0 each |
| Compensations/component assignments/statutory policies/enrollments/tax declarations/results | 0 each |
| Other operational/employee child tables | 0 each |

The complete per-table count manifest is `tests/SIAMIS.Payroll.RegressionTests/bin/d12-final-baseline-results.json` (ignored local verification artifact). Exact comparison includes employee core values, employment, permanent master values and original timestamps, not just counts.

## 26. Complete changed-file list

19 changed/new non-ignored files:

- `D12-REPORT.md`
- `src/SIAMIS.Api/Controllers/EmploymentLifecycleController.cs`
- `src/SIAMIS.Api/Security/HrAuthorizationFilter.cs`
- `src/SIAMIS.Api/Security/SecurityRegistration.cs`
- `src/SIAMIS.Application/Employees/EmploymentLifecycleContracts.cs`
- `src/SIAMIS.Application/Security/SecurityContracts.cs`
- `src/SIAMIS.Infrastructure/Security/AccountLock.cs` (new)
- `src/SIAMIS.Infrastructure/Security/AccountService.cs`
- `src/SIAMIS.Infrastructure/Security/EmployeeAccountLifecycleService.cs` (new)
- `src/SIAMIS.Infrastructure/Services/EmploymentLifecycleService.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D10SecurityTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D12OffboardingTests.cs` (new)
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs`
- `tests/verify_d12_baseline.py` (new)
- `tests/verify_d12_live.py` (new)
- `tests/verify_d12_regressions.py` (new)
- `tests/verify_d12_repository.py` (new)
- `tests/verify_d8b_live.py`
- `tests/verify_d8c_live.py`

Existing D10 pure actor stub implements the added capability query. The two original live employment callers add the required expected employment ID; their original assertions remain. Ignored bin files contain local captures, test result manifests, runners and logs; they are not committed source changes.

## 27. Remaining HR gaps

Future-dated termination scheduling, retention expiry/review, generalized access-by-employment, final-pay/severance, Leave cancellation policy, calendar cleanup and frontend workflows are separate decisions. D12 intentionally uses explicit existing security state; it does not infer that an active account must have current employment or infer access from Employee.IsActive. Permission assignment and exceptional retention remain administrator responsibilities. In-flight requests already validated before disable can finish; subsequent requests are rejected.

## 28. Verification/status table

| Check | Result |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build .\SIAMIS.sln -c Release --no-restore` | PASS, 0 warnings / 0 errors |
| Final Release pure assertions | PASS, 955 |
| D12 live/session/authorization/concurrency | PASS, 53 |
| D11 focused exhaustion | PASS, 73 |
| Employee/calendar/Payroll/Leave/Attendance regressions | PASS, 2,958 domain assertions |
| D10 dedicated security regressions | PASS, 99 |
| Anonymous API and Swagger cookie/CSRF | PASS, 228 |
| Exact Development baseline and migration history | PASS, 10 SQL assertions |
| EF `migrations has-pending-model-changes`, Release/Development | PASS, no changes since last migration |
| `git diff --check` | PASS |
| Targeted secret/whitespace scan | PASS |
| Protected Payroll/Leave/Attendance calculation and lifecycle implementations | Unchanged |
| Migration creation/application | None |
| Commit/push | None; HEAD remains `9463619` |

Reproduction: start the Release API from `src/SIAMIS.Api` with `--environment Development --urls http://localhost:5155`. The D9E query regression additionally uses EF command Information logging captured at the established `tests/SIAMIS.Payroll.RegressionTests/bin/d10-api.log` path. Run fixture suites sequentially: `python tests/verify_d12_live.py`, `python tests/verify_d12_regressions.py`, `python tests/verify_d11_live.py`, then the existing D10 security/advanced/Production-pipeline verification (the last uses a separate owned loopback API and explicit local connection). The baseline verifier consumes the pre-run D12 baseline capture. Ignored JSON manifests/logs preserve the results of this completed run. No test runner bypasses application authentication or authorization.
