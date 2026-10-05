# Frontend

F1 provides the React/TypeScript design system and integration foundation. F2 adds the responsive application shell and module navigation. Authentication belongs to F3; HR workflows and dashboard analytics remain deferred. The backend remains frozen at `hr-backend-v1`.

## Run locally

Use Node 22.22.2 or newer within the supported engines in [package.json](../../frontend/package.json). From the repository root:

```powershell
cd frontend
npm ci
npm run dev
```

Open `http://localhost:5173/` to review the school dashboard structure and application shell. The dashboard shows honest empty statistic, enrollment, population, activity, event and action regions. HR Overview, Employees, Leave, Attendance, Payroll, Documents and Accounts & Security remain placeholders. School Management expands to an overview landing and planned destinations; the other school-platform modules are unavailable entries. No business data or API requests are introduced.

The clearly marked Development preview lets you select all navigation capabilities, Employee read only, or no capabilities. This changes menu visibility only and never authenticates a user. Desktop sidebar preference alone persists in `siamis.ui.sidebar.v1`; credentials and capabilities do not persist.

`http://localhost:5173/dev/ui` retains the F1 development-only showcase. Production builds exclude the showcase and Development navigation provider/controls; the account area displays “Session not connected” with no capabilities until F3 is connected. Disabled account actions do not implement login/logout.

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
