# Frontend architecture

## Structure

| Location under frontend/src | Responsibility |
|---|---|
| `app/providers` | QueryClient, notifications and shared primitive providers |
| `app/router` | Nested shell routes and shared navigation metadata; Development-only `/dev/ui` |
| `app/styles` | Semantic design tokens and global accessibility defaults |
| `components/ui` | Locally owned accessible primitives, following the shadcn composition approach |
| `components/layout` | Application shell, navigation, account presentation, common page spacing and actions |
| `components/shared` | StatusBadge, form-field wiring and safe render fallback |
| `features/design-system` | Development samples; excluded from Production |
| `lib/api` | Credentialed fetch, CSRF and safe normalized errors |
| `lib/auth` | Capability-based presentation helper and replaceable navigation session context |
| `hooks` | Responsive media-query subscription |
| `features/shell` | Module landing placeholders without business data |
| `lib/utils` | Class merging and display formatting |
| `test` | Small Vitest/Testing Library foundation |

Future approved features belong under their own `features/<feature>/` directory. F1 creates no empty HR feature folders or fake domain services. No Redux, enterprise grid or animation framework is installed. Radix provides keyboard/focus behavior; component source belongs to this project rather than a remote theme package. No AdminLTE dependencies are used.

## State and requests

TanStack Query owns future server state; feature query keys and fetches come with those features. Query defaults use a 60-second stale time, no window-focus refetch, at most one retry for eligible failures and no retry for HTTP client/authorization failures. Mutations never retry automatically. Local interaction state stays in components. Forms use React Hook Form and Zod.

The API client defaults to same-origin, includes cookies and accepts AbortSignal. It obtains a new `/api/auth/csrf` token before every unsafe request, including future login/logout commands; callers cannot supply provenance or security headers. JSON and FormData bodies are supported, with browser-generated multipart boundaries, as are Blob downloads and 204 responses. Future download UI must revoke object URLs and use a safe display filename.

ProblemDetails field errors are retained for validation; arbitrary server detail/extensions/stacks are not presented. UI errors distinguish validation, unauthenticated, forbidden, missing, conflict, server, request and network failures. Feature code should map field names deliberately rather than assuming every server key matches a form field.

`/api/auth/me` already exposes authenticated identity, optional Employee linkage and capabilities. `can(session, capability)` is a presentation helper only; server authorization and ownership remain authoritative. No role-name inference or browser-stored authentication token is introduced.

## Integration boundaries

### F2 shell and navigation

`AppRouter` lazy-loads `ShellEntry`, which composes the replaceable session boundary with `AppShell`. Nested feature routes render in its Outlet. The frame owns sidebar, top header, mobile navigation, breadcrumbs and the F1 PageHeader/PageContainer. The existing ErrorBoundary accepts an optional shell recovery fallback and resets on route changes. Loading and not-found states use F1 primitives. `/dev/ui` stays outside the shell and is omitted from Production.

`app/router/navigation.ts` owns each destination's path, label, breadcrumb, description, icon, shallow group, capability, visibility and module status. Router registration, menus and breadcrumbs consume this registry. A small adjacent module registry describes the wider school platform; planned School Management children have labels only, without speculative business URLs or capabilities. Only the existing nine routes are registered. `PageActionGroup` keeps a primary action visible and puts secondary actions in a mobile overflow menu. Placeholders have no business actions.

The revised school dashboard uses a reusable compact StatisticCard and empty regions for enrollment, student population, recent activities, upcoming events and quick actions. It displays no invented counts, chart data or actionable workflows. The header reserves presentational search and academic-session regions, explicitly unavailable/unconnected; no session switching exists.

At 1280px and above, the sidebar defaults to expanded (248px); at 768–1279px it defaults to a 72px icon rail. The rail shows top-level module icons only; choosing HR or School Management expands the sidebar to show readable children. Planned modules remain unavailable. A persisted expanded/collapsed preference overrides defaults. Below 768px there is no permanent sidebar. A controlled Radix modal drawer traps focus, locks background scrolling, closes on navigation and transfers focus to content; Escape/close returns focus to the trigger. Sidebar navigation scrolls independently only when needed, including all 16 planned School Management destinations; content uses normal document scrolling. The controlled left drawer composes the same Radix foundation because the F1 general Sheet is an uncontrolled right overlay.

### F3 authentication integration

The application route branch wraps AuthProvider and SessionBoundary; `/dev/ui` remains a separate Development-only route without auth bootstrap. AuthProvider consumes the safe `/api/auth/me` DTO and drives real navigation/account identity. ProtectedRoutes guards session admission; CapabilityRoute reads the same metadata as navigation and shows a safe forbidden state. ShellSession is now an adapter from real session data, and the Development capability provider is deleted.

Auth forms are lazy-loaded in a shared AuthLayout, keyed by mode to avoid retaining success/password state across routes. The API client centrally reports protected-request 401 with identity-transition epoch protection, preserving existing CSRF and ProblemDetails behavior. Query data is cancelled/cleared on session loss, logout and bootstrap revalidation. Safe return paths are restricted to registered application destinations. Credential links are scrubbed before router initialization, kept only in RAM and excluded from referrers. See [authentication](AUTHENTICATION.md) for contracts, setup and deployment boundaries. Backend authorization and User-to-Employee ownership remain authoritative; no backend/session contract gap was found.

The backend uses Strict HttpOnly cookies, CSRF and a deny-by-default explicit CORS configuration. A same-origin proxy preserves these contracts. The Development proxy targets loopback only and verifies TLS. Production hosting, SPA fallback, certificate trust and deliberate separate-origin decisions remain deployment work.

Date-only display uses its calendar value unchanged; explicit-offset instants display in Asia/Bangkok by default. Number/money utilities format backend values only and never calculate payroll, statutory, Leave or Attendance results. Backend precision and totals remain authoritative; JavaScript display values do not replace decimal-domain values.

See [backend authorization](../architecture/AUTHORIZATION.md), [security](../architecture/SECURITY.md) and [frontend setup](README.md).
