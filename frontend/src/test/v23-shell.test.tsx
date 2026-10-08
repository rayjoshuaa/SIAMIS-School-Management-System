import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Tooltip } from 'radix-ui';
import { AuthContext } from '../lib/auth/auth-context';
import { NavigationSessionProvider } from '../lib/auth/navigation-session';
import { ShellNavigation } from '../components/layout/shell-navigation';
import { MobileNavigation } from '../components/layout/mobile-navigation';
import { AccountMenu } from '../components/layout/account-menu';
import { AppShell } from '../components/layout/app-shell';
import { AuthLayout } from '../features/auth/auth-layout';
import AuthForm from '../features/auth/auth-form';
import { ProtectedRoutes, CapabilityRoute } from '../features/auth/auth-boundaries';

const capabilities = ['Reporting.Read', 'Employee.Read', 'Attendance.Read', 'Leave.Read'];
const logout = vi.fn().mockResolvedValue(undefined);
function setup(
  children: React.ReactNode,
  path = '/hr',
  permitted = capabilities,
  status: 'authenticated' | 'anonymous' | 'error' = 'authenticated',
) {
  render(
    <AuthContext.Provider
      value={{
        state: {
          status,
          user:
            status === 'authenticated'
              ? {
                  userId: 'synthetic',
                  userName: 'Synthetic',
                  roles: [],
                  capabilities: permitted,
                  employeeId: null,
                  isActive: true,
                  requiresPasswordChange: false,
                }
              : undefined,
        },
        login: vi.fn(),
        refresh: vi.fn(),
        logout,
      }}
    >
      <MemoryRouter initialEntries={[path]}>
        <NavigationSessionProvider
          value={{ userName: 'Synthetic', capabilities: permitted, mode: 'authenticated' }}
        >
          <Tooltip.Provider>{children}</Tooltip.Provider>
        </NavigationSessionProvider>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}
beforeEach(() =>
  vi.stubGlobal(
    'matchMedia',
    vi
      .fn()
      .mockReturnValue({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }),
  ),
);
afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});
describe('V2.3 workspace shell', () => {
  it('renders a capability-filtered current module with a semantic active destination', () => {
    setup(<ShellNavigation />);
    const nav = screen.getByRole('navigation', { name: 'Module navigation' });
    expect(within(nav).getByRole('link', { name: 'Overview' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(within(nav).queryByRole('link', { name: 'Payroll' })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Documents' })).not.toBeInTheDocument();
  });
  it('uses explicitly granted Payroll and HRDocuments capabilities independently', () => {
    setup(<ShellNavigation />, '/hr/payroll', ['Payroll.Read']);
    expect(screen.getByRole('link', { name: 'Payroll' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', { name: 'Documents' })).not.toBeInTheDocument();
  });
  it('places existing security route under Administration without exposing HR', () => {
    setup(<ShellNavigation />, '/hr/security', ['Security.Manage']);
    expect(
      screen.getByRole('button', { name: 'Switch module: Administration' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Accounts & Security' })).toHaveAttribute(
      'href',
      '/hr/security',
    );
    expect(screen.queryByRole('link', { name: 'Employees' })).not.toBeInTheDocument();
  });
  it('opens the module menu with the keyboard and returns focus on Escape', async () => {
    setup(<ShellNavigation />);
    const trigger = screen.getByRole('button', { name: 'Switch module: Human Resources' });
    await userEvent.click(trigger);
    expect(screen.getByRole('menu')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(trigger).toHaveFocus());
  });
  it('restores drawer trigger focus on Escape without navigating', async () => {
    setup(<MobileNavigation />);
    const trigger = screen.getByRole('button', { name: 'Open navigation' });
    await userEvent.click(trigger);
    expect(screen.getByRole('dialog', { name: 'SIAMIS navigation' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(trigger).toHaveFocus());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it('closes mobile navigation after module selection and updates context', async () => {
    setup(
      <>
        <MobileNavigation />
        <main id="main" tabIndex={-1} />
      </>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    await userEvent.click(screen.getByRole('button', { name: 'Switch module: Human Resources' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'School Management' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    expect(
      screen.getByRole('button', { name: 'Switch module: School Management' }),
    ).toBeInTheDocument();
  });
  it('keeps sign-out real, accessible and retryable on failure', async () => {
    logout.mockRejectedValueOnce(new Error('synthetic failure'));
    setup(<AccountMenu />);
    await userEvent.click(screen.getByRole('button', { name: 'Account menu: Synthetic' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to sign out.');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Sign out' }));
    expect(logout).toHaveBeenCalledTimes(2);
  });
  it('does not expose the Development showcase in module navigation', async () => {
    setup(<ShellNavigation />);
    await userEvent.click(screen.getByRole('button', { name: 'Switch module: Human Resources' }));
    expect(
      screen.queryByRole('menuitem', { name: /showcase|design system/i }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /showcase|design system/i })).not.toBeInTheDocument();
  });
  it.each(['anonymous', 'error'] as const)(
    'does not mount shell or protected page for %s sessions',
    async (status) => {
      setup(
        <Routes>
          <Route element={<ProtectedRoutes />}>
            <Route element={<AppShell />}>
              <Route path="/hr" element={<p>Protected fixture</p>} />
            </Route>
          </Route>
          <Route path="/login" element={<p>Public login</p>} />
        </Routes>,
        '/hr',
        capabilities,
        status,
      );
      expect(screen.queryByText('Protected fixture')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Workspace sidebar')).not.toBeInTheDocument();
    },
  );
  it('preserves capability denial when a destination is entered directly', () => {
    setup(
      <Routes>
        <Route element={<CapabilityRoute />}>
          <Route path="/hr/payroll" element={<p>Confidential fixture</p>} />
        </Route>
      </Routes>,
      '/hr/payroll',
      ['Employee.Read'],
    );
    expect(screen.queryByText('Confidential fixture')).not.toBeInTheDocument();
  });
  it.each(['/login', '/forgot-password', '/activate', '/reset-password'])(
    'keeps %s outside authenticated navigation',
    (path) => {
      const mode =
        path === '/login'
          ? 'login'
          : path === '/forgot-password'
            ? 'forgot'
            : path === '/activate'
              ? 'activate'
              : 'reset';
      setup(
        <Routes>
          <Route element={<AuthLayout />}>
            <Route path={path} element={<AuthForm mode={mode} />} />
          </Route>
        </Routes>,
        path,
        [],
        'anonymous',
      );
      expect(screen.queryByLabelText('Workspace sidebar')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Open navigation' })).not.toBeInTheDocument();
      expect(
        screen.getByRole('img', { name: 'Siam International School crest' }),
      ).toBeInTheDocument();
    },
  );
});
