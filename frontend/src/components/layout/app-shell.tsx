import { lazy, Suspense, useState, useEffect, type ReactNode } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { PanelLeftClose, PanelLeftOpen, Search, CalendarDays } from 'lucide-react';
import { Button } from '../ui/button';
import { Select } from '../ui/controls';
import { Badge } from '../ui/feedback';
import { PageContainer, PageHeader, PageTitle, PageDescription } from './page';
import { Brand } from './brand';
import { AccountMenu } from './account-menu';
import { MobileNavigation } from './mobile-navigation';
import { ShellNavigation } from './shell-navigation';
import { ShellLoading, ShellError } from './shell-states';
import { useMediaQuery } from '../../hooks/use-media-query';
import { useNavigationSession } from '../../lib/auth/navigation-session';
import { activeRoute, breadcrumbs, paths } from '../../app/router/navigation';
import { cn } from '../../lib/utils/cn';
import { PageActionGroup } from './page-action-group';
import { ErrorBoundary } from '../shared/error-boundary';
const DevelopmentNavigation = import.meta.env.DEV
  ? lazy(() => import('../../app/providers/development-navigation'))
  : null;
const preferenceKey = 'siamis.ui.sidebar.v1';
export function ShellSession({ children }: { children: ReactNode }) {
  return DevelopmentNavigation ? (
    <Suspense fallback={<ShellLoading />}>
      <DevelopmentNavigation>{children}</DevelopmentNavigation>
    </Suspense>
  ) : (
    children
  );
}
export function AppShell({ actions }: { actions?: ReactNode }) {
  const { pathname } = useLocation();
  const session = useNavigationSession();
  const large = useMediaQuery('(min-width: 1280px)');
  const desktopNavigation = useMediaQuery('(min-width: 768px)');
  const [preference, setPreference] = useState<boolean | null>(() => {
    try {
      const stored = localStorage.getItem(preferenceKey);
      return stored === 'collapsed' ? true : stored === 'expanded' ? false : null;
    } catch {
      return null;
    }
  });
  const collapsed = preference ?? !large;
  function remember(value: boolean) {
    setPreference(value);
    try {
      localStorage.setItem(preferenceKey, value ? 'collapsed' : 'expanded');
    } catch {
      /* Optional UI preference. */
    }
  }
  function toggle() {
    remember(!collapsed);
  }
  useEffect(() => {
    document.title = `${activeRoute(pathname)?.label ?? 'Page not found'} · SIAMIS`;
  }, [pathname]);
  const route = activeRoute(pathname);
  const trail = breadcrumbs(pathname, session.capabilities);
  return (
    <div
      className={cn(
        'min-h-dvh md:grid',
        collapsed ? 'md:grid-cols-[4.5rem_minmax(0,1fr)]' : 'md:grid-cols-[15.5rem_minmax(0,1fr)]',
      )}
    >
      <a
        href="#main"
        className="sr-only fixed top-2 left-2 z-50 rounded-md bg-surface px-4 py-3 focus:not-sr-only"
      >
        Skip to content
      </a>
      <aside
        aria-label="Workspace sidebar"
        className="sticky top-0 hidden h-dvh flex-col border-r border-border bg-sidebar md:flex"
      >
        <div className="flex h-16 shrink-0 items-center px-4">
          <Brand collapsed={collapsed} />
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
          <ShellNavigation collapsed={collapsed} onExpand={() => remember(false)} />
        </div>
        <div className="border-t border-border p-3">
          <Button
            icon={collapsed}
            variant="ghost"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            aria-expanded={!collapsed}
            onClick={toggle}
            className="w-full justify-center"
          >
            {collapsed ? (
              <PanelLeftOpen aria-hidden="true" className="size-5" />
            ) : (
              <>
                <PanelLeftClose aria-hidden="true" className="size-5" />
                <span>Collapse sidebar</span>
              </>
            )}
          </Button>
        </div>
      </aside>
      <div className="min-w-0">
        <header className="sticky top-0 z-20 border-b border-border bg-surface">
          <div className="flex min-h-16 items-center justify-between gap-2 px-4 sm:px-6">
            <div className="flex min-w-0 items-center gap-2">
              <MobileNavigation key={`${pathname}-${desktopNavigation}`} />
              <Button
                icon
                variant="ghost"
                aria-label="Toggle sidebar"
                onClick={toggle}
                className="hidden md:inline-flex"
              >
                <PanelLeftOpen aria-hidden="true" className="size-5" />
              </Button>
              <span className="text-sm font-medium md:hidden">SIAMIS</span>
              <span className="hidden text-sm text-muted-foreground lg:block">
                School Management System
              </span>
            </div>
            <div className="flex min-w-0 items-center gap-2">
              <div
                title="Global search is not available yet"
                className="hidden items-center gap-2 rounded-md border border-border px-3 py-2 text-xs text-muted-foreground xl:flex"
              >
                <Search aria-hidden="true" className="size-4" />
                Search unavailable
              </div>
              <div
                aria-label="Academic session not connected"
                className="hidden items-center gap-2 border-l border-border px-3 text-xs sm:flex"
              >
                <CalendarDays aria-hidden="true" className="size-4 text-muted-foreground" />
                <span>
                  Academic session<span className="block text-muted-foreground">Not connected</span>
                </span>
              </div>
              {import.meta.env.DEV && session.mode === 'development' && <Badge>Preview</Badge>}
              <AccountMenu />
            </div>
          </div>
          <p className="border-t border-border px-4 py-2 text-xs text-muted-foreground sm:hidden">
            Academic session · not connected
          </p>
          {import.meta.env.DEV && session.mode === 'development' && (
            <div className="flex flex-wrap items-center gap-2 border-t border-border px-4 py-2 sm:px-6">
              <p className="w-full flex-none text-xs text-muted-foreground sm:w-auto sm:flex-1">
                Development preview · no authenticated session
              </p>
              <div className="w-40">
                <Select
                  label="Preview capabilities"
                  value={session.preview}
                  onValueChange={session.changePreview}
                  options={[
                    { value: 'full', label: 'All capabilities' },
                    { value: 'people', label: 'Employee read only' },
                    { value: 'none', label: 'No capabilities' },
                  ]}
                />
              </div>
              <Link
                className="inline-flex min-h-11 items-center px-2 text-xs text-primary underline"
                to={paths.designSystem}
              >
                Design system
              </Link>
            </div>
          )}
        </header>
        <PageContainer tabIndex={-1} className="max-w-[100rem] min-w-0 py-5 lg:px-6 lg:py-6">
          <nav aria-label="Breadcrumb" className="mb-2">
            <ol className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
              {trail.map((item, i) => (
                <li
                  key={item.label}
                  className={cn(
                    'min-w-0 items-center gap-2',
                    i < trail.length - 1 ? 'hidden sm:flex' : 'flex',
                  )}
                >
                  {i > 0 && (
                    <span aria-hidden="true" className="hidden sm:inline">
                      /
                    </span>
                  )}
                  {item.href ? (
                    <Link
                      to={item.href}
                      className="inline-flex min-h-11 items-center underline-offset-4 hover:underline"
                    >
                      {item.label}
                    </Link>
                  ) : (
                    <span
                      aria-current={i === trail.length - 1 ? 'page' : undefined}
                      className="break-words"
                    >
                      {item.label}
                    </span>
                  )}
                </li>
              ))}
            </ol>
          </nav>
          <PageHeader className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <div className="min-w-0">
              <PageTitle>{route?.label ?? 'Page not found'}</PageTitle>
              <PageDescription>
                {route?.description ?? 'Choose another workspace to continue.'}
              </PageDescription>
            </div>
            <PageActionGroup primary={actions} />
          </PageHeader>
          <ErrorBoundary key={pathname} fallback={<ShellError />}>
            <Suspense fallback={<ShellLoading />}>
              <Outlet />
            </Suspense>
          </ErrorBoundary>
        </PageContainer>
      </div>
    </div>
  );
}
