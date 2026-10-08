# F5 — Employee Directory & Employee 360

Status: ready for product-owner review. Not committed or pushed. Frontend and Development API remain running.

## Scope and existing contracts

F5 replaces the employee placeholder using the frozen HR APIs and V2.1 controls, typography, tokens, feedback, tabs, table viewport, pagination and Radix overlays. Public authentication, session bootstrap, CSRF, V2.3 composition and V2.4 dashboard integration are preserved.

| Existing API                                       | F5 usage                                                                                                      |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `GET /api/employees`                               | Server search, page/pageSize=20, department, designation, employment status and employee IsActive filters     |
| `GET /api/employees/{id}`                          | Quick view, Employee 360 and personal correction initialization                                               |
| `POST /api/employees`                              | Employee plus initial EmploymentRecord; no Identity account/roles                                             |
| `PUT /api/employees/{id}`                          | Personal scalar corrections, retaining employment context and omitted child collections                       |
| `GET /api/employees/{id}/employment-history`       | Actual ordered historical/current records                                                                     |
| `POST /api/employees/{id}/employment-changes`      | Effective-dated context change; omitted IDs retain current values                                             |
| `POST /api/employees/{id}/end-employment`          | Explicit terminal status/date, expected EmploymentRecord ID and linked-account decision/version when required |
| `POST /api/employees/{id}/rehire`                  | New employment period for the same employee                                                                   |
| `GET /api/employees/{id}/account-lifecycle`        | Safe offboarding readiness and existing account version                                                       |
| `GET /api/master-data/{name}?includeInactive=true` | Reference labels, historical resolution and active selections                                                 |

The legacy employee status PATCH permits only consistent no-op requests; F5 does not use it for activation/deactivation. No employee hard-delete API exists and no delete action is offered. Backend date, reference, duplicate-number, history, ownership and concurrency validation remain authoritative.

## Directory and inspection

Search and filters use database-backed pagination. Employee record activity and EmploymentRecord status are separate columns. No Identity account status is inferred from either. Loading announces progress, independent lookup/request failures allow retry, and empty/filtered results offer reset. Query keys include the complete query string; directory parameters live in the URL.

Quick view is a contextual sheet; opening/closing does not navigate, reset filters, change pagination or remount the directory. Radix contains focus, locks background scrolling, supports Escape and returns focus to the invoking button without scrolling. The full profile link uses a real nested route.

Employee 360 provides Overview, Personal & contacts, Employment history, and Teacher profile only when actual teacher data exists. Contacts, addresses and emergency contacts are read from the existing authorized detail DTO. There are no fake document, payroll or workflow tabs. Confidential document/compensation endpoints are not requested or rendered by F5, including for SystemAdmin.

## Forms and lifecycle

Creation, personal correction and lifecycle workflows use full pages. Required fields, whitespace checks and string limits mirror the contract; further validation is shown from server validation responses. FormField provides labels, required semantics, error associations and first-invalid-field focus. Reporting employee selection uses a bounded server search of active employees, with self excluded from selection and backend validation retained.

Commands use the existing API client and fresh CSRF token. Pending buttons/controls prevent repeated submission. Success is shown only after the server confirms the command, and employee queries are invalidated. Field validation stays near fields; request-level/network/server/conflict errors stay on the form without discarding values. A 409 offers a deliberate reload.

Profile PUT retains demographic IDs by resolving existing display names against master data, preserves ProfilePhoto and the current/latest historical employment fields, and omits contact/address/emergency/teacher collections. Ambiguous/missing demographic mappings stop editing instead of guessing or clearing existing values. Historical employment is never rewritten by a lifecycle form.

Employment changes submit only supplied context values. End employment requires confirmation and the existing record ID; when the backend requires a linked-account decision, Security.Manage and an explicit keep/disable decision plus the existing account version are required. No account decision is guessed. Rehire does not activate or create an Identity account.

Dirty/pending forms protect internal route changes with an accessible confirmation and browser unload with the native unload mechanism. Pristine forms leave without a warning. Destructive confirmation and unsaved confirmation are separate workflows; no nested overlays are introduced in normal interaction.

## Capability and preservation boundaries

Employee.Read gates the directory/detail routes. Employee.Manage gates create/edit/lifecycle routes and actions. Dynamic route metadata also protects direct navigation; module context and the Employees active destination persist on nested routes. Authorization never checks role names, user names or QA identifiers. The backend remains authoritative; 401 continues through the unchanged session-loss/cache-clearing mechanism.

SHA-256 inventory before/after implementation confirmed all backend source, regression source, migrations, authentication, API client, providers and V2.4 dashboard source/tests/report unchanged. The only existing shell change is the Employees NavLink prefix match for nested F5 routes; other destinations retain exact matching. No dependencies/configuration/schema/role grants changed. No Git commands were run.

## Verification

| Gate                                         | Result                                                                    |
| -------------------------------------------- | ------------------------------------------------------------------------- |
| Focused F5 frontend tests                    | 31 pass                                                                   |
| Complete frontend suite                      | 193 tests in 11 files pass                                                |
| Production frontend build                    | Pass                                                                      |
| Lint / formatting                            | Pass                                                                      |
| Backend Release build                        | 0 warnings, 0 errors; temporary artifacts avoid disturbing running API    |
| Existing backend/domain/security regressions | 1,024 assertions pass, including D10/D12/D13/D14; no database connections |
| EF pending-model check                       | No pending changes                                                        |
| Whitespace/conflict-marker check             | Pass, performed without Git                                               |

Authenticated Chromium review used the existing qa-systemadmin session via normal login. Directory, Employee 360/history and create form were inspected at 1440×900, 1280×800, 1024×768, 768×1024, 430×932 and 390×844. Mobile profile overflow found during review was corrected by containing grid children and keeping wide tables within their named scroll regions. Desktop/mobile quick-view fit, Escape/focus return, visible blue keyboard focus, field validation and read-only edit/offboarding forms were checked. F5 adds no motion; existing reduced-motion overlay/control rules remain authoritative. OS reduced-motion emulation and Firefox/WebKit were not performed.

The live API returned the existing two employees: DEV-QA-EMPLOYEE and inactive TEST-EMP-001. QA employment history shows its existing current record. Live verification performed reads and an empty create-form validation attempt that did not submit an employee command. No employees, accounts, employment records or operational fixtures were created/updated/deleted. Successful mutation tests used isolated test responses, not the Development database.

Focused coverage includes server paging/search/filtering; loading/empty/retry/404/network failures; read/manage denial; nested shell context; 360/contact/history rendering; create/validation/CSRF/success/refetch/duplicate-submit prevention; safe profile scalar/context retention and latest ended record selection; ambiguous lookup rejection; pristine/dirty navigation; employment change/rehire/end payloads; linked-account decision/version guards; manager search; and session expiry. Existing authentication and dashboard tests also remain passing.

## Known limitations

- Live mutation verification is intentionally not performed: the requested checkpoint prohibits modifying QA employees or creating fake records. Mutation behavior is covered by isolated frontend tests and the unchanged backend regressions.
- The frozen Employee PUT contract has no optimistic version token. F5 does not invent one or claim prevention of every concurrent personal-edit overwrite; it surfaces existing backend conflicts. End employment uses the existing expected IDs/version.
- Demographic IDs are absent from detail responses. Unique name resolution is required for safe editing; otherwise editing stops. A preserved inactive reference may still be rejected by the backend's active-reference validation.
- Existing inconsistent legacy employment states are not repaired here; lifecycle actions remain subject to backend validation. Inactive records with a current employment record are not advertised as ready for rehire/change/end.
- Contact/address/emergency/teacher editing, document management, compensation and operational HR module interfaces remain separate future frontend work. No links imply those placeholder workflows are complete.
- Manual product-owner approval is still required.

## Changed files

Created:

- `frontend/src/features/employees/contracts.ts`
- `frontend/src/features/employees/data.ts`
- `frontend/src/features/employees/directory.tsx`
- `frontend/src/features/employees/employees.css`
- `frontend/src/features/employees/form.tsx`
- `frontend/src/features/employees/presentation.tsx`
- `frontend/src/features/employees/profile.tsx`
- `frontend/src/features/employees/reporting-employee.tsx`
- `frontend/src/test/employees.test.tsx`
- `docs/frontend/F5-EMPLOYEE-DIRECTORY-AND-360.md`

Modified:

- `frontend/src/app/router/navigation.ts`
- `frontend/src/app/router/router.tsx`
- `frontend/src/components/layout/shell-navigation.tsx`

## Review URLs

- Directory: <http://localhost:5175/hr/employees>
- Create: <http://localhost:5175/hr/employees/new>
- Dedicated QA Employee 360: <http://localhost:5175/hr/employees/8422a4e8-faa7-480c-a1ae-4b3347378c34>

F5 EMPLOYEE DIRECTORY & EMPLOYEE 360 — READY FOR PRODUCT OWNER REVIEW. NOT COMMITTED. NOT PUSHED.
