import { describe, it, expect, vi } from 'vitest';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { render as renderUi, screen } from '@testing-library/react';
import { AuthContext } from '../lib/auth/auth-context';
import userEvent from '@testing-library/user-event';
import { Tooltip } from 'radix-ui';
import {
  routes,
  visibleRoutes,
  hrIsActive,
  breadcrumbs,
  modules,
  schoolNavigation,
} from '../app/router/navigation';
import { NavigationSessionProvider } from '../lib/auth/navigation-session';
import { ShellNavigation } from '../components/layout/shell-navigation';
import { AppShell } from '../components/layout/app-shell';
import { ShellLoading, ShellNotFound } from '../components/layout/shell-states';
import { AccountMenu } from '../components/layout/account-menu';
import { MobileNavigation } from '../components/layout/mobile-navigation';
import { PageActionGroup } from '../components/layout/page-action-group';
import { SchoolDashboard } from '../features/shell/dashboard';
const logout = vi.fn().mockResolvedValue(undefined);
const auth = {
  state: {
    status: 'authenticated' as const,
    user: {
      userId: 'fixture',
      userName: 'Test',
      employeeId: null,
      isActive: true,
      requiresPasswordChange: false,
      roles: [],
      capabilities: [],
    },
  },
  refresh: async () => {},
  login: async () => {},
  logout,
};
function render(ui: React.ReactNode) {
  return renderUi(<AuthContext.Provider value={auth}>{ui}</AuthContext.Provider>);
}
const identity = {
  userName: 'Preview',
  mode: 'authenticated' as const,
  capabilities: ['Employee.Read', 'Reporting.Read'],
};
describe('navigation contract', () => {
  it('keeps the primary action and provides secondary overflow actions', async () => {
    const action = vi.fn();
    render(
      <PageActionGroup
        primary={<button>Primary action</button>}
        secondary={[{ label: 'Secondary action', onSelect: action }]}
      />,
    );
    expect(screen.getByRole('button', { name: 'Primary action' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'More page actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Secondary action' }));
    expect(action).toHaveBeenCalledOnce();
  });
  it('contains rendering errors without exposing their details and recovers on navigation', async () => {
    const log = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockReturnValue({
        matches: true,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }),
    );
    function BrokenPage(): never {
      throw new Error('Sensitive internal detail');
    }
    try {
      render(
        <MemoryRouter initialEntries={['/hr/employees']}>
          <NavigationSessionProvider value={identity}>
            <Tooltip.Provider>
              <Routes>
                <Route element={<AppShell />}>
                  <Route path="/hr/employees" element={<BrokenPage />} />
                  <Route path="/" element={<p>Recovered workspace</p>} />
                </Route>
              </Routes>
            </Tooltip.Provider>
          </NavigationSessionProvider>
        </MemoryRouter>,
      );
      expect(screen.queryByText('Sensitive internal detail')).not.toBeInTheDocument();
      expect(screen.getByLabelText('Workspace sidebar')).toBeInTheDocument();
      await userEvent.click(screen.getByRole('link', { name: 'Return to Dashboard' }));
      expect(screen.getByText('Recovered workspace')).toBeInTheDocument();
    } finally {
      log.mockRestore();
      vi.unstubAllGlobals();
    }
  });
  it('keeps long Thai account names available in full', async () => {
    const name = 'คุณครู ทดสอบชื่อที่มีความยาวสำหรับโรงเรียนนานาชาติ';
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={{ ...identity, userName: name }}>
          <AccountMenu />
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: `Account menu: ${name}` }));
    expect(screen.getByRole('menu')).toHaveTextContent(name);
    expect(screen.getByRole('menuitem', { name: 'Profile' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out' }));
    expect(logout).toHaveBeenCalled();
  });
  it('closes mobile navigation on a destination selection', async () => {
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <MobileNavigation />
            <main id="main" tabIndex={-1} />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Employees' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it('keeps routes unique and uses actual Security.Manage', () => {
    expect(new Set(routes.map((r) => r.path)).size).toBe(routes.length);
    expect(routes.find((r) => r.path === '/hr/security')?.capability).toBe('Security.Manage');
  });
  it('filters by capabilities rather than roles', () => {
    expect(visibleRoutes(['Employee.Read']).map((r) => r.path)).toEqual([
      '/',
      '/hr/employees',
      '/school-management',
    ]);
    expect(visibleRoutes([]).some((r) => r.path === '/hr/payroll')).toBe(false);
  });
  it('resolves parent state only for HR route segments', () => {
    expect(hrIsActive('/hr/leave')).toBe(true);
    expect(hrIsActive('/hrocket')).toBe(false);
  });
  it('derives breadcrumb labels and does not link an unavailable parent', () => {
    expect(breadcrumbs('/hr/employees', [])).toEqual([
      { label: 'HR', href: undefined },
      { label: 'Employees' },
    ]);
    expect(breadcrumbs('/hr/employees', ['Reporting.Read'])[0].href).toBe('/hr');
  });
  it('marks the active child and parent and hides payroll', () => {
    render(
      <MemoryRouter initialEntries={['/hr/employees']}>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <ShellNavigation />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Employees' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'Human Resources, current module' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
    expect(screen.queryByRole('link', { name: 'Payroll' })).not.toBeInTheDocument();
  });
  it('collapses groups and calls the mobile navigation callback', async () => {
    const navigate = vi.fn();
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <ShellNavigation onNavigate={navigate} />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Human Resources' }));
    expect(screen.queryByRole('link', { name: 'Employees' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Human Resources' }));
    await userEvent.click(screen.getByRole('link', { name: 'Employees' }));
    expect(navigate).toHaveBeenCalledOnce();
  });
  it('keeps collapsed module icons accessible and expands HR without exposing nested icons', async () => {
    const expand = vi.fn();
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <ShellNavigation collapsed onExpand={expand} />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    expect(screen.queryByRole('link', { name: 'Employees' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Human Resources' })).toHaveClass(
      'inline-flex',
      'min-h-11',
      'justify-center',
    );
    expect(screen.getByRole('link', { name: 'Dashboard' })).toHaveClass('bg-sidebar-active');
    await userEvent.click(screen.getByRole('button', { name: 'Human Resources' }));
    expect(expand).toHaveBeenCalledOnce();
  });
  it('models the whole school platform without inventing business routes', () => {
    expect(modules.map((m) => m.label)).toEqual([
      'School Management',
      'Admissions & CRM',
      'Human Resources',
      'Accounting & Finance',
      'Teacher Learning',
      'Projects & Tasks',
      'Reports',
      'System Administration',
    ]);
    expect(schoolNavigation).toHaveLength(16);
    expect(routes).toHaveLength(9);
    expect(routes.filter((r) => r.status === 'planned')).toHaveLength(1);
  });
  it('expands planned school destinations without making them business links', async () => {
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <ShellNavigation />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'School Management' }));
    expect(screen.getByRole('button', { name: 'Results & Report Cards' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Students' })).toBeDisabled();
    expect(screen.getByRole('link', { name: 'School Management overview' })).toHaveAttribute(
      'href',
      '/school-management',
    );
    expect(screen.getByRole('button', { name: 'Accounting & Finance' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(screen.getByRole('button', { name: 'School Management' }));
    expect(screen.queryByRole('button', { name: 'Students' })).not.toBeInTheDocument();
  });
  it('supports long planned school navigation inside the mobile drawer', async () => {
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <MobileNavigation />
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    await userEvent.click(screen.getByRole('button', { name: 'School Management' }));
    expect(screen.getByRole('button', { name: 'Student Documents' })).toBeDisabled();
    await userEvent.click(screen.getByRole('link', { name: 'School Management overview' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it('shows school dashboard structure with honest empty metrics and no fake actions', () => {
    render(<SchoolDashboard />);
    for (const name of [
      'Students',
      'Teachers',
      'Classes',
      'Admissions',
      'Enrollment overview',
      'Student population',
      'Recent activities',
      'Upcoming events',
      'Quick actions',
    ]) {
      expect(screen.getByRole('heading', { name })).toBeInTheDocument();
    }
    expect(screen.getAllByText('No data available')).toHaveLength(4);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByText('No actions available')).toBeInTheDocument();
  });
  it('persists only a UI collapse preference', async () => {
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockReturnValue({
        matches: true,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }),
    );
    render(
      <MemoryRouter>
        <NavigationSessionProvider value={identity}>
          <Tooltip.Provider>
            <Routes>
              <Route element={<AppShell />}>
                <Route path="/" element={<p>Workspace</p>} />
              </Route>
            </Routes>
          </Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    expect(screen.getByLabelText('Academic session not connected')).toHaveTextContent(
      'Not connected',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Collapse sidebar' }));
    expect(localStorage.getItem('siamis.ui.sidebar.v1')).toBe('collapsed');
    expect(screen.getByRole('button', { name: 'Expand sidebar' })).toBeInTheDocument();
    localStorage.removeItem('siamis.ui.sidebar.v1');
    vi.unstubAllGlobals();
  });
  it('provides safe loading and not-found recovery', () => {
    render(
      <MemoryRouter>
        <ShellLoading />
        <ShellNotFound />
      </MemoryRouter>,
    );
    expect(screen.getByRole('status')).toHaveAccessibleName('Preparing your workspace');
    expect(screen.getByRole('link', { name: 'Return to Dashboard' })).toHaveAttribute('href', '/');
  });
});
