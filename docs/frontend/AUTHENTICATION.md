# Frontend authentication

F3 integrates frozen D10–D13 identity, capabilities, account eligibility, security stamps and CSRF. No JWT, browser-stored auth token, role inference or backend change is introduced.

## Existing contracts

| Method/path                      | Contract                                                                                                     |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| GET `/api/auth/csrf`             | `{ token }`, no-store, HttpOnly antiforgery cookie                                                           |
| POST `/api/auth/login`           | `{ userName, password }`; 204 or generic 401; non-persistent login                                           |
| GET `/api/auth/me`               | Safe identity, effective capabilities, account flags and optional Employee linkage; 401 for rejected session |
| POST `/api/auth/logout`          | No body; authenticated/CSRF-protected; 204                                                                   |
| POST `/api/auth/change-password` | `{ currentPassword, newPassword }`; 204 or generic 400                                                       |
| POST `/api/auth/forgot-password` | `{ email }`; neutral 200 for eligible, unknown, pending, unconfirmed or disabled accounts                    |
| POST `/api/auth/activate`        | `{ userId, token, newPassword }`; 204 or generic 400                                                         |
| POST `/api/auth/reset-password`  | Same fields as activation; 204 or generic 400                                                                |

Unsafe commands obtain a fresh CSRF token centrally through the F1 API client and send `X-CSRF-TOKEN`. Requests use `credentials: include`, no-store and redirect rejection. JavaScript never reads authentication cookies or persists CSRF tokens.

## Session and routes

AuthProvider owns bootstrapping, authenticated, anonymous, expired and recoverable-error states. It reads `/api/auth/me` initially, after login, on authenticated window focus and every five minutes while authenticated. Initial loading avoids login flashing. If the initial probe is unavailable, public `/login` remains visible; protected routes stay fail-closed. Actual login network/server failures appear within the form and do not claim invalid credentials.

Only safe username, User ID, optional Employee ID, active/password-change flags, roles and effective capabilities enter the session context. Roles are display context only. Required-password-change accounts cannot enter the normal shell; they complete the existing password-change command or sign out.

Anonymous/expired application requests go to `/login`. `returnTo` allows only exact registered application paths, rejecting external, protocol-relative, query-bearing, auth and unknown destinations. Missing effective capability shows Access Denied inside the shell. Backend permission/ownership remains authoritative. Planned school modules acquire no fake capabilities.

Login, activation, forgot/reset and required password change share AuthLayout and F1 controls. Each mode gets a fresh form instance. Profile/settings remain deferred. Authenticated login visits return safely to the application.

## Session loss and caches

Successful logout ends the server session before clearing frontend identity and Query caches. Protected-request 401 cancels/clears queries, removes identity/capabilities and displays a session-ended message. Invalid login and anonymous credential failures are excluded from session-loss notifications. Request epochs prevent an older request's response invalidating or populating a newer session.

Stage 1 preserves the mounted tree and Query cache when revalidation returns the same security context: user ID, Employee linkage, active/password-change flags and normalized role/capability sets. A changed context clears protected caches, advances the request epoch and remounts the protected boundary. Logout invalidates in-flight refresh before issuing its command.

Transient authenticated revalidation failure retains RAM-only edits while access is blocked. A native modal dialog masks/inerts protected content, including existing overlay portals, and offers retry; Escape cannot dismiss this security block. Protected requests and mutations waiting for CSRF are prevented from crossing the blocked/changed session boundary. Existing overlay guards preserve unsaved forms. This is not offline authorization, fake authentication or permission to submit with an unverified session. Drafts do not persist across full reloads, logout or context changes. Browser-tab persistence remains deferred.

403 remains forbidden, not a login redirect. F1 400 validation, 404 resource hiding, 409 conflict and 5xx/network normalization remain intact. Forms use generic credential/token messages, including rate-limit recovery, without raw Identity detail/stacks. Failed logout remains retryable and does not pretend the server session ended.

## Credential links and password rules

Backend delivery supplies `{ userId, token, purpose }`, not a configured frontend URL. Frontend routes accept `/activate?userId=...&token=...` and `/reset-password?userId=...&token=...`. Delivery must URL-encode the unchanged token (including `+`) and use the correct frontend origin. This composes a frontend link without changing the backend token contract.

Query parameters are captured in RAM and removed by history replacement before router construction and API calls. The document sets no-referrer metadata. Tokens are not displayed, logged, stored or sent to analytics; they are discarded on success and credential-view unmount. Refreshing a scrubbed link requires reopening the delivered link. Deployment access logs must redact credential query parameters: the initial document request necessarily reaches the hosting server.

Identity requires 12–256 characters, with no required uppercase/lowercase/digit/symbol combination. Confirmation is frontend-only and never enters strict request DTOs. Invalid/expired/reused tokens share the generic backend failure. Activation establishes a password and verifies provisioned email but never enables disabled accounts. Recovery requires an active account, established password and confirmed email. Successful reset invalidates previous sessions without changing roles, employment or administration.

## Development setup

Run the existing API profile and supply the public localhost certificate to Node before starting Vite:

```powershell
dotnet run --project .\src\SIAMIS.Api -c Release --launch-profile SIAMIS.Api
# In another terminal; use your public certificate path:
$env:NODE_EXTRA_CA_CERTS = 'C:\path\to\public-development-certificate.pem'
cd frontend
npm run dev
```

The existing Vite proxy verifies TLS to `https://localhost:7142`; browser requests use same-origin `/api`. No CORS/cookie policy changed. Verification used `http://localhost:5175` because 5173 was occupied. Chromium's localhost secure-context exception accepted Secure cookies; this is not a general HTTP deployment recommendation. Other browsers need compatible certificate/HTTPS Development setup without weakening policy.

The original F3 fixtures were cleaned after their checkpoint. Subsequent approved web provisioning established six reusable Development QA personas alongside existing review/helper accounts; these are not Production seeds. Do not consume old credential files or run the superseded QA provisioner. Normal Development delivery remains opt-in. `/dev/ui` stays independent of session/API availability and is excluded from Production. The F2 capability switch remains removed.

## Deployment and next scope

Production should host browser and API under one HTTPS origin, with SPA fallback, trusted certificates, protected persistent Data Protection keys and existing cookie/CSRF controls. Vite proxying is Development infrastructure. Cross-origin deployment requires an explicit decision. Production credential delivery remains deferred/fail-closed; no provider was configured. Keep sensitive links/request bodies out of logs/telemetry. Development verification is not Production deployment certification.

Supported HR feature screens and real User Accounts are now implemented; profile/settings beyond existing contracts remain deferred. Existing User-to-Employee linkage determines self-service ownership; client Employee IDs are not proof. First-administrator provisioning remains an explicit operational action. See [current HR status](../modules/hr/README.md) and [release evidence](../releases/v0.5.1.md).
