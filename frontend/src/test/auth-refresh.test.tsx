import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { useEffect, useState } from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../app/providers/auth-provider';
import { createQueryClient } from '../app/providers/query-client';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { useAuth } from '../lib/auth/auth-context';
import { api } from '../lib/api/client';
import {
  blockProtectedRequests,
  reportSessionLoss,
  sessionEpoch,
} from '../lib/auth/session-events';
import type { SessionUser } from '../lib/auth/contracts';
import { Dialog } from '../components/ui/overlays';

const original: SessionUser = {
  userId: 'user-a',
  userName: 'Synthetic A',
  employeeId: null,
  isActive: true,
  requiresPasswordChange: false,
  roles: ['HRAdmin'],
  capabilities: ['Employee.Read'],
};
let current: SessionUser;
let checks: (() => Promise<Response>)[];
let mounts: number;
let unmounts: number;
function response(body: unknown = current, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
function deferred() {
  let resolve!: (value: Response) => void;
  const promise = new Promise<Response>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
beforeEach(() => {
  current = { ...original, capabilities: [...original.capabilities] };
  checks = [];
  mounts = 0;
  unmounts = 0;
  blockProtectedRequests(false);
  HTMLDialogElement.prototype.showModal = function () {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function () {
    this.open = false;
  };
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string) => {
      if (input === '/api/auth/me') return checks.shift()?.() ?? response();
      if (input === '/api/auth/csrf') return response({ token: 'synthetic-csrf' });
      return response({});
    }),
  );
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  blockProtectedRequests(false);
});
function Workspace() {
  const { state, refresh, logout } = useAuth();
  const [draft, setDraft] = useState('');
  useEffect(() => {
    mounts++;
    return () => {
      unmounts++;
    };
  }, []);
  return (
    <>
      <h1>Protected editor</h1>
      <p>{state.user?.userName}</p>
      <label>
        Unsaved draft
        <input value={draft} onChange={(event) => setDraft(event.target.value)} />
      </label>
      <button onClick={() => void refresh()}>Check session</button>
      <button onClick={() => void logout()}>Logout</button>
      <Dialog
        trigger={<button>Open draft modal</button>}
        title="Draft modal"
        description="Synthetic draft"
      >
        <label>
          Modal draft
          <input value={draft} onChange={(event) => setDraft(event.target.value)} />
        </label>
      </Dialog>
    </>
  );
}
async function setup(path = '/') {
  const cache = createQueryClient();
  render(
    <QueryClientProvider client={cache}>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route element={<SessionBoundary />}>
              <Route path="/login" element={<h1>Sign in</h1>} />
              <Route element={<ProtectedRoutes />}>
                <Route path="/" element={<Workspace />} />
                <Route element={<CapabilityRoute />}>
                  <Route path="/hr/employees" element={<Workspace />} />
                </Route>
              </Route>
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
  await screen.findByRole('heading', { name: 'Protected editor' });
  cache.setQueryData(['private'], 'synthetic protected data');
  fireEvent.change(screen.getByLabelText('Unsaved draft'), { target: { value: 'retain me' } });
  return cache;
}
async function focus() {
  const before = vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/auth/me').length;
  fireEvent(window, new Event('focus'));
  await waitFor(() =>
    expect(
      vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/auth/me').length,
    ).toBeGreaterThan(before),
  );
  await act(async () => {});
}
describe('background session revalidation with actual provider and route boundaries', () => {
  it('keeps the mounted workspace, draft and cache while same-user focus refresh is pending and completed', async () => {
    const cache = await setup();
    const pending = deferred();
    checks.push(() => pending.promise);
    await focus();
    expect(screen.getByLabelText('Unsaved draft')).toHaveValue('retain me');
    expect(mounts).toBe(1);
    expect(unmounts).toBe(0);
    await act(async () => pending.resolve(response()));
    expect(mounts).toBe(1);
    expect(cache.getQueryData(['private'])).toBe('synthetic protected data');
  });
  it('treats role/capability ordering as the same security context', async () => {
    current.roles = ['HRAdmin', 'SystemAdmin'];
    current.capabilities = ['Employee.Read', 'Leave.Read'];
    const cache = await setup();
    current = {
      ...current,
      roles: [...current.roles].reverse(),
      capabilities: [...current.capabilities].reverse(),
    };
    await focus();
    expect(mounts).toBe(1);
    expect(cache.getQueryData(['private'])).toBeDefined();
  });
  it('preserves state through the five-minute check', async () => {
    // Install fake timers before mounting so the provider interval uses the controlled clock.
    vi.useFakeTimers();
    const cache = createQueryClient();
    render(
      <QueryClientProvider client={cache}>
        <AuthProvider>
          <MemoryRouter>
            <Routes>
              <Route element={<SessionBoundary />}>
                <Route element={<ProtectedRoutes />}>
                  <Route path="/" element={<Workspace />} />
                </Route>
              </Route>
            </Routes>
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    );
    await act(async () => {});
    fireEvent.change(screen.getByLabelText('Unsaved draft'), { target: { value: 'timer draft' } });
    await act(async () => vi.advanceTimersByTimeAsync(300000));
    expect(screen.getByLabelText('Unsaved draft')).toHaveValue('timer draft');
    expect(mounts).toBe(1);
    expect(vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/auth/me')).toHaveLength(2);
  });
  it('clears state and cache after real logout', async () => {
    const cache = await setup();
    await userEvent.click(screen.getByRole('button', { name: 'Logout' }));
    await screen.findByRole('heading', { name: 'Sign in' });
    expect(cache.getQueryData(['private'])).toBeUndefined();
    expect(unmounts).toBe(1);
  });
  it.each(['401', 'inactive', 'password-change'])('fails closed for %s', async (kind) => {
    const cache = await setup();
    if (kind === '401') checks.push(async () => response({}, 401));
    else
      current = {
        ...current,
        isActive: kind !== 'inactive',
        requiresPasswordChange: kind === 'password-change',
      };
    await focus();
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Protected editor' })).not.toBeInTheDocument(),
    );
    expect(cache.getQueryData(['private'])).toBeUndefined();
  });
  it('replaces the old workspace and cache on identity change', async () => {
    const cache = await setup();
    current = { ...current, userId: 'user-b', userName: 'Synthetic B' };
    await focus();
    await screen.findByText('Synthetic B');
    expect(screen.getByLabelText('Unsaved draft')).toHaveValue('');
    expect(mounts).toBe(2);
    expect(cache.getQueryData(['private'])).toBeUndefined();
  });
  it('revokes route access and clears sensitive cache immediately', async () => {
    const cache = await setup('/hr/employees');
    current = { ...current, capabilities: [] };
    await focus();
    await screen.findByText('Access denied');
    expect(screen.queryByLabelText('Unsaved draft')).not.toBeInTheDocument();
    expect(cache.getQueryData(['private'])).toBeUndefined();
  });
  it('replaces linked-employee security context even with the same user ID', async () => {
    const cache = await setup();
    current = { ...current, employeeId: 'employee-b' };
    await focus();
    expect(screen.getByLabelText('Unsaved draft')).toHaveValue('');
    expect(cache.getQueryData(['private'])).toBeUndefined();
  });
  it('preserves drafts but blocks protected HTTP operations during network failure and retry', async () => {
    await setup();
    const input = screen.getByLabelText('Unsaved draft');
    checks.push(async () => {
      throw new TypeError('offline');
    });
    await focus();
    await screen.findByRole('dialog', { name: 'Session connection unavailable' });
    expect(input).toHaveValue('retain me');
    expect(input).not.toBeVisible();
    const count = vi.mocked(fetch).mock.calls.length;
    await expect(api('/api/employees')).rejects.toMatchObject({ kind: 'network' });
    await expect(api('/api/employees', { method: 'POST', body: {} })).rejects.toMatchObject({
      kind: 'network',
    });
    expect(vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/employees')).toHaveLength(
      0,
    );
    expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThanOrEqual(count);
    const pending = deferred();
    checks.push(() => pending.promise);
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(input).not.toBeVisible();
    await act(async () => pending.resolve(response()));
    expect(screen.getByLabelText('Unsaved draft')).toBe(input);
    expect(input).toBeVisible();
    expect(mounts).toBe(1);
  });
  it('does not accept an older refresh after a newer identity is established', async () => {
    await setup();
    const old = deferred();
    const latest = deferred();
    checks.push(
      () => old.promise,
      () => latest.promise,
    );
    await focus();
    await focus();
    await act(async () =>
      latest.resolve(response({ ...current, userId: 'new', userName: 'New identity' })),
    );
    await act(async () => old.resolve(response(original)));
    expect(screen.getByText('New identity')).toBeVisible();
    expect(screen.queryByText('Synthetic A')).not.toBeInTheDocument();
  });
  it('does not restore an expired session from an obsolete success', async () => {
    await setup();
    const old = deferred();
    checks.push(() => old.promise);
    await focus();
    act(() => reportSessionLoss(sessionEpoch()));
    await screen.findByRole('heading', { name: 'Sign in' });
    await act(async () => old.resolve(response()));
    expect(screen.queryByRole('heading', { name: 'Protected editor' })).not.toBeInTheDocument();
  });
  it('does not let a pending refresh undo logout', async () => {
    await setup();
    const old = deferred();
    checks.push(() => old.promise);
    await focus();
    await userEvent.click(screen.getByRole('button', { name: 'Logout' }));
    await screen.findByRole('heading', { name: 'Sign in' });
    await act(async () => old.resolve(response()));
    expect(screen.queryByRole('heading', { name: 'Protected editor' })).not.toBeInTheDocument();
  });
  it('rejects obsolete protected read responses after an identity change', async () => {
    await setup();
    const pending = deferred();
    const normal = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input) === '/api/employees' ? pending.promise : normal(input, init),
    );
    const read = api('/api/employees');
    const rejection = expect(read).rejects.toMatchObject({ kind: 'request' });
    current = { ...current, userId: 'new-user' };
    await focus();
    pending.resolve(response({ private: 'old-user-data' }));
    await rejection;
  });
  it('does not send a protected command across an identity change while waiting for CSRF', async () => {
    await setup();
    const pending = deferred();
    const normal = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((input, init) =>
      String(input) === '/api/auth/csrf' ? pending.promise : normal(input, init),
    );
    const command = api('/api/employees', { method: 'POST', body: {} });
    const rejection = expect(command).rejects.toMatchObject({ kind: 'request' });
    current = { ...current, userId: 'new-user' };
    await focus();
    pending.resolve(response({ token: 'synthetic-token' }));
    await rejection;
    expect(vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/employees')).toHaveLength(
      0,
    );
  });
  it('blocks authenticated password changes while revalidation is unavailable', async () => {
    await setup();
    checks.push(async () => {
      throw new TypeError('offline');
    });
    await focus();
    await expect(
      api('/api/auth/change-password', { method: 'POST', body: {} }),
    ).rejects.toMatchObject({ kind: 'network' });
    expect(
      vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/auth/change-password'),
    ).toHaveLength(0);
  });
  it('keeps recovery accessible above an existing portalled editor and retains its draft', async () => {
    await setup();
    await userEvent.click(screen.getByRole('button', { name: 'Open draft modal' }));
    const draft = screen.getByLabelText('Modal draft');
    checks.push(async () => {
      throw new TypeError('offline');
    });
    await focus();
    const recovery = screen.getByRole('dialog', { name: 'Session connection unavailable' });
    expect(recovery.parentElement).toBe(document.body);
    expect(recovery).toHaveStyle({ pointerEvents: 'auto' });
    expect(draft).toHaveValue('retain me');
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'Session connection unavailable' }),
      ).not.toBeInTheDocument(),
    );
    expect(screen.getByLabelText('Modal draft')).toBe(draft);
    expect(draft).toHaveValue('retain me');
  });
});
