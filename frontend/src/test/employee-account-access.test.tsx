import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { AuthContext } from '../lib/auth/auth-context';
import { AccountAccess } from '../features/employees/account-access';
import type { Employee } from '../features/employees/contracts';

const employeeId = '11111111-1111-4111-8111-111111111111';
const userId = '22222222-2222-4222-8222-222222222222';
const employee = { employeeId, isActive: false, currentEmployment: null } as Employee;
let lifecycle: Record<string, unknown>;
let account: Record<string, unknown>;
let failure: number;
let calls: { path: string; method: string; body: unknown }[];
beforeEach(() => {
  failure = 0;
  calls = [];
  lifecycle = {
    employeeId,
    accountLinked: true,
    linkedUserId: userId,
    accountStatus: 'Active',
    currentEmploymentStatus: 'Resigned',
    hasCurrentEmployment: false,
  };
  account = {
    userId,
    employeeId,
    userName: 'Synthetic account',
    isActive: true,
    roles: ['Employee'],
    version: 'fixture-version',
    credentialEstablished: true,
    requiresPasswordChange: false,
    emailConfirmed: true,
    isLockedOut: false,
  };
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      const method = options?.method ?? 'GET';
      const body = options?.body ? JSON.parse(String(options.body)) : null;
      calls.push({ path: String(url), method, body });
      const status = String(url).includes('/api/admin/users') && failure ? failure : 200;
      return new Response(
        JSON.stringify(
          String(url).endsWith('/csrf')
            ? { token: 'isolated-fixture-token' }
            : String(url).endsWith('/account-lifecycle')
              ? lifecycle
              : account,
        ),
        { status, headers: { 'Content-Type': 'application/json' } },
      );
    }),
  );
});
afterEach(() => vi.unstubAllGlobals());
function setup(security = true) {
  const cache = createQueryClient();
  cache.setDefaultOptions({ queries: { retry: false }, mutations: { retry: false } });
  render(
    <QueryClientProvider client={cache}>
      <AuthContext.Provider
        value={{
          state: {
            status: 'authenticated',
            user: {
              userId: 'actor',
              userName: 'fixture',
              isActive: true,
              employeeId: null,
              requiresPasswordChange: false,
              roles: [],
              capabilities: ['Employee.Read', ...(security ? ['Security.Manage'] : [])],
            },
          },
          login: vi.fn(),
          logout: vi.fn(),
          refresh: vi.fn(),
        }}
      >
        <MemoryRouter>
          <AccountAccess employee={employee} />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}
describe('F6 employee account access', () => {
  it('groups existing account identity, credential and administration facts without mutating access', async () => {
    setup();
    expect(await screen.findByRole('region', { name: 'Linked account identity' })).toBeVisible();
    expect(screen.getByRole('region', { name: 'Credentials and verification' })).toBeVisible();
    expect(screen.getByRole('region', { name: 'Account administration' })).toHaveTextContent(
      'revoke existing sessions',
    );
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });
  it('separates employee, employment, account and credential statuses', async () => {
    setup();
    expect(await screen.findByText('Synthetic account')).toBeVisible();
    expect(screen.getByText('Inactive')).toBeVisible();
    expect(screen.getByText('Resigned')).toBeVisible();
    expect(screen.getByText('Established')).toBeVisible();
    expect(screen.getByText(/no account action has been taken/)).toBeVisible();
  });
  it('does not request or expose privileged details to an employee reader', async () => {
    setup(false);
    await screen.findByText(/Assigned roles, credential details/);
    expect(calls.some((call) => call.path.startsWith('/api/admin'))).toBe(false);
    expect(screen.queryByText('Synthetic account')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Disable account' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Open User Accounts' })).not.toBeInTheDocument();
  });
  it('renders an unlinked state with contextual provisioning navigation', async () => {
    lifecycle = { ...lifecycle, accountLinked: false, linkedUserId: null, accountStatus: null };
    setup();
    const link = await screen.findByRole('link', { name: 'Provision through User Accounts' });
    expect(link).toHaveAttribute('href', '/hr/security');
    expect(screen.getByText(/For provisioning, use Employee ID/)).toHaveTextContent(employeeId);
    expect(calls.some((call) => call.path.startsWith('/api/admin'))).toBe(false);
  });
  it('does not offer provisioning to HR without Security.Manage', async () => {
    lifecycle = { ...lifecycle, accountLinked: false };
    setup(false);
    await screen.findByText(/No system account is linked/);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
  it('reports pending activation without issuing a credential link', async () => {
    account.credentialEstablished = false;
    setup();
    expect(await screen.findByText(/The user must establish their own password/)).toBeVisible();
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
    expect(screen.getByText(/Activation and password recovery are initiated/)).toBeVisible();
  });
  it('reports required password change and lockout without conflating account status', async () => {
    account.requiresPasswordChange = true;
    account.isLockedOut = true;
    setup();
    expect(await screen.findByText('Password change required')).toBeVisible();
    expect(screen.getByText('Locked out')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Disable account' })).toBeVisible();
  });
  it('confirms status changes and sends the current version with fresh CSRF', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Disable account' }));
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
    await user.click(screen.getByRole('button', { name: 'Confirm account change' }));
    await screen.findByText(/Account change saved/);
    expect(calls.find((call) => call.method === 'PATCH')).toEqual({
      path: `/api/admin/users/${userId}/status`,
      method: 'PATCH',
      body: { isActive: false, version: 'fixture-version' },
    });
    expect(calls.some((call) => call.path === '/api/auth/csrf')).toBe(true);
    expect(calls.filter((call) => call.method !== 'GET')).toHaveLength(1);
  });
  it('cancels a status confirmation without mutation and restores focus', async () => {
    setup();
    const user = userEvent.setup();
    const button = await screen.findByRole('button', { name: 'Disable account' });
    await user.click(button);
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(button).toHaveFocus());
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });
  it('uses complete replacement role semantics with explicit confirmation', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Replace assigned roles' }));
    expect(screen.getByText(/Unselected roles will be removed/)).toBeVisible();
    await user.click(screen.getByRole('checkbox', { name: 'HRAdmin' }));
    await user.click(screen.getByRole('button', { name: 'Confirm account change' }));
    await screen.findByText(/Account change saved/);
    expect(calls.find((call) => call.method === 'PUT')?.body).toEqual({
      roles: ['Employee', 'HRAdmin'],
      version: 'fixture-version',
    });
  });
  it('offers enable explicitly for disabled current employees', async () => {
    lifecycle = { ...lifecycle, accountStatus: 'Disabled', hasCurrentEmployment: true };
    account.isActive = false;
    setup();
    expect(await screen.findByRole('button', { name: 'Enable account' })).toBeVisible();
    expect(screen.getByText(/These facts do not imply a policy violation/)).toBeVisible();
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });
  it('fails safely if the account linkage no longer matches', async () => {
    account.employeeId = 'another-employee';
    setup();
    await screen.findByText(/Reload the employee before administering/);
    expect(screen.queryByText('Synthetic account')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Disable account' })).not.toBeInTheDocument();
  });
  it('does not discard unrecognized role assignments', async () => {
    account.roles = ['FutureRole'];
    setup();
    expect(await screen.findByRole('button', { name: 'Replace assigned roles' })).toBeDisabled();
  });
  it.each([400, 403, 409, 503])(
    'preserves failed command confirmation on HTTP %s',
    async (status) => {
      setup();
      const user = userEvent.setup();
      await user.click(await screen.findByRole('button', { name: 'Disable account' }));
      failure = status;
      await user.click(screen.getByRole('button', { name: 'Confirm account change' }));
      expect(await screen.findByRole('alert')).toHaveTextContent('No successful change is implied');
      expect(screen.getByRole('alertdialog')).toBeVisible();
      expect(screen.queryByText(/Account change saved/)).not.toBeInTheDocument();
    },
  );
});
