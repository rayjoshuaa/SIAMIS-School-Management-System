# Frontend architecture

## Structure

| Location under frontend/src | Responsibility |
|---|---|
| `app/providers` | QueryClient, notifications and shared primitive providers |
| `app/router` | Route composition; Development-only `/dev/ui` |
| `app/styles` | Semantic design tokens and global accessibility defaults |
| `components/ui` | Locally owned accessible primitives, following the shadcn composition approach |
| `components/layout` | Common page spacing, headers, actions, sections and grids |
| `components/shared` | StatusBadge, form-field wiring and safe render fallback |
| `features/design-system` | Development samples; excluded from Production |
| `lib/api` | Credentialed fetch, CSRF and safe normalized errors |
| `lib/auth` | Capability-based presentation helper |
| `lib/utils` | Class merging and display formatting |
| `test` | Small Vitest/Testing Library foundation |

Future approved features belong under their own `features/<feature>/` directory. F1 creates no empty HR feature folders or fake domain services. No Redux, enterprise grid or animation framework is installed. Radix provides keyboard/focus behavior; component source belongs to this project rather than a remote theme package. No AdminLTE dependencies are used.

## State and requests

TanStack Query owns future server state; feature query keys and fetches come with those features. Query defaults use a 60-second stale time, no window-focus refetch, at most one retry for eligible failures and no retry for HTTP client/authorization failures. Mutations never retry automatically. Local interaction state stays in components. Forms use React Hook Form and Zod.

The API client defaults to same-origin, includes cookies and accepts AbortSignal. It obtains a new `/api/auth/csrf` token before every unsafe request, including future login/logout commands; callers cannot supply provenance or security headers. JSON and FormData bodies are supported, with browser-generated multipart boundaries, as are Blob downloads and 204 responses. Future download UI must revoke object URLs and use a safe display filename.

ProblemDetails field errors are retained for validation; arbitrary server detail/extensions/stacks are not presented. UI errors distinguish validation, unauthenticated, forbidden, missing, conflict, server, request and network failures. Feature code should map field names deliberately rather than assuming every server key matches a form field.

`/api/auth/me` already exposes authenticated identity, optional Employee linkage and capabilities. `can(session, capability)` is a presentation helper only; server authorization and ownership remain authoritative. No role-name inference, localStorage token or authentication workflow is introduced.

## Integration boundaries

The backend uses Strict HttpOnly cookies, CSRF and a deny-by-default explicit CORS configuration. A same-origin proxy preserves these contracts. The Development proxy targets loopback only and verifies TLS. Production hosting, SPA fallback, certificate trust and deliberate separate-origin decisions remain deployment work.

Date-only display uses its calendar value unchanged; explicit-offset instants display in Asia/Bangkok by default. Number/money utilities format backend values only and never calculate payroll, statutory, Leave or Attendance results. Backend precision and totals remain authoritative; JavaScript display values do not replace decimal-domain values.

See [backend authorization](../architecture/AUTHORIZATION.md), [security](../architecture/SECURITY.md) and [frontend setup](README.md).
