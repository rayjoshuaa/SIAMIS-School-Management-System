# Development QA authorization personas — web administration

Current status: Development administrator access restored. The minimal web
User Accounts functionality, interaction model and UX architecture are approved.
Final SIAMIS-wide visual polish is deferred. QA personas are not yet created.
Local commit preparation is approved; not pushed. Earlier stopped reviews below are
retained as checkpoint history.

## Superseded provisioning approach

The product owner superseded `tools/development/Provision-QaPersonas.ps1` on
2026-10-08 in favor of SIAMIS web Administration → User Accounts using the
existing D13 provisioning APIs. The script remains unverified and is retained
pending review. Do not run it. No QA personas or dedicated QA Employee have
been created.

## Administrator access prerequisite

The current in-app browser redirects `/hr/security` to the normal login page;
no usable authenticated Security.Manage browser session was verified.

Read-only account inspection found:

- `f4-bootstrap-helper`: active SystemAdmin, credential established, no forced
  password change, no recovery email or email confirmation. Its supplied local
  credential previously failed normal login. Existing first-admin bootstrap is
  disabled while a SystemAdmin exists.
- `f4-review`: HRAdmin only; cannot administer users through Security.Manage.

Implementation is paused as explicitly requested until an initial administrator
credential/access decision is made. No password reset, role mutation, bootstrap
bypass, account creation, database/schema migration, or authorization change is
authorized by this review.

## Existing backend contracts

`AdminUsersController`, protected by Security.Manage, already provides:

- GET `/api/admin/users` and GET `/api/admin/users/{id}` — safe account DTOs.
- POST `/api/admin/users` — passwordless provisioning with email, existing roles,
  optional Employee linkage; initiates activation through configured delivery.
- PUT `/api/admin/users/{id}/roles` and PATCH `/api/admin/users/{id}/status` —
  version-protected administration.
- POST `/api/admin/users/{id}/issue-credentials` — activation/reset initiation;
  no production token response.

The existing Development-only credential delivery collection is additionally
guarded by environment, explicit delivery configuration, and Security.Manage.
Production delivery still requires a configured delivery provider. No new
backend contract is required for the requested minimal web workflow.

The future frontend must use Security.Manage capability gating and the real
backend policies. Administrators must not choose final user passwords. The
approved dedicated QA Employee and six personas remain deferred until both
administrator access and the web workflow are verified.

## Preservation and verification

This review only adds this status document. V2.2, V2.3, and F4 source files are
unchanged. No QA script execution or login retry occurred during this review.
Frontend/backend verification gates are deferred because implementation stopped
at the mandatory administrator-access prerequisite. No commit or push.

## Approved controlled recovery — completed

The product owner subsequently approved an interactive Development-only
credential reset of the existing `f4-bootstrap-helper`, using real Identity
password validation/hashing, security-stamp rotation and audit recording.
`tools/development/ResetDevelopmentAdministrator` provides the narrowly scoped
operator mechanism. It does not provision users, consume the old QA credential
files, alter roles, host an endpoint or bypass normal login.

Its Release build passed with zero warnings/errors; all six environment/target
isolation checks passed without database access. The existing backend regression
runner passed 1,024 assertions, including D10/D13/D14 coverage.

At the pre-reset baseline: Users 2, UserRoles 2, Employees 1,
EmploymentRecords 1, SecurityAuditEvents 31, migration history 42. The audit
increase from the original 30 records is the previously reported failed helper
login. The product owner privately ran the tool and selected the replacement
password. SQL verifies one DevelopmentAdministratorCredentialReset audit event.
The real Codex browser verified normal authenticated account reads, logout and
another normal login as f4-bootstrap-helper. Its existing SystemAdmin assignment
and Security.Manage endpoint access remain intact. No QA personas are created
automatically; the product owner will create/review them through the web workflow.

## Minimal Administration → User Accounts

Review URL: <http://localhost:5175/hr/security>. This preserves the existing
protected route inside V2.3's Administration module; it replaces its placeholder
only. No shell layout redesign or separate administration module was introduced.

- Bounded, paginated account list (20 rows), safe account detail, actual roles,
  active/disabled/lockout and credential status.
- Passwordless Create User: username, delivery email, the five existing permanent
  roles, and optional existing Employee GUID. Employee role requires linkage.
  No Employee is created by this form; no employee management feature is added.
- Initial activation is initiated by D13's existing create API. Reissue activation
  or initiate eligible verified-email recovery uses issue-credentials with the
  latest AdministrationVersion. No final password is chosen by administrators.
- Development-only transient delivery handoff uses the existing authenticated,
  environment/configuration-protected collection endpoint. Link state is RAM only;
  no token is persisted, logged or placed in documentation. Existing activation
  link capture removes sensitive query parameters before rendering/API requests.
  The Production bundle excludes these Development controls and endpoint path.
- Production activation/recovery still needs its real configured delivery provider.
  A 503 does not imply successful account creation or delivery.
- No status editing, role editing, employee creation, deletion, bulk provisioning
  or new administration business workflow in this minimal scope.

Security.Manage controls route visibility, protected routing and the component's
query/mutation entry. The existing backend policy remains authoritative. Role
names are assignment choices only, not frontend authorization conditions.
HRAdmin's lack of payroll access and SystemAdmin's lack of HRDocuments access
remain unchanged. Roles/capabilities, D10/D13/D14/D15 behavior, schema and
migrations are unchanged.

## Verification and preservation

- Frontend: 146 tests across 10 files, including 10 focused administration tests;
  production build, lint and formatting passed.
- Backend: Release solution build, zero warnings/errors; 1,024 existing regression
  assertions including 40 D10, 26 D13 and 43 D14 assertions passed.
- Reset tool: Release build and six environment/target isolation checks passed.
- EF: no pending model changes. Repository: git diff --check passed.
- Live Development: authenticated backend list/detail of the two existing users;
  passwordless form and required-field validation; normal logout/login-again;
  real anonymous GET /api/admin/users returns 401.
- Responsive DOM-width checks: 1440×900, 1280×800, 1024×768, 768×1024,
  430×932 and 390×844; no document-wide horizontal overflow. Mobile tables
  scroll within their own named region. Desktop/mobile Chromium screenshots
  inspected; viewport overrides reset. No Firefox/WebKit claim.
- No live account creation or credential issuance was performed. Create/activation
  request semantics are frontend contract-tested and supported by existing D13
  regressions. First new-account live provisioning remains product-owner review.
- SHA-256 comparison of the pre-change frontend inventory shows only router.tsx,
  navigation.ts and one V2.3 shell test assertion changed among existing files.
  All V2.2 auth source/tests/styles, V2.3 layout components/styles, and F4
  source/contracts/tests remain byte-for-byte unchanged. F4's existing router
  branch remains intact. No backend src file changed.

### Database before/after

| Table | Original approved baseline | Immediately before reset | Final |
| --- | ---: | ---: | ---: |
| Users | 2 | 2 | 2 |
| UserRoles | 2 | 2 | 2 |
| Roles | 5 | 5 | 5 |
| Employees | 1 | 1 | 1 |
| EmploymentRecords | 1 | 1 | 1 |
| SecurityAuditEvents | 30 | 31 | 36 |
| Migration history | 42 | 42 | 42 |

The pre-reset audit increase was the earlier failed helper login. The subsequent
five audit events are one explicit reset, three successful normal helper logins
(including product-owner entry), and one normal logout. Audit rows are retained.
Helper credential hash/security stamp, Identity concurrency state and
AdministrationVersion changed through Identity; normal login reset its failed
attempt counter from 1 to 0. No other account mutation occurred. TEST-EMP-001
remains the sole Employee and inactive. No QA employee, operational fixture or
new user was inserted. f4-review roles/credentials were not changed.

### Complete files changed for recovery + web administration

- tools/development/ResetDevelopmentAdministrator/ResetDevelopmentAdministrator.csproj
- tools/development/ResetDevelopmentAdministrator/Program.cs
- tools/development/ResetDevelopmentAdministrator/README.md
- docs/development/DEV-QA-AUTHORIZATION-PERSONAS.md
- frontend/src/features/administration/account-contracts.ts
- frontend/src/features/administration/user-accounts.tsx
- frontend/src/app/router/router.tsx (account-route mapping only)
- frontend/src/app/router/navigation.ts (account label/description only)
- frontend/src/test/user-accounts.test.tsx
- frontend/src/test/v23-shell.test.tsx (matching label assertion only)

Pre-existing uncommitted V2.2/V2.3/F4 work is retained separately. The superseded
Provision-QaPersonas.ps1 remains untouched/unverified and was not run. No old
encrypted QA credential files were consumed for this workflow.

## Planned personas and remaining boundaries

qa-systemadmin, qa-hradmin, qa-payrolladmin, qa-management, qa-employee and
qa-systemadmin-hr remain planned, not provisioned. The combined-role persona
will test normal SystemAdmin + HRAdmin capability resolution. qa-employee must
use its own dedicated Development QA Employee; never TEST-EMP-001. Creating that
minimum Employee/link is a later explicitly controlled operation, not part of
this account form. No salaries, payroll, leave, attendance, documents or
operational history should be fabricated.

No QA cleanup/reset is needed now because none were created. Later account
cleanup must identify only approved QA identities, respect existing auditing and
ownership/relationships, and receive its own authorization. The original review
accounts are retained. V2.3 product-owner visual approval remains pending. V2.4
and operational UI development were not started.

## V2 overlay UX refinement

User Accounts remains the full-page bounded list. Create User now uses the shared
V2.1 Dialog: its passwordless fields, role selection and optional existing
Employee GUID fit a focused modal. View opens a shared Sheet with safe account,
credential, role and Employee-link metadata. Activation/recovery issuance and
the existing Development transient-delivery collection require an explicit
AlertDialog decision. These are separate overlay states, never nested. Cancel
returns to inspection; successful create/credential actions return to the drawer.
Role/status editing and additional administration workflows were not added.

The underlying list remains mounted with its current pagination and scroll
context. Focus starts on Username for creation, Close for inspection and Cancel
for confirmation. Radix retains modal focus containment/background isolation.
Escape/backdrop close safe forms/inspection; confirmation ignores backdrop.
Pending commands disable cancellation/dismissal and duplicate submission.
Failures remain in the active overlay; field values are retained. Confirmation
errors are scoped to the selected command, preventing stale issuance errors from
appearing in delivery collection. Close restores the original list/Create invoker.

Dialogs/confirmations have viewport-bounded scrolling. The drawer is a full-height
side panel on desktop and full-width sheet on narrow mobile screens. Existing
100ms opacity opening and reduced-motion suppression remain; no new animation,
dependency or overlay framework. The global modal/drawer/page decision standard
and anti-patterns are documented in V2.1-PREMIUM-DESIGN-SYSTEM.md, section 17.

### Refinement verification

- Frontend: **154 tests in 10 files passed**, including **18 account tests**;
  production build, lint and formatting checks passed. Focused coverage includes
  focus containment/restoration, Escape/backdrop, single-overlay transitions,
  pending dismissal protection, validation, API failure/success, retained list
  pagination, Production handoff omission and capability/anonymous gating.
- Backend: Release build passed with zero warnings/errors; **1,024 regression
  assertions passed**, including existing D10/D13/D14 security contracts.
- EF reports no pending model changes; git diff --check passed.
- Live authenticated Chromium: inspected existing account list/detail, opened
  creation and required-field validation (no POST), checked Username initial
  focus and Shift+Tab trap, Escape/opener focus, recovery confirmation with Cancel
  initial focus, Cancel back to inspection and row focus restoration.
- Create and drawer geometry checked at 1440×900, 1280×800, 1024×768,
  768×1024, 430×932 and 390×844. No horizontal document overflow. At 390×844,
  the creation modal scrolls internally with reachable actions. Desktop and
  mobile screenshots inspected. Viewport override reset. Reduced-motion CSS
  inspected; OS preference toggling and an actual mobile software keyboard were
  not tested. No Firefox/WebKit claim.
- Successful mutation/error/loading paths are fixture-tested; no live account
  creation, issuance, recovery or transient-token collection occurred.
- SHA-256 comparison with the pre-refinement inventory confirms V2.2 auth,
  V2.3 shell/router/navigation and F4 source/tests remain byte-for-byte unchanged.
  Backend, schema, migrations, contracts, permanent roles/capabilities and
  existing credentials are unchanged. No QA personas were created.
- Read-only Development counts: Users **2**, UserRoles **2**, Roles **5**,
  Employees **1**, EmploymentRecords **1**, migration history **42**.
  SecurityAuditEvents **36 → 37**, the user's normal private login for this
  browser review; existing audit history retained. TEST-EMP-001 remains inactive.

### Complete refinement changed-file list

- frontend/src/components/ui/overlays.tsx
- frontend/src/features/administration/user-accounts.tsx
- frontend/src/test/user-accounts.test.tsx
- docs/frontend/V2.1-PREMIUM-DESIGN-SYSTEM.md
- docs/development/DEV-QA-AUTHORIZATION-PERSONAS.md

Review: http://localhost:5175/hr/security using normal Security.Manage access.
Frontend/API remain running. Administration User Accounts functionality and UX
architecture are approved; global V2 overlay standard established. Final visual
polish is deferred. QA personas have not been created. Local commit preparation
is approved; not pushed. Historical verification results above remain unchanged.
