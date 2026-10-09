import { Suspense, useEffect, type ReactNode } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { AccountMenu } from './account-menu';
import { Brand } from './brand';
import { MobileNavigation } from './mobile-navigation';
import { ShellNavigation } from './shell-navigation';
import { ShellLoading, ShellError } from './shell-states';
import { NavigationSessionProvider } from '../../lib/auth/navigation-session';
import { activeRoute } from '../../app/router/navigation';
import { ApplicationHeader } from './application-header';
import { ContentFrame, WorkspaceHeader } from '../shared/workspace';
import { ErrorBoundary } from '../shared/error-boundary';
import { useAuth } from '../../lib/auth/auth-context';
import './application-shell.css';
export function ShellSession({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  return (
    <NavigationSessionProvider
      value={{
        userName: state.user?.userName ?? '',
        capabilities: state.user?.capabilities ?? [],
        roles: state.user?.roles ?? [],
        mode: 'authenticated',
      }}
    >
      {children}
    </NavigationSessionProvider>
  );
}
export function AppShell({ actions }: { actions?: ReactNode }) {
  const { pathname } = useLocation();
  const route = activeRoute(pathname);
  // Width is presentation only; route permissions and feature state stay unchanged.
  const employeeForm =
    /^\/hr\/employees\/(new|[^/]+\/(edit|employment-change|rehire|end-employment))$/.test(pathname);
  const detail = pathname.startsWith('/hr/employees/') || pathname.startsWith('/hr/attendance/');
  useEffect(() => {
    document.title = `${activeRoute(pathname)?.title ?? activeRoute(pathname)?.label ?? 'Page not found'} · SIAMIS`;
  }, [pathname]);
  return (
    <div className="application-shell">
      <a href="#main" className="shell-skip-link">
        Skip to content
      </a>
      <aside aria-label="Workspace sidebar" className="shell-sidebar">
        <Brand />
        <ShellNavigation />
        <p className="shell-institution">School Management System</p>
      </aside>
      <div className="shell-workspace">
        <ApplicationHeader navigation={<MobileNavigation />} account={<AccountMenu />} />
        <main id="main" tabIndex={-1} className="shell-content">
          <ContentFrame width={employeeForm ? 'form' : detail ? 'reading' : 'workspace'}>
            <WorkspaceHeader
              title={route?.title ?? route?.label ?? 'Page not found'}
              description={route?.description ?? 'Choose another workspace to continue.'}
              actions={actions}
            />
            <ErrorBoundary key={pathname} fallback={<ShellError />}>
              <Suspense fallback={<ShellLoading />}>
                <Outlet />
              </Suspense>
            </ErrorBoundary>
          </ContentFrame>
        </main>
      </div>
    </div>
  );
}
