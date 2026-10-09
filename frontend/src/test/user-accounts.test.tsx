import { AuthProvider } from '../app/providers/auth-provider';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { AuthContext } from '../lib/auth/auth-context';
import {
  SessionBoundary,
  CapabilityRoute,
  ProtectedRoutes,
} from '../features/auth/auth-boundaries';
import { UserAccounts } from '../features/administration/user-accounts';
import { permanentRoles } from '../features/administration/account-contracts';
import { availableModules, moduleDestinations } from '../app/router/navigation';

const fixture = {
  userId: '00000000-0000-0000-0000-000000000001',
  userName: 'Synthetic account',
  email: 'synthetic@example.invalid',
  employeeId: null as string | null,
  isActive: true,
  requiresPasswordChange: false,
  credentialEstablished: false,
  emailConfirmed: false,
  isLockedOut: false,
  roles: ['Management'],
  version: 'fixture-version',
};
let calls: { path: string; method: string; body: Record<string, unknown> | undefined }[];
let createStatus: number;
let issueStatus: number;
let listTotal: number;
let detailLoading: boolean;
let createLoading: boolean;
beforeEach(() => {
  fixture.employeeId = null;
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
  calls = [];
  createStatus = 201;
  issueStatus = 200;
  listTotal = 1;
  detailLoading = false;
  createLoading = false;
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options: RequestInit) => {
      const path = new URL(url, 'http://localhost').pathname;
      const method = options?.method ?? 'GET';
      const body = options?.body ? JSON.parse(options.body as string) : undefined;
      calls.push({ path, method, body });
      if (
        (detailLoading && path === `/api/admin/users/${fixture.userId}`) ||
        (createLoading && method === 'POST' && path === '/api/admin/users')
      )
        return new Promise<Response>(() => {});
      const response =
        path === '/api/auth/csrf'
          ? { token: 'synthetic-csrf' }
          : path.endsWith('/issue-credentials')
            ? { purpose: 'Activation' }
            : path === '/api/admin/users' && method === 'GET'
              ? {
                  items: [fixture],
                  totalCount: listTotal,
                  page: Number(new URL(url, 'http://localhost').searchParams.get('page')),
                  pageSize: 20,
                }
              : fixture;
      return new Response(JSON.stringify(response), {
        status: path.endsWith('/issue-credentials')
          ? issueStatus
          : path === '/api/admin/users' && method === 'POST'
            ? createStatus
            : 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }),
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});
function setup(
  capabilities = ['Security.Manage'],
  status: 'authenticated' | 'anonymous' = 'authenticated',
) {
  const query = createQueryClient();
  query.setDefaultOptions({ queries: { retry: false } });
  render(
    <QueryClientProvider client={query}>
      <AuthContext.Provider
        value={{
          state: {
            status,
            user:
              status === 'authenticated'
                ? {
                    userId: 'actor',
                    userName: 'Synthetic',
                    roles: [],
                    capabilities,
                    employeeId: null,
                    isActive: true,
                    requiresPasswordChange: false,
                  }
                : undefined,
          },
          login: vi.fn(),
          logout: vi.fn(),
          refresh: vi.fn(),
        }}
      >
        <MemoryRouter initialEntries={['/hr/security']}>
          <Routes>
            <Route path="/login" element={<h1>Login</h1>} />
            <Route element={<ProtectedRoutes />}>
              <Route element={<CapabilityRoute />}>
                <Route path="/hr/security" element={<UserAccounts />} />
              </Route>
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}
async function openCreate() {
  await userEvent.click(await screen.findByRole('button', { name: 'Create User' }));
}
async function fill() {
  await userEvent.type(screen.getByRole('textbox', { name: 'Username' }), 'synthetic-user');
  await userEvent.type(
    screen.getByRole('textbox', { name: 'Delivery email' }),
    'synthetic-user@example.invalid',
  );
  await userEvent.click(screen.getByRole('checkbox', { name: 'Management' }));
}
describe('real D13 account administration contract', () => {
  it.each([true, false])('gates linked employee navigation on Employee.Read (%s)', async (read) => {
    fixture.employeeId = '11111111-1111-4111-8111-111111111111';
    setup(['Security.Manage', ...(read ? ['Employee.Read'] : [])]);
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await screen.findByRole('heading', { name: 'Synthetic account' });
    const link = screen.queryByRole('link', { name: 'View linked employee' });
    if (read) expect(link).toHaveAttribute('href', `/hr/employees/${fixture.employeeId}`);
    else expect(link).not.toBeInTheDocument();
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });
  it('uses approved overlay widths and protects unsaved provisioning input without issuing a request', async () => {
    setup();
    await openCreate();
    const dialog = await screen.findByRole('dialog', { name: 'Create User' });
    expect(dialog).toHaveAttribute('data-size', 'md');
    await userEvent.type(screen.getByRole('textbox', { name: 'Username' }), 'unsaved-fixture');
    await userEvent.keyboard('{Escape}');
    expect(screen.getByText('Discard unsaved changes?')).toBeVisible();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Keep editing' }));
    expect(screen.getByRole('textbox', { name: 'Username' })).toHaveValue('unsaved-fixture');
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
    await userEvent.click(screen.getByRole('button', { name: 'View Synthetic account' }));
    expect(await screen.findByRole('dialog', { name: 'Account details' })).toHaveAttribute(
      'data-size',
      'md',
    );
  });
  it('opens a named create dialog, focuses Username, traps Tab and restores the opener on Escape', async () => {
    setup();
    const opener = await screen.findByRole('button', { name: 'Create User' });
    await userEvent.click(opener);
    const dialog = await screen.findByRole('dialog', { name: 'Create User' });
    expect(within(dialog).getByRole('textbox', { name: 'Username' })).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(within(dialog).getByRole('button', { name: 'Close' })).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(within(dialog).getByRole('button', { name: 'Create User' })).toHaveFocus();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(opener).toHaveFocus();
  });
  it('shows account quick view in a drawer and returns focus without resetting pagination', async () => {
    listTotal = 40;
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'Next' }));
    await screen.findByText('Page 2 · 40 accounts');
    const opener = screen.getByRole('button', { name: 'View Synthetic account' });
    await userEvent.click(opener);
    await screen.findByRole('dialog', { name: 'Account details' });
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(opener).toHaveFocus();
    expect(screen.getByText('Page 2 · 40 accounts')).toBeInTheDocument();
  });
  it('dismisses a safe create dialog on backdrop interaction', async () => {
    setup();
    await openCreate();
    await screen.findByRole('dialog');
    await userEvent.click(document.querySelector('[data-overlay-backdrop]')!);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
  it('replaces the drawer with one confirmation and Cancel returns to inspection without an API action', async () => {
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Reissue activation' }));
    expect(
      await screen.findByRole('alertdialog', { name: 'Reissue activation?' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await userEvent.click(document.querySelector('[data-overlay-backdrop]')!);
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await screen.findByRole('dialog', { name: 'Account details' });
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(calls.some((c) => c.path.endsWith('/issue-credentials'))).toBe(false);
  });
  it('retains a failed credential action in the confirmation with a safe error', async () => {
    issueStatus = 503;
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Reissue activation' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Issue instructions' }));
    expect(await screen.findByText(/Credential delivery is unavailable/)).toBeInTheDocument();
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  });
  it('does not carry a failed issuance error into a different delivery confirmation', async () => {
    issueStatus = 503;
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Reissue activation' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Issue instructions' }));
    await screen.findByText(/Credential delivery is unavailable/);
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await userEvent.click(
      await screen.findByRole('button', { name: 'Collect Development delivery link' }),
    );
    expect(
      await screen.findByRole('alertdialog', { name: 'Collect Development delivery?' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Action not completed')).not.toBeInTheDocument();
  });
  it('keeps the quick-view drawer open while account data loads', async () => {
    detailLoading = true;
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    expect(await screen.findByRole('dialog', { name: 'Account details' })).toBeInTheDocument();
    expect(screen.getByText('Loading account details')).toBeInTheDocument();
  });
  it('prevents ambiguous dialog dismissal during account creation', async () => {
    createLoading = true;
    setup();
    await openCreate();
    await fill();
    await userEvent.click(screen.getByRole('button', { name: 'Create User' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled());
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog', { name: 'Create User' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
  });
  it('omits transient credential delivery controls in Production builds', async () => {
    vi.stubEnv('DEV', false);
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await screen.findByRole('button', { name: 'Reissue activation' });
    expect(
      screen.queryByRole('button', { name: 'Collect Development delivery link' }),
    ).not.toBeInTheDocument();
    expect(calls.some((call) => call.path.endsWith('/credential-delivery'))).toBe(false);
  });
  it('requires Security.Manage rather than a role name and does not fetch unauthorized records', async () => {
    setup(['Employee.Manage']);
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(calls).toHaveLength(0);
  });
  it('keeps anonymous users outside the account page', async () => {
    setup([], 'anonymous');
    expect(await screen.findByRole('heading', { name: 'Login' })).toBeInTheDocument();
    expect(calls).toHaveLength(0);
  });
  it('reads bounded actor-scoped account data and shows real roles and status', async () => {
    setup();
    expect(await screen.findByText('Synthetic account')).toBeInTheDocument();
    expect(screen.getByText('Activation pending')).toBeInTheDocument();
    expect(calls.filter((c) => c.method === 'GET' && c.path === '/api/admin/users')).toHaveLength(
      1,
    );
  });
  it('offers only the five existing roles with no password field', async () => {
    setup();
    await openCreate();
    expect(permanentRoles).toEqual([
      'SystemAdmin',
      'HRAdmin',
      'PayrollAdmin',
      'Management',
      'Employee',
    ]);
    for (const role of permanentRoles)
      expect(screen.getByRole('checkbox', { name: role })).toBeInTheDocument();
    expect(document.querySelector('input[type="password"]')).toBeNull();
  });
  it('validates required fields and Employee linkage before sending creation', async () => {
    setup();
    await openCreate();
    await userEvent.click(screen.getByRole('button', { name: 'Create User' }));
    expect(screen.getByText('Username is required.')).toBeInTheDocument();
    await fill();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Employee' }));
    await userEvent.click(screen.getByRole('button', { name: 'Create User' }));
    expect(
      screen.getByText('The Employee role requires an existing Employee link.'),
    ).toBeInTheDocument();
    expect(calls.some((c) => c.method === 'POST' && c.path === '/api/admin/users')).toBe(false);
  });
  it('creates a passwordless account using the existing API and CSRF client', async () => {
    setup();
    await openCreate();
    await fill();
    await userEvent.click(screen.getByRole('button', { name: 'Create User' }));
    await screen.findByText(
      'Account created. Initial activation issued through the configured delivery channel.',
    );
    expect(calls.find((c) => c.method === 'POST' && c.path === '/api/admin/users')?.body).toEqual({
      userName: 'synthetic-user',
      email: 'synthetic-user@example.invalid',
      roles: ['Management'],
      employeeId: null,
    });
    expect(calls.some((c) => c.path === '/api/auth/csrf')).toBe(true);
    expect(calls.some((c) => c.path === '/api/auth/activate')).toBe(false);
  });
  it('does not imply success when configured delivery is unavailable', async () => {
    createStatus = 503;
    setup();
    await openCreate();
    await fill();
    await userEvent.click(screen.getByRole('button', { name: 'Create User' }));
    expect(await screen.findByText(/Credential delivery is unavailable/)).toBeInTheDocument();
    expect(screen.queryByText(/Account created\./)).not.toBeInTheDocument();
  });
  it('reissues activation with the latest administration version and never selects a password', async () => {
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'View Synthetic account' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Reissue activation' }));
    expect(calls.some((c) => c.path.endsWith('/issue-credentials'))).toBe(false);
    await userEvent.click(await screen.findByRole('button', { name: 'Issue instructions' }));
    await waitFor(() =>
      expect(calls.find((c) => c.path.endsWith('/issue-credentials'))?.body).toEqual({
        version: 'fixture-version',
      }),
    );
    expect(
      calls.some((c) => c.path === '/api/auth/reset-password' || c.path === '/api/auth/activate'),
    ).toBe(false);
  });
  it('exposes Administration and User Accounts through existing capability metadata only', () => {
    expect(availableModules(['Employee.Manage']).some((m) => m.id === 'system')).toBe(false);
    const module = availableModules(['Security.Manage']).find((m) => m.id === 'system')!;
    expect(moduleDestinations(module, ['Security.Manage'])[0]).toMatchObject({
      path: '/hr/security',
      label: 'User Accounts',
      capability: 'Security.Manage',
    });
  });
});
describe('Stage 1 account form preservation using the real session lifecycle', () => {
  it('keeps the provisioning draft and role selection during same-user focus refresh', async () => {
    const normal = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      if (String(input) === '/api/auth/me')
        return new Response(
          JSON.stringify({
            userId: 'actor',
            userName: 'Synthetic',
            roles: ['SystemAdmin'],
            capabilities: ['Security.Manage'],
            employeeId: null,
            isActive: true,
            requiresPasswordChange: false,
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      return normal(input, init);
    });
    render(
      <QueryClientProvider client={createQueryClient()}>
        <AuthProvider>
          <MemoryRouter initialEntries={['/hr/security']}>
            <Routes>
              <Route element={<SessionBoundary />}>
                <Route element={<ProtectedRoutes />}>
                  <Route element={<CapabilityRoute />}>
                    <Route path="/hr/security" element={<UserAccounts />} />
                  </Route>
                </Route>
              </Route>
            </Routes>
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    );
    await openCreate();
    await fill();
    const username = screen.getByRole('textbox', { name: 'Username' });
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByRole('textbox', { name: 'Username' })).toBe(username);
    expect(username).toHaveValue('synthetic-user');
    expect(screen.getByRole('checkbox', { name: 'Management' })).toBeChecked();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
});
