# Frontend

F1 provides the design system, F2 the school shell and F3 real authentication/session/account access. HR screens and dashboard analytics remain deferred. The backend remains frozen at `hr-backend-v1`.

## Run locally

Use Node 22.22.2 or newer within the supported engines in [package.json](../../frontend/package.json). From the repository root:

```powershell
cd frontend
npm ci
npm run dev
```

Open `http://localhost:5173/`. Anonymous users see login; authenticated users see the school shell with effective capability navigation. HR feature pages and dashboard values remain placeholders. Planned school modules remain unavailable.

F3 removes the Development capability switch. Only sidebar preference persists in `siamis.ui.sidebar.v1`; credentials/tokens/capabilities do not persist. Real account sign-out uses the backend; profile/settings remain deferred.

`http://localhost:5173/dev/ui` retains the independent Development-only F1 showcase without requiring authentication or API calls. It is excluded from Production.

```powershell
npm run build
npm run lint
npm run test
npm run format:check
```

## API connection

Copy [`.env.example`](../../frontend/.env.example) to a local `.env` if overrides are needed. `VITE_API_BASE_URL` is public, bundled configuration; never put secrets in Vite variables. Empty uses same-origin `/api` paths. The Vite Development proxy forwards `/api` to the existing loopback HTTPS API at `https://localhost:7142`, with certificate verification enabled.

Run the API separately using its existing launch profile. Node must trust the local development certificate: supply its public certificate as `NODE_EXTRA_CA_CERTS` before starting Vite if Node does not already trust it. Do not disable backend HTTPS, CSRF or cookie security. No certificate is created/exported automatically by F1. The showcase itself does not require the API or a certificate.

Production should serve the application and API under one HTTPS origin with SPA routing configured by the deployment host. The Vite Development proxy is not Production infrastructure. A separate-origin deployment requires explicit approved CORS/cookie configuration; setting a public base URL alone does not authorize it.

Read [architecture](ARCHITECTURE.md), [design system](DESIGN-SYSTEM.md), the historical [F1 report](F1-REPORT.md), and the [F2 report](F2-REPORT.md). The official school logo remains required; the shell uses approved text branding.

See [authentication/setup](AUTHENTICATION.md) and [F3 report](F3-REPORT.md). No permanent test account remains; first-administrator provisioning requires explicit approval.
