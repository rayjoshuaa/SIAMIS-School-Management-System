import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../app/providers/auth-provider';
import { createQueryClient } from '../app/providers/query-client';
import { useAuth } from '../lib/auth/auth-context';
import AuthForm from '../features/auth/auth-form';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { api } from '../lib/api/client';
import { safeReturnUrl } from '../lib/auth/return-url';
import { captureCredentialLink, clearCredentialLink } from '../lib/auth/credential-link';
import { readSession } from '../lib/auth/contracts';
import { advanceSession, reportSessionLoss, sessionEpoch } from '../lib/auth/session-events';
const person = {
  userId: 'ad00ce15-46a4-4c9c-9cbd-a12895227301',
  userName: 'Fixture user',
  employeeId: null,
  isActive: true,
  requiresPasswordChange: false,
  roles: ['HRAdmin'],
  capabilities: ['Employee.Read'],
};
let current: typeof person | null;
let rejection = 401;
let unavailable = false;
let credentialFailure = false;
const calls: { path: string; body: unknown; csrf: string | null }[] = [];
function response(status: number, body?: unknown) {
  return new Response(status === 204 ? null : JSON.stringify(body ?? {}), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
beforeEach(() => {
  current = null;
  rejection = 401;
  unavailable = false;
  credentialFailure = false;
  calls.length = 0;
  clearCredentialLink();
  window.history.replaceState(null, '', '/');
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options: RequestInit) => {
      if (unavailable) throw new Error('private network detail');
      const path = new URL(url, 'http://localhost').pathname;
      const body = options.body ? JSON.parse(String(options.body)) : null;
      calls.push({ path, body, csrf: new Headers(options.headers).get('X-CSRF-TOKEN') });
      if (path === '/api/auth/csrf') return response(200, { token: 'synthetic csrf' });
      if (path === '/api/auth/me') return current ? response(200, current) : response(401);
      if (path === '/api/auth/login') {
        if (body.userName === 'good') {
          current = { ...person };
          return response(204);
        }
        return response(401);
      }
      if (path === '/api/auth/change-password') {
        current = { ...person };
        return response(204);
      }
      if (path === '/api/auth/logout') {
        current = null;
        return response(204);
      }
      if (path === '/api/employees') return response(rejection);
      if (path === '/api/auth/activate' || path === '/api/auth/reset-password')
        return response(credentialFailure ? 400 : 204, { detail: 'private token details' });
      return response(200, { message: 'neutral backend response' });
    }),
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  clearCredentialLink();
});
function Probe() {
  const { state, logout, refresh } = useAuth();
  const [error, setError] = useState('');
  return (
    <>
      <h1>Protected workspace</h1>
      <p>{state.user?.userName}</p>
      <p>{state.user?.capabilities.join(',')}</p>
      <button onClick={() => void logout()}>Logout</button>
      <button onClick={() => void refresh()}>Refresh session</button>
      <button onClick={() => void api('/api/employees').catch((e) => setError(String(e.status)))}>
        Protected request
      </button>
      <p>{error}</p>
    </>
  );
}
function setup(path = '/', cache = createQueryClient()) {
  render(
    <QueryClientProvider client={cache}>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route element={<SessionBoundary />}>
              <Route path="/login" element={<AuthForm key="login" mode="login" />} />
              <Route path="/change-password" element={<AuthForm key="change" mode="change" />} />
              <Route path="/forgot-password" element={<AuthForm key="forgot" mode="forgot" />} />
              <Route path="/activate" element={<AuthForm key="activate" mode="activate" />} />
              <Route path="/reset-password" element={<AuthForm key="reset" mode="reset" />} />
              <Route element={<ProtectedRoutes />}>
                <Route path="/" element={<Probe />} />
                <Route element={<CapabilityRoute />}>
                  <Route path="/hr/payroll" element={<Probe />} />
                  <Route path="/hr/employees" element={<Probe />} />
                </Route>
              </Route>
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
  return cache;
}
async function signIn(name = 'good') {
  await screen.findByLabelText(/^Username/);
  await userEvent.type(screen.getByLabelText(/^Username/), name);
  await userEvent.type(screen.getByLabelText(/^Password/), 'synthetic password');
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}
async function credential(mode: 'activate' | 'reset-password') {
  window.history.replaceState(null, '', `/${mode}?userId=${person.userId}&token=synthetic%2Btoken`);
  captureCredentialLink();
  setup('/' + mode);
  await screen.findByLabelText(/^New password/);
  await userEvent.type(screen.getByLabelText(/^New password/), 'synthetic password');
  await userEvent.type(screen.getByLabelText(/^Confirm password/), 'synthetic password');
  await userEvent.click(
    screen.getByRole('button', {
      name: mode === 'activate' ? 'Activate account' : 'Reset password',
    }),
  );
}
describe('F3 authoritative session and account access', () => {
  it('redirects an already authenticated login visit safely', async () => {
    current = { ...person };
    setup('/login?returnTo=https://evil.invalid');
    expect(await screen.findByRole('heading', { name: 'Protected workspace' })).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
  });
  it('requires the existing password-change contract before normal shell access', async () => {
    current = { ...person, requiresPasswordChange: true };
    setup('/');
    await screen.findByLabelText(/^Current password/);
    await userEvent.type(screen.getByLabelText(/^Current password/), 'old synthetic password');
    await userEvent.type(screen.getByLabelText(/^New password/), 'new synthetic password');
    await userEvent.type(screen.getByLabelText(/^Confirm password/), 'new synthetic password');
    await userEvent.click(screen.getByRole('button', { name: 'Change password' }));
    expect(await screen.findByRole('heading', { name: 'Protected workspace' })).toBeInTheDocument();
    expect(calls.find((x) => x.path === '/api/auth/change-password')?.body).toEqual({
      currentPassword: 'old synthetic password',
      newPassword: 'new synthetic password',
    });
  });
  it('keeps reset failures generic and retryable', async () => {
    credentialFailure = true;
    await credential('reset-password');
    expect(
      await screen.findByText(
        'Unable to complete this password operation. Check your password or request a new link.',
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reset password' })).toBeEnabled();
  });
  it('shows bootstrap before anonymous login without a login flash', async () => {
    setup();
    expect(screen.getByRole('status', { name: 'Preparing your workspace' })).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
    expect(await screen.findByLabelText(/^Username/)).toBeInTheDocument();
  });
  it('restores authenticated identity and effective capabilities', async () => {
    current = { ...person };
    setup();
    expect(await screen.findByText(person.userName)).toBeInTheDocument();
    expect(screen.getByText('Employee.Read')).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
  });
  it('keeps bootstrap network failure distinct from anonymous', async () => {
    unavailable = true;
    setup();
    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
    unavailable = false;
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByLabelText(/^Username/)).toBeInTheDocument();
  });
  it('signs in through CSRF-protected username contract and restores safe destination', async () => {
    setup('/login?returnTo=/hr/employees');
    await signIn();
    expect(await screen.findByRole('heading', { name: 'Protected workspace' })).toBeInTheDocument();
    const call = calls.find((x) => x.path === '/api/auth/login')!;
    expect(call.body).toEqual({ userName: 'good', password: 'synthetic password' });
    expect(call.csrf).toBe('synthetic csrf');
  });
  it('does not treat invalid or disabled login as an authenticated session', async () => {
    setup('/login');
    await signIn('disabled');
    expect(
      await screen.findByText('Unable to sign in with the provided credentials.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Protected workspace' })).not.toBeInTheDocument();
  });
  it('supports accessible password visibility without persisting it', async () => {
    setup('/login');
    await screen.findByLabelText(/^Password/);
    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText(/^Password/)).toHaveAttribute('type', 'text');
    await userEvent.click(screen.getByRole('button', { name: 'Hide password' }));
    expect(screen.getByLabelText(/^Password/)).toHaveAttribute('type', 'password');
    expect(localStorage.length).toBe(0);
  });
  it('clears cached user data only after server logout', async () => {
    current = { ...person };
    const cache = setup();
    await screen.findByText(person.userName);
    cache.setQueryData(['confidential'], { employee: 'User A' });
    await userEvent.click(screen.getByRole('button', { name: 'Logout' }));
    await screen.findByLabelText(/^Username/);
    expect(screen.getByLabelText(/^Username/)).toHaveFocus();
    expect(screen.getByRole('heading', { name: 'Sign in to SIAMIS' })).not.toHaveFocus();
    expect(cache.getQueryData(['confidential'])).toBeUndefined();
    expect(calls.find((x) => x.path === '/api/auth/logout')?.csrf).toBe('synthetic csrf');
  });
  it('clears cache and explains authoritative 401 session loss', async () => {
    current = { ...person };
    const cache = setup();
    await screen.findByText(person.userName);
    cache.setQueryData(['private'], 'secret sample');
    await userEvent.click(screen.getByRole('button', { name: 'Protected request' }));
    expect(
      await screen.findByText('Your session has ended. Please sign in again.'),
    ).toBeInTheDocument();
    expect(cache.getQueryData(['private'])).toBeUndefined();
  });
  it('preserves session and cache on 403 instead of redirecting to login', async () => {
    current = { ...person };
    rejection = 403;
    const cache = setup();
    await screen.findByText(person.userName);
    cache.setQueryData(['private'], 'sample');
    await userEvent.click(screen.getByRole('button', { name: 'Protected request' }));
    await screen.findByText('403');
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
    expect(cache.getQueryData(['private'])).toBe('sample');
  });
  it('denies direct capability routes without making role assumptions', async () => {
    current = { ...person, roles: ['SystemAdmin'], capabilities: ['Employee.Read'] };
    setup('/hr/payroll');
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Protected workspace' })).not.toBeInTheDocument();
  });
  it('allows a directly requested route when the effective capability exists', async () => {
    current = { ...person, roles: [], capabilities: ['Payroll.Read'] };
    setup('/hr/payroll');
    expect(await screen.findByRole('heading', { name: 'Protected workspace' })).toBeInTheDocument();
  });
  it('rejects external, protocol-relative, encoded, auth and unknown return destinations', () => {
    for (const v of [
      'https://evil.invalid',
      '//evil.invalid',
      '/%2f%2fevil.invalid',
      '/login',
      '/activate?token=x',
      '/unknown',
      '/hr/employees?returnTo=https://evil.invalid',
      '\\evil.invalid',
    ])
      expect(safeReturnUrl(v)).toBe('/');
    expect(safeReturnUrl('/hr/employees')).toBe('/hr/employees');
  });
  it('does not let stale request failures invalidate a newer session epoch', () => {
    const old = sessionEpoch();
    advanceSession();
    reportSessionLoss(old);
    expect(sessionEpoch()).toBeGreaterThan(old);
  });
  it('rejects malformed bootstrap data and suppresses capabilities during mandatory password change', () => {
    expect(() => readSession({ roles: ['SystemAdmin'] })).toThrow();
    expect(readSession({ ...person, requiresPasswordChange: true }).capabilities).toEqual([]);
    expect(readSession({ ...person, isActive: false }).capabilities).toEqual([]);
  });
  it('activates using the exact contract and removes sensitive URL data before requests', async () => {
    await credential('activate');
    expect(
      await screen.findByText('Your account has been activated. You can now sign in.'),
    ).toBeInTheDocument();
    expect(window.location.search).toBe('');
    expect(calls.find((x) => x.path === '/api/auth/activate')?.body).toEqual({
      userId: person.userId,
      token: 'synthetic+token',
      newPassword: 'synthetic password',
    });
    expect(screen.queryByText('synthetic+token')).not.toBeInTheDocument();
  });
  it('returns safe activation failure without raw token/server details', async () => {
    credentialFailure = true;
    await credential('activate');
    expect(
      await screen.findByText(
        'Unable to complete this password operation. Check your password or request a new link.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText('private token details')).not.toBeInTheDocument();
  });
  it('validates password confirmation and required length without making a command', async () => {
    window.history.replaceState(null, '', `/activate?userId=${person.userId}&token=synthetic`);
    captureCredentialLink();
    setup('/activate');
    await screen.findByLabelText(/^New password/);
    await userEvent.type(screen.getByLabelText(/^New password/), 'short');
    await userEvent.type(screen.getByLabelText(/^Confirm password/), 'different');
    await userEvent.click(screen.getByRole('button', { name: 'Activate account' }));
    expect(await screen.findByText('Passwords must match.')).toBeInTheDocument();
    expect(screen.getByText('Use at least 12 characters.')).toBeInTheDocument();
    expect(calls.some((x) => x.path === '/api/auth/activate')).toBe(false);
  });
  it('shows missing credential link recovery without exposing token input', async () => {
    setup('/reset-password');
    expect(await screen.findByText('This link is unavailable')).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Token/)).not.toBeInTheDocument();
  });
  it('uses neutral verified-email recovery behavior', async () => {
    setup('/forgot-password');
    await screen.findByLabelText(/^Email/);
    await userEvent.type(screen.getByLabelText(/^Email/), 'unknown@example.invalid');
    await userEvent.click(screen.getByRole('button', { name: 'Request password reset' }));
    expect(await screen.findByText(/If an eligible account matches/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Back to sign in' }));
    expect(await screen.findByLabelText(/^Username/)).toBeInTheDocument();
    expect(calls.find((x) => x.path === '/api/auth/forgot-password')?.body).toEqual({
      email: 'unknown@example.invalid',
    });
  });
  it('resets the password without sending confirmation or provenance fields', async () => {
    await credential('reset-password');
    expect(
      await screen.findByText('Your password has been reset. Sign in with your new password.'),
    ).toBeInTheDocument();
    expect(
      Object.keys(calls.find((x) => x.path === '/api/auth/reset-password')!.body as object),
    ).toEqual(['userId', 'token', 'newPassword']);
  });
});
