import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../lib/auth/auth-context';
import { AuthLayout } from '../features/auth/auth-layout';
import AuthForm from '../features/auth/auth-form';
import { captureCredentialLink, clearCredentialLink } from '../lib/auth/credential-link';

type Mode = 'login' | 'activate' | 'forgot' | 'reset';
const paths: Record<Mode, string> = {
  login: '/login',
  activate: '/activate',
  forgot: '/forgot-password',
  reset: '/reset-password',
};
function setup(mode: Mode, login = vi.fn().mockResolvedValue(undefined)) {
  if (mode === 'activate' || mode === 'reset') {
    window.history.replaceState(
      null,
      '',
      `${paths[mode]}?userId=11111111-1111-1111-1111-111111111111&token=synthetic-not-a-real-token`,
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
describe('authentication presentation family', () => {
  it.each(['login', 'activate', 'forgot', 'reset'] as const)(
    'shares real school branding and one task heading on %s',
    (mode) => {
      setup(mode);
      expect(
        screen.getByRole('img', { name: 'Siam International School crest' }),
      ).toBeInTheDocument();
      expect(screen.getByText('SIAM International School')).toBeInTheDocument();
      expect(screen.getByText('School Management System')).toBeInTheDocument();
      const headings = screen.getAllByRole('heading', { level: 1 });
      expect(headings).toHaveLength(1);
      expect(headings[0]).not.toHaveAttribute('tabindex');
      expect(headings[0]).not.toHaveFocus();
      expect(screen.queryByText('synthetic-not-a-real-token')).not.toBeInTheDocument();
    },
  );
  it('keeps login public activation-free and browser autocomplete intact', () => {
    setup('login');
    expect(screen.queryByRole('link', { name: /activate/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /activate/i })).not.toBeInTheDocument();
    expect(screen.getByLabelText(/^Username/)).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText(/^Password/)).toHaveAttribute('autocomplete', 'current-password');
  });
  it.each(['activate', 'reset'] as const)(
    'retains new-password autocomplete and accessible pressed toggle on %s',
    async (mode) => {
      setup(mode);
      expect(screen.getByLabelText(/^New password/)).toHaveAttribute(
        'autocomplete',
        'new-password',
      );
      expect(screen.getByLabelText(/^Confirm password/)).toHaveAttribute(
        'autocomplete',
        'new-password',
      );
      const toggle = screen.getByRole('button', { name: 'Show password' });
      expect(toggle).toHaveAttribute('aria-pressed', 'false');
      await userEvent.click(toggle);
      expect(screen.getByRole('button', { name: 'Hide password' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      expect(screen.getByLabelText(/^New password/)).toHaveAttribute('type', 'text');
      expect(screen.getByLabelText(/^Confirm password/)).toHaveAttribute('type', 'password');
    },
  );
  it('announces submitting, prevents a second command and preserves password visibility navigation', async () => {
    let resolve!: () => void;
    const login = vi.fn(
      () =>
        new Promise<void>((done) => {
          resolve = done;
        }),
    );
    setup('login', login);
    await userEvent.type(screen.getByLabelText(/^Username/), 'synthetic user');
    await userEvent.type(screen.getByLabelText(/^Password/), 'synthetic password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    const submitting = await screen.findByRole('button', { name: 'Please wait…' });
    expect(submitting).toBeDisabled();
    expect(submitting).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByRole('status')).toHaveTextContent('Submitting your request');
    await userEvent.click(submitting);
    expect(login).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('link', { name: 'Forgot password?' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show password' })).toBeEnabled();
    resolve();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled());
  });
  it('keeps recovery success neutral and provides the existing sign-in next action', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async (url: string) =>
          new Response(
            url.endsWith('/csrf')
              ? JSON.stringify({ token: 'synthetic csrf' })
              : JSON.stringify({}),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          ),
      ),
    );
    setup('forgot');
    await userEvent.type(screen.getByLabelText(/^Email/), 'synthetic@example.invalid');
    await userEvent.click(screen.getByRole('button', { name: 'Request password reset' }));
    expect(await screen.findByRole('heading', { name: 'Check your email' })).not.toHaveFocus();
    expect(screen.getByRole('status')).toHaveTextContent('If an eligible account matches');
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', '/login');
  });
});
