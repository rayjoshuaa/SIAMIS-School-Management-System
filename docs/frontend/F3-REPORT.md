# F3 — authentication, session and account access

## Summary / starting point

Completed 2026-10-05 from clean HEAD `576307dc513bc888ca01f339ef712204c0e23166` (F2 committed by the owner). F3 connects real authentication to the F2 school shell using existing frozen contracts. No backend code, migration, schema, business workflow, package version or production security setting changed. No commit/push or permanent administrator provisioning.

## Inspected backend / contracts

Read Program, SecurityRegistration, AuthController, ApiCsrfFilter, HrAuthorizationFilter, IdentityPrincipalFactory, AccountService, CredentialService, account lifecycle, delivery and DTOs; verified Swagger contracts in the existing D13 suite. `/api/auth/me` safely provides identity and effective capabilities, so no additive backend endpoint was needed.

| Endpoint | Existing contract used |
|---|---|
| GET `/api/auth/csrf` | Fresh token + framework antiforgery cookie |
| POST `/api/auth/login` | Username/password; 204 or generic 401; non-persistent |
| GET `/api/auth/me` | Safe user/capabilities/account flags; 401 on rejected session |
| POST `/api/auth/logout` | Authenticated, CSRF protected; 204 |
| POST `/api/auth/change-password` | Current/new password; required-change flow and session refresh |
| POST `/api/auth/activate` | UserId/token/new password; password establishment + email verification |
| POST `/api/auth/forgot-password` | Email; enumeration-neutral 200 |
| POST `/api/auth/reset-password` | UserId/token/new password; previous-session invalidation |

The minimal frontend session projection excludes internal Identity stamps/hashes/version/employment detail. Roles are safe display context only; effective capabilities drive menus and direct-route UX, including real `Security.Manage`. Planned school modules gain no permissions.

## Session, login and authorization

AuthProvider represents bootstrapping/authenticated/anonymous/expired/recoverable failure. Initial/refresh/post-login `/me` is authoritative; authenticated focus and five-minute revalidation prevent indefinite cached admission. Loading avoids login flashing. Network/server/malformed responses show retry, not invalid credentials or assumed anonymity.

Shared AuthLayout serves `/login`, `/activate`, `/forgot-password`, `/reset-password` and `/change-password`. F1 branding, controls, accessible labels/autocomplete, show/hide password, keyboard submission, root announcements, field error association/focus and duplicate-submit prevention remain. Mode keys reset form state when navigating back from recovery. Required-password-change accounts cannot enter the shell and can sign out.

Protected routes send anonymous/expired users to login; authenticated login visits return safely. Intended destinations accept only exact registered internal application paths, preventing open redirects. Missing capability displays Access Denied inside the shell; backend 403 remains authoritative and is not a login redirect. Existing 400/404/409/5xx ProblemDetails behavior stays intact.

## Cookies, CSRF, account and cache security

F1 credentialed fetch obtains a fresh `/csrf` token centrally before every unsafe command, including login/logout/credential commands. No cookie decoding or tokens in browser storage. Actual browser verification confirmed `SIAMIS.Session` remained Secure, HttpOnly and SameSite=Strict. Remember Me is absent because login is non-persistent.

Account menu uses the real username and safe roles. Profile/settings remain deferred. Logout calls the server before clearing identity, capabilities and Query data; failure stays retryable. Authoritative protected 401 cancels/clears caches and transitions to expired/login. Invalid login/anonymous credential failures are excluded from session-loss notification. Request epochs suppress an older request's 401 after identity changes. Bootstrap revalidation also clears caches before exposing the resulting identity. User A data cannot carry into User B through the established provider.

## Activation / recovery / password policy

Activation/reset links consume unchanged backend UserId/token contracts through URL-encoded frontend query parameters. Parameters are scrubbed before router construction and API calls, kept only in RAM and discarded on success/unmount; no token inputs, analytics, browser persistence or referrer propagation. Initial hosting access logs still need deployment query redaction. A scrubbed-page refresh requires reopening the delivered link.

Identity requires 12–256 characters without required upper/lower/digit/symbol classes; confirmation is frontend-only. Invalid/expired/reused tokens share generic failure. Activation verifies the provisioned email and establishes a password without enabling disabled accounts. Forgot-password preserves neutral responses and verified-email eligibility. Reset never changes roles, account administrative state or employment and invalidates old sessions. Actual disable/reset/offboarding behavior and last-SystemAdmin protection remain untouched.

## Verification / cleanup

| Check | Final result |
|---|---|
| Frontend build | PASS; lazy auth/shell chunks; no warnings |
| ESLint / Prettier | PASS; zero lint warnings |
| Frontend tests | 60/60 PASS (37 retained shell/foundation + 23 focused auth) |
| Backend Release build | PASS; 0 warnings / 0 errors |
| Existing pure regression runner | 1024 PASS, including D10 40, D12 26 and D13 26 assertions |
| Existing D13 live suite | 78 PASS; verified localhost HTTPS; exact 85-table baseline restored |
| Real frontend/API browser checks | 114 PASS; temporary fixtures; exact baseline restored |
| Production/Development gating | PASS with synthetic responses for Production preview only |
| EF pending-model check | No changes since last migration |
| Diff / targeted secret check | PASS; no credentials/tokens in deliverable files |

Real browser checks used 1440, 1280, 1024, 768, 430 and 390px for login, forgot, activation, reset, authenticated shell/account, invalid login, forbidden and expired-session presentation. No horizontal overflow or rendering exceptions. Actual flows covered anonymous protected routing, invalid credentials, activation, keyboard login, activation-to-login navigation, authenticated refresh, capability-filtered navigation, direct forbidden route, server logout/refresh, neutral recovery, reset, stale-session rejection and disabled-account login rejection. Screenshots were visually reviewed; this is focused accessibility verification, not complete certification.

Production preview tests used synthetic `/me` responses solely to verify route/build gating; they are not claimed as Production E2E. `/dev/ui` works independently without auth/API requests and is unavailable in Production. The F2 Development provider and preview controls are removed from normal execution.

Temporary users used established bootstrap, provisioning, Identity and activation fixtures, never permanent provisioning. Credentials/tokens stayed in RAM/stdin pipes; no private certificate key was exported. The test API's RAM delivery process was stopped after cleanup. Exact full SQL snapshots matched before/after both suites, not only row counts. Final SQL:

| Table | Count |
|---|---|
| Employees | 1 |
| EmploymentRecords | 1 |
| PayrollComponents | 17 |
| Roles | 5 |
| Users | 0 |
| UserRoles | 0 |
| SecurityAuditEvents | 0 |

No baseline Employee data changed. No migration was generated/applied. Backend source matches `hr-backend-v1`; all source/tests/solution configuration and existing F1/F2 reports match starting HEAD. Ignored `frontend/test-results/` holds local helpers/screenshots/public certificate, not secrets or deliverables.

## Development / deployment / next scope

See [authentication/setup](AUTHENTICATION.md). Live verification used the existing HTTPS API through a TLS-verifying Vite proxy at `http://localhost:5175`, since 5173 was occupied; Node trusted the public Development certificate. No CORS/cookie security policy was weakened. Chromium accepted Secure cookies through its localhost exception; other browsers require compatible certificate/HTTPS Development setup. A normal Development API process (test delivery disabled) and frontend review server can show login; no account remains for normal sign-in.

Production requires one HTTPS browser/API origin, SPA fallback, certificate/proxy/Data Protection hardening, sensitive-log redaction and explicitly configured credential delivery. Delivery remains deferred/fail-closed and no deployment was performed. F4 real HR pages, account profile/settings, dashboard data, self-service feature UX and first permanent administrator provisioning require their own approved scope. No unresolved contract stop condition remains.

## Final authentication branding — visual polish

The owner-supplied `SIAM Logo.png` and clean `Grand Red-Brick Campus Building.png` are stored under `frontend/src/assets/branding/` with maintainable school-specific filenames. SHA-256 comparisons confirm both copies are byte-for-byte unchanged; no crest reconstruction, image generation, destructive processing or Maps screenshot was used. Their original aspect ratios and quality are retained.

Shared AuthLayout now presents the real crest, “SIAM International School” and “School Management System” above the existing opaque authentication card. A full-viewport decorative campus layer uses CSS cover positioning, 1px blur, restrained brightness/contrast and a blue overlay; mobile uses a smaller logo and stronger overlay. Login, activation, recovery, reset and the existing password-change page share the same presentation. Existing form components, focus states, authentication, CSRF, session, authorization and credential semantics are unchanged.

Visual-pass verification: frontend build/lint/formatting PASS; all existing 60 frontend tests PASS. Real Development browser checks covered all four requested auth pages at 1440, 1280, 1024, 768, 430 and 390px (24 combinations), with loaded assets, preserved aspect ratio, decorative background, no horizontal overflow or browser exceptions. Desktop/mobile screenshots were inspected; password visibility still works, including a 390px-wide shorter viewport. Only safe reads and unsubmitted dummy activation/reset links were used: no database fixtures or mutations, backend changes, migrations, security-test changes, commit or push. `git diff --check` PASS. Preview: <http://localhost:5175/login>.

The two original PNGs total about 3.88 MB; their quality is preserved in this pass. Deployment can separately consider owner-approved optimized derivatives while retaining originals.

Visual-pass changed files only:

- `frontend/src/assets/branding/siam-international-school-logo.png`
- `frontend/src/assets/branding/siam-international-school-campus.png`
- `frontend/src/features/auth/auth-layout.tsx`
- `frontend/src/features/auth/auth-layout.css`
- `docs/frontend/F3-REPORT.md`

### Final login-card adjustment

Login-only presentation now places the official logo, school name and “School Management System” inside the card above a restrained gold separator. “Sign in to SIAMIS” is visually secondary, with the unchanged supporting text. “Forgot password?” appears before the sign-in button. No marketing tagline, public activation action or “or” divider is present; Remember Me remains absent because the existing backend does not support persistent login. Administrator-issued activation links and all other auth routes/semantics remain operational and unchanged.

Changed in this final adjustment: `auth-layout.tsx`, `auth-layout.css`, presentation markup in `auth-form.tsx`, and this report. The approved campus asset and CSS treatment are unchanged. Build/lint/formatting and all 60 existing frontend tests PASS; browser verification covered all six requested widths (including no horizontal overflow, logo inside login card, no activation action or checkbox, and preserved password visibility). All four auth pages were checked, including activation/reset forms using unsubmitted dummy links. `git diff --check` PASS. No backend/database/migration/security-test changes, commit or push.

### Compact login proportions

The follow-up presentation correction reduces desktop card dimensions from 448 × 638px to 416 × 530px, with 28px horizontal / 24px vertical desktop padding, an 84px logo, tighter branding/form spacing and a secondary 18px heading. Inputs and touch controls retain their 44px minimum height. No page zoom/scale is used. Campus `cover` sizing remains; the extra image scale is removed, the blue overlay is lighter, and desktop/tablet/mobile positioning preserves school context (including the flag on laptop compositions where possible).

Before/after screenshots and measured checks passed at 1440×900, 1280×800, 1024×768, 768×1024, 430×932 and 390×844: no horizontal overflow or vertical scrolling, complete card visible, original images retained. Build/lint/format checks and all 60 existing tests PASS; `git diff --check` PASS. Changes are presentation-only in the same three auth layout/form files and this report. Authentication, activation/recovery, backend, database and migrations are untouched; no forms were submitted, commit or push performed.

### Login error separation

Login clears stale form-level errors on a new attempt and when username/password changes; field validation still gates the existing submit handler. Incomplete credentials never invoke login. Authentication rejection and network/server failures retain their separate existing messages in the form-level location, now announced as a login alert. No fixed-height or reserved error space was added: the compact 530px card grows naturally to 586px for two required-field messages.

Nine additional focused tests cover empty-field combinations, authentication versus network/server failures, edits and pending retries; all 69 frontend tests, build/lint/format checks and `git diff --check` PASS. Browser checks at 1440×900, 1280×800, 430×932 and 390×844 confirmed natural growth, field/general separation and no horizontal overflow. Failure responses were explicitly synthetic; no real credentials, successful login or database writes were used. This correction changes only `auth-form.tsx`, adds `login-errors.test.tsx`, and updates this report. Existing security tests, APIs, CSRF, sessions, capabilities, backend and migrations remain unchanged. No commit or push.

### Final campus asset and login focus correction

The owner subsequently supplied `Grand Thai Campus Under Blue Skies (1).png`, copied unchanged to `frontend/src/assets/branding/siamis-campus-login.png` (matching SHA-256). It is now the shared centered `cover` background with a restrained 16% navy overlay (20% mobile), no filtering or image scaling. The approved 416 × 530px desktop login card and natural error growth remain unchanged; all six specified viewports were visually reviewed for this replacement.

The unwanted heading rectangle was reproduced: AuthForm programmatically focused its `tabIndex=-1` H1, activating the existing maroon `:focus-visible` rule. Login now uses React Hook Form's `setFocus` for Username and removes focusability from the login H1. Other auth-page heading focus and global interactive focus styling remain intact; no CSS workaround or layout change was made. Three focused tests and logout focus assertions cover entry, keyboard order, retries and logout. Browser checks PASS for direct visit, refresh, synthetic logout/session, failed login/retry and visible keyboard focus on all controls. All 72 frontend tests, build/lint/format checks and `git diff --check` PASS. No backend/database/migration changes, commit or push.

### Shared label / field spacing polish

The original inline Label plus `space-y-2` arrangement did not reliably establish label-to-input separation. Shared FormField now uses a column layout with an explicit 8px control gap and 6px hint/error gap. Shared Label is block-level, 14px / 600 weight / 20px line-height. Invalid inputs/textareas/select triggers use the existing destructive border token; normal focus retains the existing separated maroon outline. Required indicators, ARIA associations, error announcements, 44px controls and password-button alignment are unchanged.

The logo is modestly enlarged to 96px and the owner's latest `Siam International School Entrance.png` replaces the campus asset byte-for-byte (SHA-256 matched), retaining centered cover and restrained overlay. Card structure, authentication and natural validation growth remain unchanged. Desktop/mobile browser checks measured consistent 8px / 6px gaps through untouched, hover, keyboard/mouse focus, filled, required-error and synthetic authentication failure states. Chromium autofill styling was verified using DevTools-forced `:autofill`, not an actual password-manager credential flow. F1 showcase validation/disabled layouts and shared auth-page viewport checks PASS. All 72 existing frontend tests, build/lint/format and `git diff --check` PASS. No backend/database/migration changes, commit or push.

## Complete changed-file list

- `README.md`
- `docs/frontend/ARCHITECTURE.md`
- `docs/frontend/AUTHENTICATION.md`
- `docs/frontend/F3-REPORT.md`
- `docs/frontend/README.md`
- `frontend/index.html`
- `frontend/src/assets/branding/siam-international-school-logo.png`
- `frontend/src/assets/branding/siam-international-school-campus.png`
- `frontend/src/assets/branding/siamis-campus-login.png`
- `frontend/src/app/providers/auth-provider.tsx`
- `frontend/src/app/providers/development-navigation.tsx` (removed)
- `frontend/src/app/router/router.tsx`
- `frontend/src/components/layout/account-menu.tsx`
- `frontend/src/components/layout/app-shell.tsx`
- `frontend/src/components/shared/form-field.tsx`
- `frontend/src/components/ui/controls.tsx`
- `frontend/src/features/auth/auth-boundaries.tsx`
- `frontend/src/features/auth/auth-form.tsx`
- `frontend/src/features/auth/auth-layout.tsx`
- `frontend/src/features/auth/auth-layout.css`
- `frontend/src/lib/api/client.ts`
- `frontend/src/lib/auth/auth-context.ts`
- `frontend/src/lib/auth/contracts.ts`
- `frontend/src/lib/auth/credential-link.ts`
- `frontend/src/lib/auth/navigation-session.tsx`
- `frontend/src/lib/auth/return-url.ts`
- `frontend/src/lib/auth/session-events.ts`
- `frontend/src/test/auth.test.tsx`
- `frontend/src/test/login-errors.test.tsx`
- `frontend/src/test/shell.test.tsx`
