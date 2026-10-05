# F1 â€” Frontend architecture & SIAMIS design system

Date: 2026-10-05. Status: completed for review. No commit or push.

## Summary and inspection

F1 adds a dedicated React/TypeScript frontend, locally owned UI components following shadcn composition, semantic SIAMIS styling, forms, responsive presentation, API/query foundations and a Development showcase. It implements no HR workflows or final application navigation.

Starting HEAD: `dd1a77132c88ad52dfbc366b4f1af944587b17d2`, branch `main`, clean working tree. HR freeze tag targets `74907b66417c8f18e14f116ed27e28e8ce492d45`. Existing repository: four .NET projects in src, established tests, living docs and checkpoint archive. There was no package.json, Node application or approved competing frontend stack. The historical API wwwroot AdminLTE starter remains untouched. No suitable official logo asset was found; SIAMIS / Siam International School text is used.

The actual backend exposes Development Swagger, /api resource routes, sanitized exception ProblemDetails, validation errors and sometimes empty HTTP error bodies. Identity uses HttpOnly Strict cookies, secure Production cookies, per-request account/stamp/role checks, X-CSRF-TOKEN for unsafe requests including login, and /api/auth/csrf. /api/auth/me already exposes safe identity/linkage/capabilities. CORS accepts only explicitly configured origins. Existing launch URLs are HTTPS localhost:7142 / HTTP localhost:5142.

No conflict or unresolved stop condition required backend changes.

## Selected packages and purpose

Installed Node: 22.23.2; npm: 10.9.8. Versions were queried from the npm registry before installation and pinned with a lockfile. TypeScript latest 7.0.2 is outside typescript-eslint's declared <6.1 range, so stable 6.0.3 was selected. Node types use the installed 22.x line. No prereleases, Redux, AdminLTE, grid engine, animation framework or shadcn CLI package were added.

| Package | Exact version | Purpose |
|---|---|---|
| react | 19.3.0 | UI runtime |
| react-dom | 19.3.0 | Browser rendering |
| react-router-dom | 7.18.4 | Route composition |
| @tanstack/react-query | 5.104.1 | Server-state provider |
| react-hook-form | 7.89.0 | Form state |
| zod | 4.6.5 | Typed validation |
| @hookform/resolvers | 5.9.1 | Zod form adapter |
| radix-ui | 1.6.7 | Accessible interactive primitives |
| lucide-react | 1.52.0 | Single icon library |
| clsx | 2.1.1 | Conditional classes |
| tailwind-merge | 3.7.0 | Tailwind class merging |
| class-variance-authority | 0.7.1 | Small button variant contract |
| tailwindcss | 4.3.3 | Semantic styling and responsive utilities |
| @tailwindcss/vite | 4.3.3 | Tailwind build integration |
| eslint | 10.12.0 | Lint engine |
| @eslint/js | 10.0.1 | Baseline JS rules |
| typescript-eslint | 8.71.0 | TypeScript lint adapter |
| eslint-plugin-react-hooks | 7.1.1 | Hook correctness |
| eslint-plugin-react-refresh | 0.5.7 | Fast Refresh boundary checks |
| globals | 17.13.0 | Explicit lint environments |
| prettier | 3.9.9 | Consistent formatting |
| vitest | 5.0.3 | Focused test runner |
| @testing-library/react | 16.3.3 | Component tests |
| @testing-library/jest-dom | 7.0.1 | DOM assertions |
| @testing-library/user-event | 14.6.7 | Accessible interaction tests |
| jsdom | 30.1.2 | DOM test environment |
| @types/react | 19.3.0 | React types |
| @types/react-dom | 19.3.0 | Rendering types |
| @types/node | 22.20.5 | Installed Node 22 type line |
| typescript | 6.0.3 | Strict type checking; supported stable 6.x line |
| vite | 8.3.2 | Development/build tooling |
| @vitejs/plugin-react | 6.1.1 | React Vite integration |

The lockfile also records transitive/automatic peer dependencies. npm installed 334 packages and audited 335 with **0 vulnerabilities**. Radix is the shared primitive package; no business framework is introduced. Compatibility references: [Vite requirements](https://vite.dev/guide/), [Tailwind Vite integration](https://tailwindcss.com/docs/installation/using-vite), [shadcn Vite architecture](https://ui.shadcn.com/docs/installation/vite). Registry engine/peer metadata and successful installation/build are the selected-version evidence.

## Architecture and UI foundation

[Architecture](ARCHITECTURE.md) documents app/providers/router/styles, UI/layout/shared components, feature directories and API/auth/display utilities. Only the design-system feature exists; no empty HR folders were manufactured.

The light theme uses primary #702638, restrained gold #94702d, warm canvas #f8f7f4, white surfaces, neutral foreground and semantic feedback tokens. Colors live centrally in tokens.css. Spacing follows a 4px unit, control/card radii are 6px, and overlays carry the elevation. Local Segoe UI / Noto Sans Thai / Tahoma / Arial fallbacks support English/Thai without font-network requests. Deliberate 12â€“30px type roles and responsive patterns are documented in [Design System](DESIGN-SYSTEM.md).

Primitives include Button, Input, Textarea, Label, Select, Checkbox, RadioGroup, Switch, Badge, Card, Separator, Tabs, Tooltip, Popover, DropdownMenu, Dialog, AlertDialog, Sheet, Toast, Skeleton, Spinner, Alert, Avatar, Breadcrumb, Pagination, Table and EmptyState. PageContainer/Header/Title/Description/Actions, Section, ResponsiveGrid, FormField, StatusBadge and ErrorBoundary supply application patterns.

Buttons have five restrained variants and accessible icon/loading states. StatusBadge is intent-only. Forms use React Hook Form/Zod, associated labels, required markers, helper/error associations, disabled/read-only/submitting states, fieldsets and responsive grids. The demonstration saves nothing.

Table primitives remain small. The sample includes real local sorting, row actions, status, pagination and keyboard focus highlighting. Below 768px it renders intentional readable list cards rather than a squeezed scrolling table. Loading and empty table/section patterns are showcased separately. TanStack Table is unnecessary for this checkpoint.

Tailwind's standard 640/768/1024/1280/1536 breakpoints govern layouts. Page actions wrap, forms collapse, grids reduce columns and overlays stay within the viewport. The development header/drawer is only a showcase shell.

## API, state, errors and display

The fetch foundation supports same-origin or explicit public API base configuration, credentials, fresh CSRF tokens per unsafe command, JSON, browser-boundary FormData, Blob reads, cancellation and 204 responses. API-root-relative destinations are checked before token acquisition. Invalid JSON and HTTP errors normalize safely; arbitrary server diagnostics are not exposed. No tokens enter localStorage.

Query defaults: 60-second stale time, no focus refetch, at most one eligible query retry, no retry on HTTP client/authorization failures, no mutation retry. Component state remains local. Capability checks consume session capabilities and control UX only; backend authorization/ownership remains authoritative.

Error representations cover validation, unauthorized, forbidden, missing, conflict, server, request and network cases. Showcase feedback demonstrates access/recovery, loading, empty and error patterns. Generic render failures expose a safe fallback.

Date-only display preserves its calendar day; explicit-offset instants default to Asia/Bangkok. Integer/decimal/percentage/THB formatting is display-only. No Payroll, statutory, Leave or Attendance algorithms exist in React.

## Security, accessibility and showcase

/dev/ui is gated by import.meta.env.DEV with a dynamic import. The production build omits both route and showcase content; root Production content is a foundation placeholder. All samples are labeled as development data and make no API requests.

Keyboard focus, form associations, named icon actions, focus-managed Radix overlays, 44px targets, semantic tables/mobile lists and reduced motion are established. Text token pairs were checked: primary 10.38:1, accent 4.55:1, body 12.91:1, muted 6.11:1, success 6.43:1, warning 5.55:1, danger 5.86:1, info 6.02:1. These checks are a foundation, not a full screen-reader/cross-browser certification.

The same-origin Development proxy uses a loopback HTTPS target and retains TLS verification. Local Node certificate trust must be configured explicitly if needed. No backend CSRF, CORS, cookies, capabilities or role grants were weakened. No backend/database/API mutation was performed.

## Verification

| Check | Result |
|---|---|
| npm install | PASS; pinned lockfile; 0 audit vulnerabilities |
| npm run build | PASS; strict TypeScript, successful Production output |
| npm run lint | PASS; 0 errors / 0 warnings |
| npm run test | PASS; 20 focused tests, 1 file |
| npm run format:check | PASS |
| Responsive browser checks | PASS at 1440, 1280, 1024, 768, 430, 390 |
| Overflow / mobile tables | Zero horizontal overflow; tables at md+, cards below md |
| Dialog / sheet / form | Focus containment/restoration, viewport fit, validation and notification verified at all six widths |
| Browser errors / API requests | None / none |
| Production showcase exclusion | Confirmed in built JS |
| Backend Release build | PASS; 0 warnings / 0 errors |
| EF pending-model changes | PASS; no changes since last migration |
| Backend source and existing tests | Unchanged against starting HEAD; src unchanged against hr-backend-v1 |
| git diff --check | PASS |
| Documentation / path / secret checks | Relative references valid; no private credentials or developer-specific living-doc paths |

The browser-control surface failed to initialize; bundled Playwright with installed headless Chrome supplied actual responsive checks and screenshots. Desktop/mobile screenshots, forms, dialogs and drawers were visually inspected. Generated screenshots/results and the temporary visual runner are ignored local test artifacts. No browser package or browser software was installed.

The first visual run had an ambiguous notification selector because Radix also announces the toast; the selector was corrected and all widths passed. A route-module Fast Refresh lint warning was corrected in frontend wiring. Final tests also cover disabled/loading buttons, form associations, ProblemDetails categories, multipart/binary behavior, fresh CSRF, cancellation, capability checks, retry defaults, malformed JSON, unsafe destinations and date-only safety.

## Remaining F2/F3 and deployment boundaries

- Final application shell/navigation, login/recovery/session UX, protected-route UX and real feature queries/workflows remain deferred.
- Existing /api/auth/me is sufficient for the capability foundation; no new backend identity endpoint is required.
- End-to-end authenticated browser integration is deferred with login, including account-change cache clearing and real API field mapping. F1 tests security request behavior with mocked fetch and does not create accounts or HR data.
- Production static hosting/SPA fallback, same-origin routing, TLS and certificate deployment remain separate configuration work. A separate-origin deployment requires explicit approved CORS/cookie decisions.
- Real feature response validation, binary download filename handling/object-URL cleanup and feature-specific responsive tables come with those features.
- Official logo and any licensed/self-hosted typography asset remain required decisions. Dark mode and broader accessibility/browser certification are deferred.
- Frozen HR semantics remain authoritative. No migration, financial calculation, employment, Leave, Attendance, Identity, authorization or document behavior changed.

## Complete changed-file list

Modified: .gitignore, README.md, docs/README.md. Created files follow; ignored runtime/test artifacts are excluded.


```text
docs/frontend/ARCHITECTURE.md
docs/frontend/DESIGN-SYSTEM.md
docs/frontend/F1-REPORT.md
docs/frontend/README.md
frontend/.env.example
frontend/.prettierignore
frontend/.prettierrc.json
frontend/eslint.config.js
frontend/index.html
frontend/package-lock.json
frontend/package.json
frontend/src/app/providers/notifications.tsx
frontend/src/app/providers/providers.tsx
frontend/src/app/providers/query-client.ts
frontend/src/app/router/router.tsx
frontend/src/app/styles/tokens.css
frontend/src/components/layout/page.tsx
frontend/src/components/shared/error-boundary.tsx
frontend/src/components/shared/form-field.tsx
frontend/src/components/shared/status-badge.tsx
frontend/src/components/ui/button.tsx
frontend/src/components/ui/controls.tsx
frontend/src/components/ui/feedback.tsx
frontend/src/components/ui/navigation.tsx
frontend/src/components/ui/overlays.tsx
frontend/src/components/ui/table.tsx
frontend/src/features/design-system/demo-form.tsx
frontend/src/features/design-system/demo-table.tsx
frontend/src/features/design-system/showcase.tsx
frontend/src/lib/api/client.ts
frontend/src/lib/api/errors.ts
frontend/src/lib/auth/capabilities.ts
frontend/src/lib/utils/cn.ts
frontend/src/lib/utils/format.ts
frontend/src/main.tsx
frontend/src/test/foundation.test.tsx
frontend/src/test/setup.ts
frontend/tsconfig.json
frontend/vite.config.ts
frontend/vitest.config.ts
```
