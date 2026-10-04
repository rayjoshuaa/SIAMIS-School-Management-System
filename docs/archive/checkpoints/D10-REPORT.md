# D10 — HR Integration, Authentication & RBAC Foundation

Verified locally on 2026-10-04. No commit or push performed.

## 1. Executive summary

D10 adds ASP.NET Core Identity, authenticated API sessions, explicit capabilities, server-derived employee ownership, controlled account provisioning and immutable actor audit records. The approved independent PayrollAdmin boundary is implemented. Existing Payroll, Leave, employment and Attendance calculation/lifecycle services are unchanged.

One focused migration was applied only to Development `localhost/SIAMIS`. All original 77 tables retain their exact pre-migration rows and timestamps. The database now has 85 application tables, five permanent roles and no remaining temporary accounts or workflow fixtures.

Verification: 892 pure assertions, 2,956 existing domain live assertions, 99 dedicated live security assertions, 226 anonymous operation checks plus Swagger interceptor verification, and 10 final SQL assertions. Orchestration checks are reported separately rather than inflating domain totals. HR Backend V1 is not declared fully production-ready: remaining boundaries appear below.

## 2. Repository/database identity state inspected

Starting commit: `9502327` (`feat(hr): add operational attendance reporting and history`). The initial working tree was clean. There was no existing User/Identity model, authentication middleware, authorization policy, CORS or trusted proxy configuration. Existing actor fields were nullable historical GUIDs. Sensitive D8/D9 controller actions were Development-only.

Initial local database: SQL Server 2025, `localhost`, database `SIAMIS`, Windows integrated authentication; 77 application tables, 39 migrations, 172 master rows, one inactive TEST-EMP-001 and one EmploymentRecord. Original application rows were captured before the migration and compared after cleanup. No remote/production database was used.

## 3. Authentication architecture decision

Use first-party ASP.NET Core Identity with GUID identifiers and its EF Core 10.0.12 stores in the existing SIAMISDbContext. Application contracts remain HTTP-independent. API owns claims, cookies, policies and HTTP filters; Infrastructure owns account persistence and framework Identity operations. No second identity system, custom password cryptography, external identity provider or generic permission engine was introduced.

Framework references: [Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0), [Identity model](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0).

## 4. Authentication transport/security decision

Same-origin, first-party web login uses framework protected cookies. `SIAMIS.Session` is HttpOnly, SameSite=Strict, Secure outside Development, eight-hour absolute expiry, without sliding renewal. Every authenticated request rechecks current account status, lockout and security stamp and rebuilds authoritative roles/linkage. Role/status/password changes invalidate existing sessions. Logout clears the current browser cookie; it does not promise global logout of copied cookies or all devices.

Development allows HTTP on localhost. Production rejects HTTP unless an explicitly trusted reverse proxy establishes HTTPS. No JWT, refresh-token system or mobile-specific transport was added. A future official mobile client can use a separately reviewed standard transport against the same Identity store and capability model.

## 5. User ↔ Employee relationship

`ApplicationUser : IdentityUser<Guid>` maps to `Users`. Nullable EmployeeId references Employees with NoAction. A filtered unique EmployeeId index permits at most one linked identity per employee, including disabled accounts. System/admin identities may have no Employee link. Assigning Employee role requires an existing link; Guid.Empty and missing employees are rejected.

Authenticated UserId comes from framework NameIdentifier. EmployeeId claims are generated from the stored User linkage and refreshed from the database. Headers, URLs and request bodies cannot establish ownership. There is no public relinking or account deletion endpoint. Account disabling cannot delete HR or audit history.

## 6. Account lifecycle/status

IsActive controls login independently of Employee.IsActive/employment termination. Disabled and locked accounts fail login and existing authenticated requests. Temporary accounts require password change before any HR capability is granted. Account administration uses a public opaque AdministrationVersion token, separate from private framework security/concurrency stamps; stale writes return 409.

Automatic termination-to-login disabling remains an unapproved business policy. Administrators must explicitly disable accounts when required. An inactive employee retaining an active login remains possible by design and was tested.

## 7. Roles

| Role | Intended scope |
|---|---|
| SystemAdmin | Explicit security, employee, Leave, Attendance, reporting and Payroll capabilities |
| HRAdmin | Employee/employment, Leave, Attendance and reporting; no Payroll by default |
| PayrollAdmin | Payroll read/manage, independent of HRAdmin |
| Management | Minimized staff/Leave overview and factual Attendance reporting |
| Employee | Own self-service resources only |

All known roles receive read-only master-data lookup capability. Roles are security assignments, never inferred from department, designation, job title or employment position. HRAdmin and PayrollAdmin can be assigned together. No additional approval stage, maker-checker rule or separation-of-duty workflow was added.

## 8. Permissions/policies

Capabilities: Security.Manage; Employee.Read/Manage; Leave.Read/Manage/Review/Evidence; Attendance.Read/Manage/Finalize; Payroll.Read/Manage; Reporting.Read; SelfService; MasterData.Read.

A central API authorization filter maps current controllers/actions to capabilities and checks policies through IAuthorizationService. Explicit self-resource checks supplement administrator capabilities. Unmapped controllers deny access. Fallback authorization requires authenticated requests; only explicitly anonymous health, CSRF and login contracts bypass it. Temporary-password accounts have no HR capabilities. Policy names, rather than scattered role checks, control protected operations.

## 9. Self-service authorization

`GET /api/self/profile` resolves the stored employee linkage and returns a minimized profile. `GET /api/self/payrolls` filters own Approved/Paid headers in SQL before pagination. Client employeeId query values are ignored as identity evidence.

Employee users may read their own Attendance history/summary and Leave requests/balances, submit their own Leave and use the existing own cancellation contract. They cannot access another employee by changing route IDs, list organization payroll, approve/reject Leave, finalize Attendance, manage entitlements or provision accounts. Detailed payroll/payslip/PIT reads are restricted to server-verified own Approved/Paid payroll IDs. Draft/Calculated/Cancelled records are not self-service payroll views.

The minimized profile is a foundation; a richer own employment/profile DTO is deferred to a later frontend contract.

## 10. Administrative authorization

Only Security.Manage permits account provisioning, status and role changes. HR capabilities permit existing HR workflows. Payroll capabilities independently permit existing payroll workflows. Only SystemAdmin receives Security.Manage in the initial mapping. Management and Employee cannot obtain administration through request fields or headers.

List/read projections use DTOs and bounded pagination; controllers do not access DbContext. User role/status mutation remains serialized on the User row and uses AdministrationVersion. One of two concurrent changes with the same token succeeds; the other returns 409.

## 11. Attendance authorization

Organization daily/today reports require Reporting.Read. The review queue requires Attendance.Read. History/summary allow Reporting.Read or a matching authenticated self link. Raw evidence/calculation/review reads require Attendance.Read. Manual evidence, correction and adjudication require Attendance.Manage; absence confirmation, finalization and reopening require Attendance.Finalize.

Employees and Management cannot mutate Attendance. Existing Employee-first locks, exact tick precision, five-minute grace, source fingerprints, review tokens, immutable revisions and explicit reopen semantics remain unchanged. No automatic payroll/KPI/disciplinary consequences were introduced.

## 12. Leave authorization

HR Leave read/manage/review capabilities protect existing APIs. Entitlement, policy and calendar administration require Leave.Manage. An employee's own submission/cancellation uses the current D8 contract; own approval/rejection is denied, including a linked administrator approving their own request. Confidential external receipt/evidence operations and sandwich review require Leave.Evidence.

Ordinary self-service Leave detail/list responses hide confidential Evidence receipts. Management uses a separate status projection without reason, evidence or medical details. Required-document policy metadata remains factual. Existing balances, minute precision, capped sandwich consumption, cancellation and insufficient-entitlement behavior are unchanged.

## 13. Payroll authorization

The approved contract is implemented: SystemAdmin explicitly receives Payroll read/manage; PayrollAdmin independently controls payroll configuration, generation, adjustments, approval, paid/cancel transitions and period operations. HRAdmin and Management receive no detailed Payroll access by default.

Employee access requires authenticated User→Employee ownership and Approved/Paid status. It provides no Payroll mutation or organization-wide access. Compensation, tax/statutory configuration, rules, settings, assignments, employer profile and payroll administration require explicit Payroll policies. The Development-only evaluator diagnostic remains restricted in Production.

No financial formula, D3 entitlement rule, SSO/PIT calculator, lifecycle transition, approval workflow, Attendance deduction or Leave deduction was changed.

## 14. Employee-data authorization

Core administrative employee APIs require Employee.Read/Manage. Management gets a separate staff overview containing only ID, number, first/last name and active state. Employee self profile uses that same minimized shape.

Inspection found current salary nested in EmployeeDetailDto. An API result filter now returns CurrentCompensation=null unless Payroll.Read is granted, covering GET and create/update detail responses. The existing nullable response field is retained; entities/schema and employee service behavior were not redesigned. Live tests confirm HRAdmin cannot read salary while explicitly Payroll-authorized SystemAdmin can.

Document metadata remains HR-administrative only. Binary document storage/delivery/access audit is not claimed as implemented.

## 15. Actor attribution

ICurrentActor supplies authenticated UserId, linked EmployeeId and endpoint operation. New AttendanceEvent.ActorId, review case/action/revision ActorUserId, Leave evidence event ActorId and sandwich event ActorId are stamped server-side. New review action Origin=Authenticated. Existing immutable actorless history is untouched.

Existing core entities without actor columns receive a minimal atomic SecurityAuditEvent on the same SaveChanges/transaction. This covers Employee/employment administration, Leave approval/rejection/cancellation and payroll administrative writes without adding actor columns to every table. Failed domain operations roll back their audits with their writes.

Forged actor fields are rejected by strict existing contracts; forged identity headers are ignored. Actor identity is a User ID, not an Employee ID. Historical GUID attribution is snapshot data without an identity deletion cascade.

## 16. Development-only guards changed/retained

| Guard | D10 outcome | Safety evidence |
|---|---|---|
| AttendanceFoundation manual event | Removed after authentication/capability/actor checks | Existing validation/idempotency plus authenticated D9B and Production checks |
| AttendanceReview correction/adjudication | Removed | Existing reason/source/version/ownership protections and concurrency suite; authenticated Production actors |
| AttendanceReview absence confirmation/finalize/reopen | Removed | Existing immutable revisions, locks and source versions; authenticated D9D races and Production revision 1→2 verification |
| LeaveEvidenceSandwich receipt/read/accept/reject/cases/review | Removed | Leave.Evidence, existing immutable receipt/case history and concurrency protections, D8D regressions and Production HR actors |
| PayrollRuleEvaluation diagnostic | Retained | Diagnostic Development surface; no new production diagnostic requirement |
| Swagger | Development only retained | Production authorized request returns 404 |
| Retired legacy Attendance writes | Still retired | Existing 410 behavior for authorized valid requests; no restoration of legacy mutations |

Guard removal followed authenticated domain tests. Production verification uses the Production pipeline with an explicitly trusted loopback proxy simulation against local Development data; it does not certify deployed TLS infrastructure.

## 17. User administration APIs

| Method | Route | Purpose |
|---|---|---|
| GET | /api/auth/csrf | Framework antiforgery request token; anonymous, no-store |
| POST | /api/auth/login | Login, generic 401, rate limit, CSRF |
| POST | /api/auth/logout | Clear browser session, audit |
| GET | /api/auth/me | Safe identity/roles/capabilities, no-store |
| POST | /api/auth/change-password | Current-password verification and temporary-password activation |
| GET | /api/admin/users | Bounded safe account list |
| GET | /api/admin/users/{id} | Safe account detail |
| POST | /api/admin/users | Temporary-password account provisioning |
| PATCH | /api/admin/users/{id}/status | Enable/disable using latest Version |
| PUT | /api/admin/users/{id}/roles | Explicit role replacement using latest Version |
| GET | /api/hr/staff-overview | Minimized Management/HR overview |
| GET | /api/hr/leave-status | Minimized status overview |
| GET | /api/self/profile | Server-linked own profile |
| GET | /api/self/payrolls | Server-linked own Approved/Paid list |

No public registration, direct permission-grant endpoint, hard account deletion or caller-selected actor/employee identity exists. Duplicate username/provided email/linkage returns 409; invalid roles/linkage/password returns 400 or missing employee 404.

## 18. Password/bootstrap/security design

Framework salted hashing; minimum 12-character passwords, no arbitrary complexity composition rules. Five failed attempts lock an account for 15 minutes. Login has a per-IP 20-per-minute fixed-window limit and returns 429 when exhausted. Invalid, missing, disabled and locked accounts use the same 401 response.

Default Identity reset-token providers are available with 15-minute lifespan. Reset delivery, recovery UI and mail infrastructure are deferred; there is no public reset-delivery endpoint.

First admin creation is an explicit one-time command. Supply Bootstrap__UserName and Bootstrap__Password through secure environment/secrets, then run from src/SIAMIS.Api:

```powershell
dotnet bin/Release/net10.0/SIAMIS.Api.dll --bootstrap-admin true --environment Development
```

For a real deployment use its explicit environment and secured connection configuration. Never place a production password in command arguments or source. Bootstrap refuses when any SystemAdmin assignment exists and runs transactionally. It creates a temporary-password account; login then change password before HR access. Clear bootstrap environment variables/secrets afterward. No administrator credential remains from these tests; the school must provision its first real admin deliberately.

## 19. Secret management

API UserSecretsId=`SIAMIS.Api.Development` enables Development user-secrets. Production configuration comes from environment/deployment secrets. No credentials are seeded in migrations or appsettings. Tests use random passwords in memory/environment, never committed artifacts.

A targeted scan of all changed source/report files checks private-key blocks, common inline password/API-key patterns and common provider token formats; it is not represented as an exhaustive secret-detection product. No D10 credential was found. The synthetic pure-test password string is hash verification input, not an account credential. Logs and audit records exclude passwords, cookies, reset tokens and confidential payloads.

## 20. CORS/CSRF/HTTPS considerations

Future first-party React should share the API origin behind a reverse proxy. Before every unsafe request, fetch `/api/auth/csrf` and send its token as X-CSRF-TOKEN with session credentials. Refresh after login/logout/password identity changes. CSRF protects login as well as authenticated writes; framework antiforgery is used without MVC view services.

Security:AllowedOrigins is an explicit environment-configured origin array. Unconfigured origins receive no credentialed CORS permission. Production origins require HTTPS; wildcard origin + credentials is never enabled. Strict same-site cookies intentionally do not promise arbitrary cross-site web deployment.

Security:KnownProxies explicitly configures trusted proxy IPs; forwarded headers are disabled when empty and limited to one hop. Production requires HTTPS and sends HSTS. Restrict direct backend ingress, terminate TLS correctly and forward only controlled headers. Production tests simulate HTTPS termination, not a real public certificate or deployed proxy.

Data Protection application name is SIAMIS. Optional Security:DataProtectionKeyDirectory supports a persistent key ring. Deployment must secure, persist, protect at rest and back up the key ring; merely setting a directory does not configure certificate encryption. Native Windows default protection or an approved deployment encryption mechanism must be validated. No secrets/certificates/proxy or backup infrastructure was installed.

Framework antiforgery reference: [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## 21. Audit behavior

SecurityAuditEvents is a small append-only table: GUID ID, nullable historical ActorUserId, operation, resource type/ID and UTC timestamp. Login success/failure, logout, bootstrap, provisioning, status/role changes and password change are recorded. Missing/invalid account login does not store the submitted username or password.

Authenticated HR changes produce atomic resource audit entries, without before/after confidential payloads. Existing detailed immutable workflow events remain authoritative for reasons/outcomes/snapshots. Security audit entries cannot be modified/deleted through tracked application writes. Direct SQL fixture cleanup is strictly a local test operation, not an exposed API.

No generic audit platform, event bus, audit UI, retention scheduler or account deletion cascade was added. Failed pre-controller authorization is not promised as a durable audit event. Deployment access logs/monitoring and retention are future operational work.

## 22. Database/migration changes

Applied migration: **20261004053737_AddHrIdentityAndSecurityFoundation**. Local target only: localhost/SIAMIS, integrated authentication, encrypted client connection with Development certificate trust. Forty migrations are now recorded.

New tables: Users, Roles, UserRoles, UserClaims, UserLogins, UserTokens, RoleClaims, SecurityAuditEvents. Unique normalized username, unique provided normalized email, filtered unique EmployeeId, NoAction User→Employee FK; framework Identity child cascades are confined to security records. Login/token composite string key fields are bounded to 128 characters. Security timestamps use datetime2(7).

Two existing checks changed only to admit authenticated historical actors: CK_AttendanceEvent_SourceFields and CK_AttendanceReviewAction_Shape. Both are enabled/trusted in SQL. Legacy actorless records remain valid; new review origin is Authenticated with non-null actor. Existing HR columns and rows were not rewritten. No calculation/schema feature beyond D10 was introduced.

## 23. Permanent security seed data

Five deterministic role records remain:

| Role | GUID |
|---|---|
| SystemAdmin | d1000000-0000-0000-0000-000000000001 |
| HRAdmin | d1000000-0000-0000-0000-000000000002 |
| PayrollAdmin | d1000000-0000-0000-0000-000000000003 |
| Management | d1000000-0000-0000-0000-000000000004 |
| Employee | d1000000-0000-0000-0000-000000000005 |

Capabilities are the small reviewed application policy contract, not user-editable arbitrary grant rows. No users, credentials, role assignments or permission claims are seeded. Permanent roles are intentional foundation data, not cleanup residue.

## 24. Cross-domain HR integration tests

Real framework cookies/CSRF were used to rerun existing Employee/employment, calendar, Leave, sandwich, Attendance and Payroll fixtures. Flows cover effective employment/calendar resolution, Leave entitlement/request/approval/cancellation, Attendance coverage/staleness/reopen, frozen revisions/reporting and payroll generation/lifecycle.

Production-mode focused verification adds HR-owned Leave evidence/sandwich review, correction/adjudication/finalization/reopen and actor authority. Separate own payroll tests generate two temporary employees' payslips and verify own Approved/Paid access and cross-employee denial. Exact database cleanup follows every suite.

The pre-auth regression adapter supplies real signed sessions, accounts for the new eight tables, replaces obsolete null-actor expectations with authenticated IDs and adds 401/403 Swagger responses. Financial/Leave/Attendance expected amounts are not weakened. D9E retained constant query counts across 1/11/220 employee/date cohorts (22 commands including five fixed Identity validation queries).

## 25. IDOR/security tests

| Suite | Recorded checks | Result |
|---|---:|---|
| Focused login/roles/own-resource/administration/Swagger | 45 | Passed |
| Payroll ownership, salary privacy, lockout/rate, role concurrency, forged actor, SQL uniqueness | 28 | Passed |
| Production cookies/HTTPS/CSRF/CORS, actor workflows, confidential evidence and IDOR | 26 | Passed |
| Every documented HR operation anonymous authorization | 226 | All 401 |
| Served Swagger initializer cookie/CSRF integration | 1 | Passed |
| Final SQL schema/constraint/baseline | 10 | Passed |

No temporary administrator, account, payroll, employee, calendar, Leave or Attendance record remains. Production secure-cookie behavior was verified through an explicit trusted-loopback proxy simulation. No public deployment penetration test or browser UI automation is claimed.

## 26. Regression results

| Existing suite | Assertions |
|---|---:|
| D9B evidence/legacy retirement | 121 |
| D9C precision/calculation | 116 |
| D9D review/finalization/concurrency | 174 |
| D9E operational reporting/history | 201 |
| D8B/D1 calendars/employment/foundation | 117 |
| D8C Leave lifecycle | 246 |
| D8C ExpectedStatus/concurrency | 230 |
| D8D evidence/sandwich | 523 |
| D8D capped sandwich focus | 391 |
| D5A Payroll classification/generation/manual/lifecycle | 70 |
| D5C Section 33 integration | 306 |
| D6B | 76 |
| D6C | 48 |
| D6D PIT calculator | 38 |
| D6E PIT persistence/integration | 88 |
| D6E boundaries | 128 |
| D7 operations/payslip | 83 |
| **Domain live total** | **2,956** |

All passed. Pure console suite: **892** assertions (852 prior + 40 D10). Supplement/ReplaceAssignment, generation failure rollback, regeneration preservation, manual reconciliation, source/audit snapshots, payroll-period lifecycle and statutory calculations retain prior expectations. Existing calculation/lifecycle service source files are unchanged.

Initial verification failures were obsolete Swagger/query-log/actor test assumptions, cookie attribute case normalization and incomplete focused payroll fixtures (required SSO opt-out and pre-generation organization identity). Those fixtures/assertions were corrected; no financial behavior was changed to make tests pass. The API antiforgery implementation was corrected to use IAntiforgery directly, avoiding an MVC view-service dependency. Final runs passed and each failed attempt also cleaned its fixtures.

## 27. Exact Development cleanup/baseline

Every original table's rows and timestamps match the captured pre-D10 baseline, not only counts. Final application table count=85; migrations=40.

| Security table | Final rows |
|---|---:|
| Users | 0 |
| Roles | 5 |
| UserRoles | 0 |
| UserClaims | 0 |
| UserLogins | 0 |
| UserTokens | 0 |
| RoleClaims | 0 |
| SecurityAuditEvents | 0 |

Employees=1; EmploymentRecords=1; TEST-EMP-001 ID=433f2c1a-6222-494f-a64f-cd0c31126dc4, inactive, PreferredName=Test Updated and original timestamps/context unchanged. Original master rows=172, including PayrollComponents=17; existing earning classifications were not changed. PayrollRules/targets/settings/periods, compensations/assignments, payroll headers/lines/results/payslips, Leave/calendars/entitlements/evidence/sandwich and Attendance events/cases/actions/revisions are all empty. All other original non-master child/workflow tables are empty.

Full table counts and actual SQL metadata are recorded in the ignored local verification artifact tests/SIAMIS.Payroll.RegressionTests/bin/d10-final-baseline-results.json. Test API processes were stopped after verification; no fixture login sessions remain as usable accounts.

## 28. HR Backend V1 readiness matrix

These classifications concern backend capability, not a certification that a production school deployment is ready.

| Area | Classification | Remaining boundary |
|---|---|---|
| Employee | COMPLETE FOR V1 administrative foundation | Frontend/self profile enrichment deferred |
| Employment | COMPLETE FOR V1 foundation | Account offboarding remains explicit |
| WorkCalendar | COMPLETE FOR V1 foundation | Explicit assignments; no automatic default history |
| Payroll | PARTIALLY COMPLETE | Existing reviewed engines/lifecycles protected; real policy/configuration and deployment acceptance needed |
| Leave | PARTIALLY COMPLETE | Current request engine intact; exhaustion policy unresolved |
| Leave entitlement | COMPLETE FOR V1 foundation | Minute precision, calendar-year adjustments retained |
| Leave evidence | PARTIALLY COMPLETE | External receipt review only; binary handling deferred |
| Sandwich Leave | COMPLETE FOR V1 approved contract | No payroll debt/consequence inference |
| Attendance evidence | PARTIALLY COMPLETE | Authorized manual evidence; device ingestion deferred |
| Attendance calculation | COMPLETE FOR V1 foundation | Exact ticks, factual grace and Leave coverage |
| Attendance finalization | COMPLETE FOR V1 foundation | Authenticated review/reopen plus immutable revisions |
| Attendance reporting | COMPLETE FOR V1 foundation | Bounded factual live/official/stale reporting |
| Authentication | PARTIALLY COMPLETE | Web Identity foundation complete; production key/TLS/recovery operations pending |
| Authorization/RBAC | COMPLETE FOR V1 approved boundary | Future granular grants/mobile scopes separately reviewed |
| Actor auditing | COMPLETE FOR V1 minimum | Production retention/monitoring and confidential file read audit deferred |
| File/document security | DEFERRED | Binary storage, validation, scanning, delivery, retention/read audit |
| Notifications | DEFERRED | Mail/notification infrastructure absent |
| Device/facial-recognition integration | DEFERRED | Device trust, ingestion and operational decisions |
| Payroll/Attendance integration | BLOCKED BY BUSINESS DECISION | No automatic financial/disciplinary consequences |
| Paid Leave exhaustion/unpaid behavior | BLOCKED BY BUSINESS DECISION | No automatic split/conversion |
| Production deployment/security | PARTIALLY COMPLETE | Deploy/restrict proxy, TLS, secrets, protected key ring, backups/restore, monitoring |

## 29. Known deferred decisions

Paid Leave exhaustion still rejects insufficient normal entitlement; automatic unpaid conversion/splitting is not introduced. Unabsorbed sandwich debit remains factual, not payroll debt. Attendance absence/lateness, Leave cancellation and stale/reopened Attendance do not create Payroll/KPI/disciplinary effects.

Deferred: medical/document binary storage/delivery, scanning, confidential file read auditing and retention; device/facial recognition; notifications/reset delivery; production backups/restore and hardening; automatic employee offboarding/account disable policy; eventual mobile transport. None was silently implemented.

## 30. Recommended next checkpoints

1. Review/accept D10 policies and provision the first real administrator securely.
2. Validate production same-origin proxy/TLS/secrets/Data Protection/backup and recovery operations.
3. Agree confidential file storage/delivery/access auditing and retention contracts.
4. Resolve Leave exhaustion/offboarding business policies independently.
5. Begin frontend login/current-user/capability handling against the protected API, then HR screens.
6. Review device integration and any Attendance/Leave financial consequences as separate checkpoints.

No frontend or downstream integration was built in D10.

## 31. Complete changed-file list

The complete repository-relative list is inserted below from read-only git status/diff inspection. Generated bin/log/fixture artifacts are ignored and are not delivered source changes.

- `D10-REPORT.md`
- `src/SIAMIS.Api/Controllers/AdminUsersController.cs`
- `src/SIAMIS.Api/Controllers/AttendanceFoundationController.cs`
- `src/SIAMIS.Api/Controllers/AttendanceReviewController.cs`
- `src/SIAMIS.Api/Controllers/AuthController.cs`
- `src/SIAMIS.Api/Controllers/EmployeeAttendanceController.cs`
- `src/SIAMIS.Api/Controllers/HrOverviewController.cs`
- `src/SIAMIS.Api/Controllers/LeaveEvidenceSandwichController.cs`
- `src/SIAMIS.Api/Controllers/OrganizationProfileController.cs`
- `src/SIAMIS.Api/Controllers/SelfServiceController.cs`
- `src/SIAMIS.Api/Program.cs`
- `src/SIAMIS.Api/SIAMIS.Api.csproj`
- `src/SIAMIS.Api/Security/AdminBootstrap.cs`
- `src/SIAMIS.Api/Security/ApiCsrfFilter.cs`
- `src/SIAMIS.Api/Security/DeploymentSecurity.cs`
- `src/SIAMIS.Api/Security/HrAuthorizationFilter.cs`
- `src/SIAMIS.Api/Security/IdentityPrincipalFactory.cs`
- `src/SIAMIS.Api/Security/LeaveEvidenceResponseFilter.cs`
- `src/SIAMIS.Api/Security/PayrollResponseFilter.cs`
- `src/SIAMIS.Api/Security/SecurityDocumentationFilter.cs`
- `src/SIAMIS.Api/Security/SecurityRegistration.cs`
- `src/SIAMIS.Application/Employees/EmployeeContracts.cs`
- `src/SIAMIS.Application/Security/SecurityContracts.cs`
- `src/SIAMIS.Infrastructure/Configurations/AttendanceEventConfiguration.cs`
- `src/SIAMIS.Infrastructure/Configurations/AttendanceReviewConfiguration.cs`
- `src/SIAMIS.Infrastructure/Configurations/SecurityConfiguration.cs`
- `src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004053737_AddHrIdentityAndSecurityFoundation.Designer.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004053737_AddHrIdentityAndSecurityFoundation.cs`
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`
- `src/SIAMIS.Infrastructure/SIAMIS.Infrastructure.csproj`
- `src/SIAMIS.Infrastructure/Security/AccountService.cs`
- `src/SIAMIS.Infrastructure/Security/ApplicationUser.cs`
- `src/SIAMIS.Infrastructure/Security/HrSecurityReadService.cs`
- `src/SIAMIS.Infrastructure/Security/ResourceAccessService.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D10SecurityTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs`
- `tests/SIAMIS.Payroll.RegressionTests/SIAMIS.Payroll.RegressionTests.csproj`
- `tests/d10_regression_hook.py`
- `tests/verify_d10_advanced.py`
- `tests/verify_d10_baseline.py`
- `tests/verify_d10_production.py`
- `tests/verify_d10_regressions.py`
- `tests/verify_d10_repository.py`
- `tests/verify_d10_route_security.py`
- `tests/verify_d10_security.py`

## 32. Verification/status table

| Check | Result |
|---|---|
| Local focused Identity migration | Applied successfully only to localhost/SIAMIS |
| Actual SQL new tables/indexes/FKs/checks | Verified |
| dotnet restore | Succeeded |
| Release build | Succeeded; 0 warnings, 0 errors |
| Pure assertions | 892 passed |
| Existing domain live assertions | 2,956 passed |
| Dedicated security live assertions | 99 passed |
| Anonymous API operations / Swagger | 226 operations + 1 initializer check passed |
| Final SQL assertions / exact baseline | 10 passed |
| EF pending-model-changes | None |
| git diff --check and new-file whitespace scan | Passed |
| Targeted changed-source secret scan | Passed |
| Existing calculation/lifecycle service changes | None |
| Temporary fixture cleanup | Exact baseline restored |
| Commit / push | Neither performed |

Evidence is in ignored local tests/SIAMIS.Payroll.RegressionTests/bin/d10-*-results.json and corresponding logs. The report records completed results; retaining ignored evidence is not required to build or run the application.
