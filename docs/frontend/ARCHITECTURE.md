# Frontend architecture

React, TypeScript and Vite remain the engine. React Router separates public authentication and protected application routes. TanStack Query owns remote state; React Hook Form and Zod handle forms. Owned controls use Tailwind CSS, Radix UI and Lucide with the [premium foundation](DESIGN-SYSTEM.md).

## Responsibilities

- `frontend/src/app`: providers, capability-aware routing, shell integration and semantic styles.
- `frontend/src/lib`: API normalization, session events and shared utilities.
- `frontend/src/components`: shared controls, overlays, fields and workspace composition.
- `frontend/src/features`: supported authentication, HR, employee, administration, attendance and clocking workspaces; isolated Development showcase.
- `frontend/src/test`: semantic, workflow, authorization and regression coverage.

The shell serves all SIAMIS modules. Navigation filters effective capabilities, never role names. Backend authorization remains authoritative. Public authentication screens sit outside the shell. Placeholders do not imply implemented school/finance/learning modules.

## Page and session state

Supported shareable filters/pagination belong in the URL. Queries represent server facts. Unsaved edits remain local component state; there is no generic persistent draft store or browser-tab persistence. [Authentication lifecycle](AUTHENTICATION.md#session-loss-and-caches) governs state preservation and fail-closed refresh. A changed security context resets protected state; same-context refresh does not remount it.

Mutations use existing API contracts, centralized CSRF and normalized validation/conflict errors. Employee registration is separate from account provisioning. Registration retries retain one key for a logical submission; a genuinely new registration receives a new key. See [idempotency](F5.1E1-EMPLOYEE-REGISTRATION-IDEMPOTENCY.md).

Reuse content frames, page headers and overlays without forcing every workflow into cards. Production features show real bounded data or honest loading/empty/error states. Only the isolated showcase uses synthetic demonstration data. See [setup](README.md).
