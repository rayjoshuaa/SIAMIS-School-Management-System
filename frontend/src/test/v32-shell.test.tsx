import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../lib/auth/auth-context';
import { NavigationSessionProvider } from '../lib/auth/navigation-session';
import { AppShell } from '../components/layout/app-shell';
import { ApplicationHeader } from '../components/layout/application-header';
import { MobileNavigation } from '../components/layout/mobile-navigation';
import { AccountMenu } from '../components/layout/account-menu';

const session = {
  userName: 'Shell fixture',
  capabilities: ['Employee.Read'],
  roles: ['Employee'],
  mode: 'authenticated' as const,
};
const logout = vi.fn().mockResolvedValue(undefined);
const auth = {
  state: {
    status: 'authenticated' as const,
    user: {
      userId: 'fixture',
      userName: session.userName,
      employeeId: null,
      isActive: true,
      requiresPasswordChange: false,
      roles: session.roles,
      capabilities: session.capabilities,
    },
  },
  refresh: async () => {},
  login: async () => {},
  logout,
};
function fixture(element: React.ReactNode, path = '/hr/employees') {
  return render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[path]}>
        <NavigationSessionProvider value={session}>{element}</NavigationSessionProvider>
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
afterEach(() => vi.unstubAllGlobals());

describe('V3.2 shell composition', () => {
  it.each([
    ['/hr/employees', 'workspace'],
    ['/hr/attendance', 'workspace'],
    ['/hr/employees/fixture', 'reading'],
    ['/hr/attendance/fixture/2026-10-08', 'reading'],
    ['/hr/employees/new', 'form'],
    ['/hr/employees/fixture/edit', 'form'],
    ['/hr/employees/fixture/end-employment', 'form'],
  ])('owns one content frame for %s', (path, width) => {
    fixture(
      <Routes>
        <Route element={<AppShell />}>
          <Route path={path} element={<p>Existing feature content</p>} />
        </Route>
      </Routes>,
      path,
    );
    const main = screen.getByRole('main');
    expect(main.querySelectorAll('.ui-content-frame')).toHaveLength(1);
    const frame = main.querySelector('.ui-content-frame');
    expect(frame).toHaveAttribute('data-width', width);
    expect(frame?.firstElementChild).toHaveClass('ui-workspace-header');
    expect(frame).toContainElement(screen.getByText('Existing feature content'));
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(main).toHaveAttribute('id', 'main');
    expect(main).toHaveAttribute('tabindex', '-1');
  });
  it('keeps module context in chrome and destination title in the page', () => {
    fixture(
      <ApplicationHeader
        navigation={<button>Navigation fixture</button>}
        account={<button>Account fixture</button>}
      />,
    );
    expect(screen.getByRole('banner')).toHaveTextContent('Human Resources');
    expect(screen.getByRole('banner')).not.toHaveTextContent('Employees');
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
  });
  it('retains Escape dismissal and focus return for mobile navigation', async () => {
    fixture(<MobileNavigation />);
    const trigger = screen.getByRole('button', { name: 'Open navigation' });
    await userEvent.click(trigger);
    expect(screen.getByRole('dialog', { name: 'SIAMIS navigation' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Payroll' })).not.toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });
  it('preserves real account actions and keyboard focus restoration', async () => {
    fixture(<AccountMenu />);
    const trigger = screen.getByRole('button', { name: `Account menu: ${session.userName}` });
    await userEvent.click(trigger);
    expect(screen.getByRole('menu')).toHaveTextContent(session.userName);
    expect(screen.getByRole('menuitem', { name: 'Sign out' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(trigger).toHaveFocus();
    expect(logout).not.toHaveBeenCalled();
  });
});
