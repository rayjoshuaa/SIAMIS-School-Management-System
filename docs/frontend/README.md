# Frontend

Current features: premium V3 foundation, shell/authentication, HR Dashboard, Employee Directory/360, employment/account lifecycle, User Accounts, Attendance Management and employee-owned clocking. [HR status](../modules/hr/README.md) distinguishes implemented functionality from planned modules.

## Run locally

Verified release toolchain: **Node 22.23.2 / npm 10.9.8**. Other supported engine ranges appear in [package.json](../../frontend/package.json), but were not verified for this release.

```powershell
cd frontend
npm ci
npm run dev
```

Default: `http://localhost:5173/login`. Current review uses `npm run dev -- --port 5175` and `http://localhost:5175/login`. Vite selects a strict port; it does not silently fall back. `/dev/ui` is an independent Development-only synthetic showcase, excluded from Production.

## API and accounts

Start the API separately following [Development setup](../deployment/DEVELOPMENT.md). Vite proxies same-origin `/api` to loopback HTTPS `https://localhost:7142`, with certificate verification. If Node needs explicit trust, set `NODE_EXTRA_CA_CERTS` to your public localhost certificate before starting Vite. Do not disable TLS, cookie or CSRF protections.

Copy [the environment example](../../frontend/.env.example) to local `.env` only when overrides are needed. Vite variables are public bundled configuration, never secrets. Production requires approved HTTPS hosting, SPA fallback and API routing; the Development proxy is not Production infrastructure.

Existing QA accounts are local identities, not startup seeds. Sign in privately through normal Identity authentication. User Accounts requires `Security.Manage`; provisioning remains passwordless followed by secure activation. Do not run the superseded QA PowerShell provisioner or consume old credential files.

[Authentication](AUTHENTICATION.md) is the current session contract. Public login remains visible if the initial probe cannot reach the API. Protected content remains fail-closed. Same-context refresh preserves page state; unavailable authenticated revalidation blocks protected access/commands while retaining drafts in RAM. No browser draft or credential persistence was added.

## Verification

```powershell
npx tsc -p tsconfig.json --noEmit
npm run test -- --maxWorkers=2
npm run lint
npm run format:check
npm run build
```

Stage 2 verified a genuine isolated clean installation and **386 tests / 23 suites**, TypeScript, lint, formatting and production build. See [verification evidence](../releases/v0.5.1.md#verification-evidence). Stage 3 does not claim a fresh application regression run.

Use [architecture](ARCHITECTURE.md) and [design authority](DESIGN-SYSTEM.md). Earlier F1/F2/F3 placeholder and account-cleanup statements remain historical evidence.
