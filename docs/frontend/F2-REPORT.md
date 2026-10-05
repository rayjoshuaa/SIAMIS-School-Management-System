# F2 — application shell and responsive navigation

## Summary and starting point

Completed on 2026-10-05 on top of F1. Starting HEAD: `2b739bbf07367d044ea13a052fc1584190c81894`; working tree was clean. Before implementation, F1 Production build and all 20 existing tests passed. The F1 palette, tokens, typography, controls, forms, tables, API utilities and package versions remain unchanged. No packages were added. No commit or push was performed.

Review locally at `http://localhost:5173/` using `cd frontend; npm run dev`. The existing Development server is available for review. `/dev/ui` retains the F1 showcase.

## Shell, routes and metadata

The lazy `ShellEntry` composes a replaceable session boundary and `AppShell`. Nested React Router destinations render in the Outlet beneath the top header, metadata-derived breadcrumbs and F1 PageHeader. The reusable frame contains no HR business logic. Main content is fluid up to 100rem. `PageActionGroup` supports visible primary actions and mobile secondary-action overflow; placeholders do not introduce business actions.

One small registry owns paths, labels, descriptions, icons, breadcrumbs, navigation groups, visibility, capability requirements and foundation/planned status. It drives router registration and navigation. HR has one expandable group; School Management demonstrates another module. Future Academics/Finance entries can be added when approved, without speculative screens now.

| Route | Menu capability | Content |
|---|---|---|
| `/` | None | Structured school dashboard with empty data regions |
| `/hr` | Reporting.Read | HR overview placeholder |
| `/hr/employees` | Employee.Read | Employees placeholder |
| `/hr/leave` | Leave.Read | Leave placeholder |
| `/hr/attendance` | Attendance.Read | Attendance placeholder |
| `/hr/payroll` | Payroll.Read | Payroll placeholder |
| `/hr/documents` | HRDocuments.Read | Documents placeholder |
| `/hr/security` | Security.Manage | Accounts & Security placeholder |
| `/school-management` | None | Planned module landing |
| `/dev/ui` | Development only | Preserved F1 showcase |
| Unmatched path | None | Safe 404 with Dashboard recovery |

Breadcrumbs derive from the same metadata; the HR parent is linked only when its capability is available. Mobile shows the current breadcrumb. Document titles follow the route. A rendering error preserves the frame and shows safe recovery without exception details, using a small optional-fallback extension to the existing F1 ErrorBoundary. Route navigation resets that boundary. Suspense uses a restrained accessible skeleton/loading state.

## Responsive behavior and accessibility

- Desktop at 1280px and above defaults to a 248px expanded light sidebar. Tablet at 768–1279px defaults to a 72px icon rail. Both allow explicit expansion/collapse.
- Only `siamis.ui.sidebar.v1` persists locally. Storage failure is tolerated; no identity, credentials or capabilities are stored there.
- Below 768px the permanent sidebar disappears. A controlled Radix left drawer traps focus, locks background scrolling and closes after navigation. Selection focuses main content; Escape/close returns focus to the trigger. Crossing the desktop-navigation breakpoint resets the drawer.
- Sidebar navigation scrolls when needed; its collapse control remains reachable at short heights. Main content uses ordinary document scrolling and a sticky header.
- Active links combine border, surface, font weight and `aria-current`; HR parent state is also announced. Icon links retain accessible names and focusable tooltips. Skip-to-content, visible focus, 44px controls and labelled modal/menu controls are present.
- Long/Thai account names remain available in full through accessible labels and the account menu. No official logo exists in the inspected repository; one replaceable Brand component uses approved SIAMIS text branding.

Visual review found and corrected a Radix Slot/React Router class-callback composition issue in the collapsed rail. Classes are now resolved before passing through the tooltip trigger; regression assertions cover the layout and correct active destination. The controlled navigation drawer composes existing Radix primitives because F1's general Sheet is an uncontrolled right overlay. No second primitive library was added.

## Account, capabilities and F3 boundary

Development-only lazy context supplies an explicitly unauthenticated preview, with all navigation capabilities, Employee read only and no-capability modes. It is disposable and easy to replace. Account actions are visibly unavailable and disabled. No login/logout, fake notifications, business data or API requests were implemented.

Production excludes the preview provider/controls and `/dev/ui`. It shows an unconnected session with no capabilities. Capability filtering uses actual backend names; `Security.Manage` is the existing security capability, not `Security.Read`. Roles never determine menu visibility. Hiding a link is UX only: direct placeholder routes are not authorization gates and expose no protected data.

No backend integration gap or stop condition was found. `/api/auth/me` already exposes user identity, optional Employee linkage, roles, capabilities and account-state fields. F3 must connect bootstrap/loading/expired/forbidden states, authenticated context, direct-route protection, account actions and identity-sensitive query-cache clearing. It must preserve HttpOnly cookies, CSRF, backend ownership and User-to-Employee self-service linkage. Real HR pages, dashboard analytics, notifications and official logo insertion remain deferred.

## Verification

| Check | Result |
|---|---|
| F1 starting baseline | Build PASS; 20/20 tests |
| Frontend Production build | PASS; no build warnings |
| ESLint | PASS; zero warnings |
| Prettier check | PASS |
| Vitest | 37/37 PASS: existing 20 plus 17 shell tests |
| Browser widths | 1440, 1280, 1024, 768, 430, 390px PASS |
| Browser route navigation | Nine destinations at each width; 54 transitions PASS |
| Desktop/tablet | Expanded/collapsed, active state, focus tooltip, no horizontal overflow PASS |
| Mobile | Modal, background lock, navigation closure/content focus, Escape focus restoration PASS |
| Account/capability/404 | Disabled actions, filtered links, safe recovery PASS |
| Short viewports | All six widths at 540px height PASS |
| Keyboard | Skip link, drawer focus trap, close restoration PASS |
| Mobile reopen/resize | Same-route selection followed by Escape, open-drawer desktop resize and return to mobile PASS |
| Production boundary | Preview absent, empty capabilities, `/dev/ui` unavailable PASS |
| API calls/browser exceptions | Zero in the browser verification |
| Backend Release build | PASS; 0 warnings, 0 errors |
| EF pending-model check | No changes since last migration |
| git diff --check | PASS |

Unit coverage includes metadata, capability filtering, parent/child active states, group interaction, icon rail classes, persistence, mobile navigation, long Thai names, page actions, safe loading/404 and rendering-error recovery. Headless Chrome exercised the real Vite application; screenshots were reviewed for desktop, tablet rail, mobile drawer and account menu. This is focused verification, not a full screen-reader certification. Ignored `frontend/test-results/` contains local browser evidence and helpers, not new project dependencies or deliverables.

Backend source under `src/` matches `hr-backend-v1`; no backend functional files, migrations, schema or database data changed. Backend tests and solution configuration also match the starting HEAD. The existing post-freeze test-harness report-path cleanup differs from the tag and was not changed by F2. The F1 report is preserved. No secrets were added; no database fixtures were needed.

## Visual / information-architecture revision

The technical baseline above was accepted; the 2026-10-05 product revision broadens the shell's school-system presentation while preserving F1 and all backend boundaries.

- Sidebar now describes the full platform: School Management, Admissions & CRM, Human Resources, Accounting & Finance, Teacher Learning, Projects & Tasks, Reports and System Administration, plus Dashboard.
- School Management expands to the existing overview landing and 16 planned destinations. These are disabled metadata labels, not business routes or fake implementations. Other future modules are unavailable top-level entries with restrained indicators. The original nine application routes remain the only registered destinations.
- Expanded navigation uses compact 44px rows and shallow indentation. The collapsed rail shows only top-level module icons; choosing HR or School Management expands the readable sidebar. HR child capability filtering, current-module state and route ownership remain unchanged.
- The 64px school header adds a sidebar control, an explicitly unconnected academic-session context and a presentational unavailable-search region on wide screens. No year is invented and no session switching/search workflow exists. Account names remain fully accessible while visible header text reduces at smaller widths.
- Dashboard has four reusable compact empty statistic cards and regions for enrollment, student population, activity, events and quick actions. All values are unavailable; no fabricated numbers, chart data, permitted actions or chart dependency were introduced. Layout is four/two/one columns as space allows; mobile panels stack.
- No reference graphics or assets were copied. This attachment contained the written revision brief, without the referenced screenshot files; visual direction was implemented from that brief using original F1 primitives.

Final verification: frontend build/lint/format PASS; 37/37 tests PASS. Headless browser checks repeated at 1440, 1280, 1024, 768, 430 and 390px, covering dashboard composition, expanded/collapsed navigation, School Management and HR groups, long independently scrolling mobile navigation, header/account/academic context, all nine routes, capability filtering, safe 404 and 540px-tall viewports. Zero horizontal overflow, browser exceptions or API calls. Production excludes Development controls and `/dev/ui`; keyboard skip link, focus trap/restoration, repeated drawer opening and responsive drawer reset pass. Backend Release remains 0 warnings/0 errors; EF reports no pending model changes; diff check passes. No backend/database/authentication changes, commit or push.

New tests cover the whole module registry, planned destination handling without business routes, readable collapsed navigation, long mobile School Management navigation, honest dashboard empty states and unconnected academic context. Meaningful original shell and foundation tests remain in place.

## Complete changed-file list

Modified:

- `README.md`
- `docs/frontend/README.md`
- `docs/frontend/ARCHITECTURE.md`
- `docs/frontend/DESIGN-SYSTEM.md`
- `frontend/src/app/router/router.tsx`
- `frontend/src/components/shared/error-boundary.tsx`

Created:

- `docs/frontend/F2-REPORT.md`
- `frontend/src/app/providers/development-navigation.tsx`
- `frontend/src/app/router/navigation.ts`
- `frontend/src/components/layout/account-menu.tsx`
- `frontend/src/components/layout/app-shell.tsx`
- `frontend/src/components/layout/brand.tsx`
- `frontend/src/components/layout/mobile-navigation.tsx`
- `frontend/src/components/layout/page-action-group.tsx`
- `frontend/src/components/layout/shell-entry.tsx`
- `frontend/src/components/layout/shell-navigation.tsx`
- `frontend/src/components/layout/shell-states.tsx`
- `frontend/src/features/shell/placeholder.tsx`
- `frontend/src/features/shell/dashboard.tsx`
- `frontend/src/components/shared/statistic-card.tsx`
- `frontend/src/hooks/use-media-query.ts`
- `frontend/src/lib/auth/navigation-session.tsx`
- `frontend/src/test/shell.test.tsx`

F2 stops here. Authentication and feature workflows require their next approved scope.
