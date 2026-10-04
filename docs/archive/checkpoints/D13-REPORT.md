# D13 — Account Provisioning, Credentials & Recovery

## 1. Executive summary

Completed the approved trusted recovery-contact contract using existing ASP.NET Core Identity. Staff provisioning is passwordless; activation establishes password and email trust; recovery requires an active, established credential and confirmed User email. Last usable SystemAdmin protection includes D12 offboarding and concurrent administrative writers.

All verification passed. No schema change or migration. Exact Development baseline restored. Owned APIs stopped. No commit or push.

## 2. Repository/database state inspected

Baseline HEAD: c61b58a (completed D12). Inspected Identity/configuration, provisioning/bootstrap, sessions, capabilities, actor attribution, CSRF/Swagger/deployment, AccountLock, D12 integration, tests and actual SQL.

Only localhost/SIAMIS, Windows integrated authentication, TrustServerCertificate=true was used. SQL instance reports Ray. Baseline: 85 application tables; 41 migrations, latest 20261004072245_AddLeavePaidUnpaidAllocation; 172 master rows including 17 PayrollComponents; five permanent roles; one inactive TEST-EMP-001 and its original EmploymentRecord; no Users, security audit or operational workflow rows.

## 3. Identity/account model

ApplicationUser remains IdentityUser<Guid>. Existing nullable PasswordHash, EmailConfirmed, IsActive, RequiresPasswordChange, security stamps and AdministrationVersion represent pending credentials. Shared Identity/HR DbContext and transaction retained. No new account enum, parallel identity, cryptography or session store.

## 4. Provisioning ownership

POST /api/admin/users requires Security.Manage. Normal request: username, required intended email, optional existing Employee linkage, explicit established roles. TemporaryPassword removed; strict JSON rejects password/hash/stamp/confirmation/actor/claim injection. UserManager.CreateAsync(user) creates no password. Unconfigured/failed delivery fails closed without a partial account, roles or audit.

Existing one-time CLI bootstrap remains the temporary-credential exception. It never automatically confirms email. Bootstrap credentials must still be changed before normal capabilities become available.

## 5. Employee linkage

Existing optional filtered-unique Employee linkage and NoAction FK remain. Employee role requires linkage. Provisioning locks the existing Employee, never creates Employee automatically or silently replaces linkage. Duplicate and concurrent same-Employee provisioning are rejected consistently. Rehire retains identity.

## 6. Public registration boundary

No public registration endpoint. Anonymous callers cannot provision accounts, select roles or activate without a valid server-issued token. Employee creation does not create User.

## 7. Initial credential/activation lifecycle

Pending: PasswordHash=null, EmailConfirmed=false, RequiresPasswordChange=true; IsActive remains administrative state. Identity provider uses separate activation purpose SIAMIS.InitialActivation.v1. Successful completion adds the framework-hashed password, confirms possession of intended email, clears required password change, advances Version and rotates security stamp. Tokens then become stale. No new persistent activation state.

## 8. Password policy

Unchanged Identity minimum length 12; no forced digit/case/punctuation composition. Existing request maximum 256. Framework validation/hashing; no plaintext password persistence or retrieval.

## 9. Change-password behavior

Existing authenticated own-password route uses principal identity and current/new password. Employee-first transaction refreshes authoritative User. Wrong current password, forged target or disabled/locked state fails safely. Successful change advances stamp/version, invalidates other sessions and refreshes caller cookie. No client-selected target.

## 10. Forgot-password enumeration resistance

POST /api/auth/forgot-password accepts recovery email, not username or Employee/profile email. Issuance requires active User, established password, provided User email and EmailConfirmed=true. Eligible, unknown, unconfirmed, pending, disabled and no-email inputs share the identical generic 200 message. Response contains no user/linkage/role/state/eligibility metadata. Anonymous initiation does not create identifying audit entries. Unconfigured delivery also returns the generic message. This is response-based enumeration resistance, not a constant-time guarantee.

## 11. Reset-password behavior

POST /api/auth/reset-password uses Identity ResetPasswordAsync with UserId, token and new password. Locked one-time consumption rotates stamp/version. Retains identity, roles, email, linkage, employment and history. Old sessions fail on their next request. Malformed/expired/wrong-user/wrong-purpose/reused tokens return generic 400. No automatic administrative enablement.

## 12. Disabled-account behavior

Disabled users cannot activate/reset or receive usable recovery issuance. Disable stales earlier tokens; explicit re-enable does not revive them. Reset cannot supersede D12, rehire Employee or change employment. Reactivation remains explicit versioned Security.Manage administration.

## 13. Lockout behavior

Unchanged five failed attempts and 15-minute Identity temporary lockout. Lockout does not change IsActive. Reset preserves the lockout deadline; standard expiry restores authentication eligibility. Live expiry verification aged only a recorded temporary fixture's deadline; production policy was never shortened. No new unlock shortcut/table.

## 14. Administrator-triggered recovery

POST /api/admin/users/{id}/issue-credentials requires Security.Manage and latest Version. Pending users receive activation reissue; confirmed active established users receive reset initiation. Activation reissue rotates stamp and stales previous invitations. Reset issuance itself preserves sessions; consumption revokes them. Response contains Purpose only. Ineligible/stale state 409; unavailable delivery 503. Administrator cannot retrieve password or select permanent password.

## 15. Role administration

Existing SystemAdmin, HRAdmin, PayrollAdmin, Management and Employee capability model unchanged. No position-derived privileges/new roles. Versioned role replacement rotates stamp; initial assignment and role deltas are audited. Ordinary users cannot self-promote. Fresh and stale-session permission behavior verified.

## 16. Last-SystemAdmin protection

Before disable, SystemAdmin removal/demotion, or D12 offboarding with disable, another usable SystemAdmin must exist: active, established password, no required password change, not currently locked. EmailConfirmed is deliberately not required for bootstrap usability. Pending, disabled, temporary-password-required and locked role rows do not substitute for access.

Serializable writers lock Employee, then shared SystemAdmin role row WITH (UPDLOCK), then fresh User. Stable 409 code: last_usable_system_admin_required. Concurrent final-two operations cannot both succeed. Self-management remains allowed when another usable administrator exists. No hidden superuser/deletion endpoint. Temporary authentication lockout remains an independent security protection; the guard is not a guarantee against all outages/lost credentials.

## 17. Session/security-stamp behavior

Existing every-request principal validation rejects missing/disabled/locked/stamp-mismatched User and refreshes roles/linkage. Reset/change, disable, role changes and activation reissue invalidate relevant old state. Enable does not revive pre-disable cookies. In-flight requests are bounded by their authorization point; subsequent requests cannot retain removed permission. No second session store.

## 18. Rehire/offboarding compatibility

D12 expected employment ID, explicit disable/retain decision, expected account Version, Security.Manage boundary and Employee-first ordering retained. Last-admin check occurs before HR/account mutation in the shared transaction. Rehire does not enable User, reset password, assign roles or create another identity. Provision-versus-offboarding serializes and preserves the explicit active-account decision.

## 19. Audit behavior

Existing append-only SecurityAuditEvents and UTC timestamps retained. Events cover AccountCreated, ActivationInitiated, ActivationReissued, AccountActivated, PasswordChanged, PasswordRecoveryInitiated, PasswordResetCompleted, RoleAssigned/RoleRemoved and existing enable/disable/offboarding operations. Authenticated actor comes from principal; anonymous completion actor is null with target snapshot ID.

No passwords/hashes/stamps/cookies/tokens in audit. Existing privileged validation rejection conventions return safe failure without separately committing rejected-request audit; no general rejection-audit subsystem was added. Failed transactional mutations remain rolled back.

## 20. Token security/lifetime

Existing Identity DataProtection provider and 15-minute lifetime; framework user/purpose/stamp scoping. Pure tests verify expiry separately for activation and reset with an isolated zero-lifetime provider, without changing runtime lifetime or waiting 15 minutes. Live tests cover valid completion, malformed/wrong-user/wrong-purpose tokens, reuse, reissue staleness, disable/re-enable staleness and concurrent consumption. No custom cryptography, token table, token-bearing URL or real token in Swagger examples.

## 21. Email/delivery boundary

Narrow ICredentialDelivery interface; no production email provider selected. Unconfigured privileged creation/issuance returns 503 without partial persistence. Anonymous recovery remains generic. Delivery failure does not disclose recipient/token.

Test handoff defaults OFF. Explicit --Security:EnableDevelopmentCredentialDelivery true is allowed only with --environment Development. Any non-Development enable attempt fails startup. Its Security.Manage/CSRF-protected POST collection is no-store, one-shot, RAM-only and expires after 15 minutes; no token file/log/SQL storage. Authorized Production collection returns 404. All owned processes were stopped, clearing the test handoff. Use this opt-in only in isolated local tests.

Email remains non-editable. Username login and normalized uniqueness remain. Recovery uses normalized unique User email. Bootstrap/legacy unconfirmed emails are not trusted; future separately authorized verification/email-change workflow is required. Production delivery adapter, protected persistent Data Protection keys, HTTPS/proxy/origin configuration remain deployment work.

## 22. API/DTO changes

| Route | Contract / result |
|---|---|
| POST /api/admin/users | Passwordless; required intended email; 201/400/404/409/503 |
| POST /api/admin/users/{id}/issue-credentials | Latest Version; Purpose only; 200/400/404/409/503 |
| POST /api/auth/activate | UserId, Token, NewPassword; 204/400/429 |
| POST /api/auth/forgot-password | Optional Email; generic 200/400/429 |
| POST /api/auth/reset-password | UserId, Token, NewPassword; 204/400/429 |
| POST /api/admin/users/{id}/credential-delivery | Explicit Development-only test handoff; 200/400/404, protected |

New strict requests: ForgotPasswordRequest, CompleteCredentialRequest, IssueCredentialRequest. Safe response CredentialIssuedDto. Adapter message CredentialDeliveryMessage is exposed only by the explicit Development test boundary. SecurityUserDto adds CredentialEstablished, EmailConfirmed, IsLockedOut; no Identity secrets. Existing status/roles/self-password APIs retained. Swagger documents purposes/responses with existing authorization-code filter.

## 23. CSRF/HTTP security

All unsafe requests, including anonymous credentials, require existing cookie/CSRF pairing. Credentials are bodies, never URLs. Existing HttpOnly/Strict cookies, Production Secure/HTTPS, explicit trusted proxy/origin rules and no Production Swagger retained. Recovery/test handoff are no-store. Login limiter remains 20/IP/minute; credential routes share standard 60/IP/minute, no queue. Excess credential requests verified as 429.

## 24. Concurrency behavior

Shared Serializable transaction and AccountLock authoritative refresh cover credential completion/issuance/change and admin writers. Relative Employee-before-User D12 order preserved, with shared role serialization gate between them. Expected versions and SQL uniqueness retained. Tested same-Employee provisioning, token reuse, reset versus disable/re-enable, role removal versus protected read, provisioning versus offboarding and final-two admin writers. No administrative state bypass or duplicate linkage.

## 25. Payroll non-impact

Payroll calculator/generation/preview/rules/statutory results/lifecycle/payslip implementation unchanged. No monetary or domain expectation changes. Fresh existing suites include Supplement/ReplaceAssignment, provenance/snapshots, rollback/regeneration, SSO, PIT and manual reconciliation.

## 26. Leave non-impact

Calendars/policies/entitlement, request/lifecycle, evidence, sandwich and D11 paid/unpaid calculation implementations unchanged. No new Payroll/Attendance coupling.

## 27. Attendance non-impact

Evidence, tick precision/residue, review/correction, immutable revisions/stale handling and reporting unchanged. Fresh existing authenticated regressions pass.

## 28. Migration/schema changes

None created, modified or applied. Existing Identity fields suffice. All 41 migration history entries unchanged. EF reports no pending model changes. No permanent seed changes.

## 29. Pure/live/security test results

| Verification | Assertions / result |
|---|---|
| Pure framework/domain suite | 981 PASS, including 26 D13 |
| Focused D13 live Identity/API/SQL | 78 PASS |
| D13 Production fail-closed | 7 PASS |
| Credential generic/rate-limit checks | 2 PASS |
| Final exact SQL baseline/schema checks | 10 PASS |
| D12 live compatibility | 52 PASS |
| D11 live paid/unpaid compatibility | 73 PASS |
| D10 core security | 45 PASS |
| D10 Production security | 26 PASS |
| D10 advanced ownership/race/lockout | 28 PASS |
| Anonymous routes / Swagger | 230 PASS: 229 operations plus interceptor |

Original A–AM matrix coverage:

| Cases | Evidence |
|---|---|
| A–F | No registration/implicit User; private linked provisioning, unique linkage, strict credential-free contract; Production token exclusion |
| G–K | Live activation/scoping/reuse/malformed; pure framework expiry |
| L–N, AD | Own-password validation/principal targeting, session refresh; bootstrap admin password change |
| O–Q | Identical generic eligible/unknown/unconfirmed/pending/disabled/no-email responses |
| R–V | Reset scoping/reuse, preserved identity/roles/HR and disabled state; revoked sessions; framework reset expiry |
| W–X | Five-failure lockout, preserved deadline/active distinction, expired fixture login |
| Y–Z | D10/HRAdmin restrictions, forgery rejection, role removal and stale-session denial |
| AA–AC | Sole admin disable/demotion/D12 end rejected; unusable alternatives ignored; final-two races preserve one |
| AE–AF | Fresh D12 rehire/offboarding/session/history regressions |
| AG–AI | Strict actor/input contract, audit attribution; token absence from logs/SQL; Production guard; CSRF/CORS/cookies |
| AJ–AK | Reset versus disable/re-enable; concurrent activation/reset single-use |
| AL–AM | Fresh D11 and full unchanged domain suites below |

Verification-only fixes: the initial forged TemporaryPassword case accidentally went through the legacy fixture adapter; corrected to send the raw strict request and explicitly cleaned its identified temporary row. Expanded fixture setup hit the unchanged login limiter; test-only pacing now respects 429. Neither required an application/security-policy workaround. Final fresh runs and exact cleanup passed.

Ignored result artifacts: tests/SIAMIS.Payroll.RegressionTests/bin/d13-*-results.json and logs. Tokens/passwords are never serialized into those results. These are local verification artifacts, not committed files.

## 30. Regression results

Fresh D13 authenticated domain runner: 2,958 scenario assertions, plus 18 wrapper/audit/cleanup assertions. No reused D12 totals and no adapted financial expectations.

| Suite | Assertions |
|---|---:|
| D9D review/finalization | 174 |
| D9B evidence | 121 |
| D9C daily calculation | 116 |
| D9E reporting | 201 |
| D8B / D1 calendar/employment | 117 |
| D8C request/calculation | 246 |
| D8C transition concurrency | 230 |
| D5A classification | 70 |
| D5C SSO integration | 306 |
| D6B PIT classification | 76 |
| D6C policy/claims | 48 |
| D6D monetary | 38 |
| D6E persistence | 88 |
| D6E boundary cases | 128 |
| D7 operations/payslip | 80 |
| D8D evidence/sandwich | 527 |
| D8D capped review | 392 |

Legacy security fixtures now provision passwordlessly and activate through real Development-only delivery before their unchanged domain/authorization assertions. Production security fixtures establish credentials through isolated Development setup, then exercise the actual Production pipeline against only the local database. No application authorization bypass.

## 31. Exact Development cleanup

All 85 tables' original rows/timestamps compare exactly to captured baseline; all 41 migrations unchanged. Employees=1 (original inactive TEST-EMP-001), EmploymentRecords=1, PayrollComponents=17, master rows=172, Roles=5. Users, UserRoles, UserClaims, UserLogins, UserTokens, RoleClaims and SecurityAuditEvents=0. All operational Leave/Attendance/calendar/Payroll/statutory tables remain at original empty baseline. No temporary fixture remains. Owned Development/Production API PIDs stopped and final listener/process check confirms none remain.

## 32. Complete changed-file list

Modified:

- src/SIAMIS.Api/Controllers/AdminUsersController.cs
- src/SIAMIS.Api/Controllers/AuthController.cs
- src/SIAMIS.Api/Controllers/EmploymentLifecycleController.cs
- src/SIAMIS.Api/Security/HrAuthorizationFilter.cs
- src/SIAMIS.Api/Security/SecurityRegistration.cs
- src/SIAMIS.Application/Security/SecurityContracts.cs
- src/SIAMIS.Infrastructure/Security/AccountLock.cs
- src/SIAMIS.Infrastructure/Security/AccountService.cs
- src/SIAMIS.Infrastructure/Security/EmployeeAccountLifecycleService.cs
- tests/SIAMIS.Payroll.RegressionTests/Program.cs
- tests/verify_d10_production.py
- tests/verify_d10_security.py

Created:

- D13-REPORT.md
- src/SIAMIS.Api/Controllers/DevelopmentCredentialDeliveryController.cs
- src/SIAMIS.Api/Security/CredentialDelivery.cs
- src/SIAMIS.Application/Security/CredentialContracts.cs
- src/SIAMIS.Infrastructure/Security/CredentialService.cs
- src/SIAMIS.Infrastructure/Security/SystemAdminGuard.cs
- tests/SIAMIS.Payroll.RegressionTests/D13CredentialTests.cs
- tests/verify_d13_baseline.py
- tests/verify_d13_live.py
- tests/verify_d13_production.py
- tests/verify_d13_rate_limit.py
- tests/verify_d13_regressions.py
- tests/verify_d13_security_regressions.py

25 files total. Ignored build/test artifacts excluded. No entities, EF configuration, migrations, domain calculations or production connection settings changed.

## 33. Remaining HR/security gaps

Production staff provisioning/recovery intentionally stays unavailable until a secure delivery adapter is configured. Protected persistent Data Protection keys and correct HTTPS/proxy/origin deployment remain essential. Email changes/legacy verified-contact establishment require a future separately authorized workflow. No UI, MFA, User deletion, broader notification platform or domain-processing changes introduced. Existing denied-operation auditing remains limited to established conventions.

## 34. Verification/status table

| Check | Result |
|---|---|
| Approved D13 contract / full A–AM matrix | PASS |
| dotnet restore | PASS |
| Final Release build | PASS: 0 warnings, 0 errors |
| Pure / live / Production / regression verification | PASS, totals above |
| Exact Development rows/timestamps / migrations | PASS, 85 tables / 41 entries |
| EF pending-model-changes | PASS: no changes since last migration |
| git diff --check | PASS |
| Targeted source/report token/secret scan | PASS |
| Live token scan of API logs and SQL audit | PASS |
| Owned API processes / listeners | Stopped / none |
| Migration / domain-calculation changes | None |
| Commit / push | None |
