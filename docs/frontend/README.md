# Frontend

F1 provides a React/TypeScript design system and integration foundation. HR business workflows, login, the dashboard and the final shell are deferred to F2/F3. The backend remains frozen at `hr-backend-v1`.

## Run locally

Use Node 22.22.2 or newer within the supported engines in [package.json](../../frontend/package.json). From the repository root:

```powershell
cd frontend
npm ci
npm run dev
```

Open `http://localhost:5173/dev/ui`. This development-only showcase uses static samples and performs no API requests or database mutations. Production builds exclude the showcase route and its module.

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

Read [architecture](ARCHITECTURE.md), [design system](DESIGN-SYSTEM.md) and the [F1 report](F1-REPORT.md). The official school logo remains required; the preview uses text branding.
