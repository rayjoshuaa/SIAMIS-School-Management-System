# F6 — Employment & Account Lifecycle

Status: **READY FOR PRODUCT OWNER REVIEW — NOT COMMITTED — NOT PUSHED**.

## Contract inventory

Inspected EmployeeContracts, EmploymentLifecycleContracts/controller, EmployeeAccountLifecycleService, SecurityContracts, AdminUsersController, AccountService, CredentialContracts/Service, HrAuthorizationFilter, F5 Employee 360/forms, Administration User Accounts, and V2.1 interaction/overlay conventions.

| Workflow | Contract availability | Existing contract / boundary |
| --- | --- | --- |
| Employee detail and employment history | SUPPORTED | Employee.Read; GET employee and employment-history |
| Linkage/account readiness | SUPPORTED | Employee.Read; GET employees/{id}/account-lifecycle; safe linked User ID, Active/Disabled, current employment and offboarding readiness |
| Identity detail/roles/credential state | SUPPORTED | Security.Manage; GET admin/users/{id}; no password hashes/security stamps |
| Passwordless provisioning with optional Employee link | SUPPORTED | POST admin/users; existing employee required; Employee role requires linkage; initial activation through configured delivery |
| Contextual Employee preselection in Create User UI | PARTIALLY SUPPORTED | Backend accepts EmployeeId; current UI requires manual GUID entry. Use normal navigation with the existing Employee ID as context, without a second form |
| Account enable/disable | SUPPORTED | PATCH admin/users/{id}/status with IsActive and latest Version; backend revokes sessions and guards last usable SystemAdmin |
| Replace complete role set | SUPPORTED | PUT admin/users/{id}/roles with Roles and latest Version; removals/additions audited; backend validates permanent roles and Employee linkage |
| Activation/recovery initiation | SUPPORTED | Existing User Accounts confirmation and POST issue-credentials; server selects activation versus verified-email recovery; version/delivery/eligibility checks remain authoritative |
| Development credential delivery | SUPPORTED | Existing environment/configuration/capability gate; untouched. No real delivery requested during F6 testing |
| Employment change/end/rehire | SUPPORTED | Existing F5 routes. Ending employment with an active linked account requires explicit retain/disable decision, Security.Manage and current versions |
| Automatic account action from employment status | NOT SUPPORTED | No new automation; existing explicit end-employment decision preserved; rehire never reactivates accounts or changes roles |
| Relink/unlink existing Identity account | NOT SUPPORTED | Employee linkage immutable through current administration contracts |
| General security audit timeline in Employee 360 | NOT SUPPORTED | Existing backend auditing remains intact; no new audit-read API or fabricated history |

## Implementation

Employee 360 gains an **Account access** tab. It distinguishes employee record status, current employment status/presence, system account status and credential status. Unlinked employees have an honest empty state. Credential-established, forced-change, email-confirmation and lockout facts are read from the safe Identity DTO, only for Security.Manage users.

Account lifecycle reads use a separate query with cancellation, loading, error and retry states. A privileged detail query runs only for a linked account and Security.Manage. Returned EmployeeId must match the employee before detail or commands are displayed. Administration version and linked account version remain internal concurrency data, not visible UI values.

Security administrators can explicitly enable/disable a linked account or replace its complete role set. Shared AlertDialog provides confirmation, safe cancellation, focus restoration, pending protection and error retention. The role set begins with all current assignments; unselected roles are explicitly removed. Unknown assignments block replacement instead of being silently discarded. The centralized API client supplies fresh CSRF and existing session-expiry handling; no mutation retry is added. Success refreshes account/readiness/admin-list queries. Failure does not imply success; conflicts require reloading current facts.

Provisioning, activation and recovery use the existing `/hr/security` workflow. No duplicate creation form, token handling or password input was added. Employee preselection is not invented; authorized operators receive the Employee GUID as context. No account credential link was issued during testing.

F5 lifecycle actions remain unchanged. Explicit end-employment access decisions are preserved, without automatic offboarding or post-rehire reactivation. Access-review notices describe only backend-reported current employment presence versus Active/Disabled account state; they never declare a policy violation. No payroll, documents, leave or attendance behavior changes.

## Authorization

Employee 360 retains Employee.Read route protection. Employee.Manage still governs F5 employment actions. Security.Manage independently controls privileged account reads and commands. HRAdmin without Security.Manage cannot request administrative details or provision accounts. No role-name authorization was introduced; permanent role names are assignment choices only. SelfService, PayrollAdmin, HRDocuments and combined-role boundaries remain unchanged.

Backend AdminUsers authorization remains Security.Manage via HrAuthorizationFilter. Source-reviewed backend validation, version checking, Identity hashing, audit events and last-administrator protection remain authoritative. No frontend enforcement is claimed as a replacement for backend security.

**Live six-persona HTTP authorization acceptance remains open.** F6 uses the existing authenticated qa-systemadmin session for read-only browser review, plus isolated frontend mocks and existing backend regressions. It does not claim live denied/mutation tests for all six accounts.

## Verification

| Check | Result |
| --- | --- |
| Complete frontend suite | 211 tests passed, 12 files; 16 new focused Account Access tests and 2 profile integration regressions |
| F5 and V2.4 | Existing tests retained and passed |
| F6 coverage | Linked/unlinked, privilege filtering/no admin request, provisioning link, activation/credential/lockout states, independent statuses, factual review, confirmed status/roles requests, CSRF, cancellation/focus, linkage drift, unknown roles, HTTP 400/403/409/503 errors, 401 session expiry |
| Production frontend build | Passed |
| Lint | Passed, zero warnings |
| Frontend formatting | Passed |
| Backend Release build | Passed, zero warnings/errors; isolated temporary output keeps API running |
| Backend regression executable | 1,024 assertions passed, including D10 authorization, D12 offboarding, D13 credentials and D14 privacy/document security; no database connections |
| EF pending-model check | No changes since the last migration |
| Preservation | SHA-256 inventory confirms backend/migrations, V2.2/auth/session/API client, V2.3 shell, Administration User Accounts, F5 forms/directory and V2.4 HR dashboard unchanged |
| Data safety | Browser only reads and opens/cancels confirmation; mutations are isolated mocked fixtures. No real employees/users/roles changed; no credential issuance/reset, schema or migration operation |
| Git | No Git commands, commit or push; changed-file whitespace/conflict-marker scan performed without Git |

Chromium review used 1440×900, 1280×800, 1024×768, 768×1024, 430×932 and 390×844. No horizontal page overflow. Existing tabs wrap, facts reflow and actions retain accessible control sizing. Mobile role confirmation fits within the viewport; Escape dismissal and focus restoration verified against the real UI. Shared Radix dialogs/tabs and V2.1 tokens/focus/motion remain in use; no new CSS or dependencies. Vertical scrolling is natural for Employee 360. This is technical verification, not product-owner visual approval or a full accessibility certification.

Real read-only examples: DEV-QA-EMPLOYEE linked to qa-employee, active account, Employee role, established credential. TEST-EMP-001 remains inactive with Active employment status and no linked account, correctly shown as independent facts. User Accounts still displays eight existing accounts; Employee directory displays two existing employees. No operational fixture data was created.

## Changed files

- `frontend/src/features/employees/account-access.tsx` — new section and confirmed linked-account commands.
- `frontend/src/features/employees/contracts.ts` — existing safe lifecycle DTO fields typed.
- `frontend/src/features/employees/profile.tsx` — Account access tab only.
- `frontend/src/test/employee-account-access.test.tsx` — new focused isolated tests.
- `frontend/src/test/employees.test.tsx` — two account-readiness integration regressions; existing F5 tests preserved.
- `docs/frontend/F6-EMPLOYMENT-AND-ACCOUNT-LIFECYCLE.md` — this report.

## Review URLs and limitations

- Employee directory: <http://localhost:5175/hr/employees>
- Linked QA Employee 360: <http://localhost:5175/hr/employees/8422a4e8-faa7-480c-a1ae-4b3347378c34> — select Account access.
- Existing User Accounts: <http://localhost:5175/hr/security>

Frontend and API remain running. Existing User Accounts is reused without contextual preselection or automatic opening of an account drawer. Account relinking/unlinking and a security audit timeline require future approved backend contracts. Roles/credentials are deliberately unavailable to Employee.Read-only users. No F7 work was started.
