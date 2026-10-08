import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../lib/auth/auth-context';
import { ApiError } from '../lib/api/errors';
import { captureCredentialLink, clearCredentialLink } from '../lib/auth/credential-link';
import { AuthLayout } from '../features/auth/auth-layout';
import AuthForm from '../features/auth/auth-form';

type Mode = 'login' | 'activate' | 'forgot' | 'reset';
const paths: Record<Mode, string> = {
  login: '/login',
  activate: '/activate',
  forgot: '/forgot-password',
  reset: '/reset-password',
};
function setup(mode: Mode, login = vi.fn().mockResolvedValue(undefined), withLink = false) {
  if (withLink) {
    window.history.replaceState(
      null,
      '',
      `${paths[mode]}?userId=11111111-1111-1111-1111-111111111111&token=synthetic-v22-fixture`,
    );
    captureCredentialLink();
  }
  render(
    <AuthContext.Provider
      value={{ state: { status: 'anonymous' }, login, refresh: vi.fn(), logout: vi.fn() }}
    >
      <MemoryRouter initialEntries={[paths[mode]]}>
        <Routes>
          <Route element={<AuthLayout />}>
            <Route path={paths[mode]} element={<AuthForm mode={mode} />} />
            {mode !== 'login' && <Route path="/login" element={<AuthForm mode="login" />} />}
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}
afterEach(() => {
  clearCredentialLink();
  window.history.replaceState(null, '', '/');
  vi.unstubAllGlobals();
});
describe('V2.2 authentication experience', () => {
  it.each(['activate', 'reset'] as const)(
    'provides a safe missing-link state on %s without a broken form',
    async (mode) => {
      setup(mode);
      expect(screen.getByRole('heading', { name: 'This link is unavailable' })).toBeInTheDocument();
      expect(screen.queryByLabelText(/^New password/)).not.toBeInTheDocument();
      expect(screen.queryByLabelText(/token/i)).not.toBeInTheDocument();
      await userEvent.click(screen.getByRole('link', { name: 'Back to sign in' }));
      expect(screen.getByRole('heading', { name: 'Sign in to SIAMIS' })).toBeInTheDocument();
    },
  );
  it.each(['network', 'server'] as const)(
    'announces %s feedback separately from credential rejection and preserves entered username',
    async (kind) => {
      setup('login', vi.fn().mockRejectedValue(new ApiError(kind, kind === 'network' ? 0 : 503)));
      await userEvent.type(screen.getByLabelText(/^Username/), 'synthetic user');
      await userEvent.type(screen.getByLabelText(/^Password/), 'synthetic password');
      await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Service unavailable');
      expect(alert).toHaveTextContent("We couldn't connect to SIAMIS. Please try again.");
      expect(alert).not.toHaveTextContent('provided credentials');
      expect(screen.getByLabelText(/^Username/)).toHaveValue('synthetic user');
      expect(screen.getByLabelText(/^Password/)).toHaveAttribute('type', 'password');
    },
  );
  it('preserves password value and toggle focus when revealing and concealing it', async () => {
    setup('login');
    const password = screen.getByLabelText(/^Password/);
    await userEvent.type(password, 'synthetic password');
    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(password).toHaveValue('synthetic password');
    expect(screen.getByRole('button', { name: 'Hide password' })).toHaveFocus();
    await userEvent.keyboard(' ');
    expect(password).toHaveAttribute('type', 'password');
    expect(password).toHaveValue('synthetic password');
    expect(screen.getByRole('button', { name: 'Show password' })).toHaveFocus();
  });
  it('announces throttling as retry guidance rather than credential or connection failure', async () => {
    setup('login', vi.fn().mockRejectedValue(new ApiError('server', 429)));
    await userEvent.type(screen.getByLabelText(/^Username/), 'synthetic user');
    await userEvent.type(screen.getByLabelText(/^Password/), 'synthetic password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Please wait before retrying');
    expect(alert).toHaveTextContent('Too many requests.');
    expect(alert).not.toHaveTextContent('Service unavailable');
    expect(alert).not.toHaveTextContent('provided credentials');
  });
  it('provides recovery navigation using the existing email identifier', async () => {
    setup('forgot');
    expect(screen.getByLabelText(/^Email/)).toHaveAttribute('type', 'email');
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Request password reset' }));
    const error = await screen.findByText('Enter your email.');
    expect(error).toHaveAttribute('role', 'alert');
    expect(screen.getByLabelText(/^Email/)).toHaveAccessibleDescription('Enter your email.');
    await userEvent.click(screen.getByRole('link', { name: 'Back to sign in' }));
    expect(screen.getByLabelText(/^Username/)).toBeInTheDocument();
  });
  it.each(['activate', 'reset'] as const)(
    'shows %s completion without authenticating or exposing credentials',
    async (mode) => {
      const fetcher = vi.fn(async (url: string) =>
        url.endsWith('/csrf')
          ? new Response(JSON.stringify({ token: 'synthetic csrf' }), { status: 200 })
          : new Response(null, { status: 204 }),
      );
      vi.stubGlobal('fetch', fetcher);
      const login = vi.fn();
      setup(mode, login, true);
      await userEvent.type(screen.getByLabelText(/^New password/), 'synthetic password');
      await userEvent.type(screen.getByLabelText(/^Confirm password/), 'synthetic password');
      await userEvent.click(
        screen.getByRole('button', {
          name: mode === 'activate' ? 'Activate account' : 'Reset password',
        }),
      );
      expect(
        await screen.findByRole('heading', {
          name: mode === 'activate' ? 'Account setup complete' : 'Password updated',
        }),
      ).not.toHaveFocus();
      expect(login).not.toHaveBeenCalled();
      expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute(
        'href',
        '/login',
      );
      expect(screen.queryByText('synthetic-v22-fixture')).not.toBeInTheDocument();
      expect(window.location.search).toBe('');
    },
  );
  it('keeps rejected secure links safe and retryable without raw server detail', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async (url: string) =>
          new Response(
            JSON.stringify(
              url.endsWith('/csrf')
                ? { token: 'synthetic csrf' }
                : { detail: 'private token explanation' },
            ),
            { status: url.endsWith('/csrf') ? 200 : 400 },
          ),
      ),
    );
    setup('reset', undefined, true);
    await userEvent.type(screen.getByLabelText(/^New password/), 'synthetic password');
    await userEvent.type(screen.getByLabelText(/^Confirm password/), 'synthetic password');
    await userEvent.click(screen.getByRole('button', { name: 'Reset password' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Check your password or request a new link',
    );
    expect(screen.queryByText('private token explanation')).not.toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Reset password' })).toBeEnabled(),
    );
  });
});
