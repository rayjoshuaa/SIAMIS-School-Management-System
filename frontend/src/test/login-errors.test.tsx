import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import AuthForm from '../features/auth/auth-form';
import { AuthContext } from '../lib/auth/auth-context';
import { ApiError } from '../lib/api/errors';

const authenticationError = 'Unable to sign in with the provided credentials.';
const connectionError = "We couldn't connect to SIAMIS. Please try again.";

function setup(error = new ApiError('unauthorized', 401)) {
  const login = vi.fn().mockRejectedValue(error);
  render(
    <AuthContext.Provider
      value={{ state: { status: 'anonymous' }, login, refresh: vi.fn(), logout: vi.fn() }}
    >
      <MemoryRouter>
        <AuthForm mode="login" />
      </MemoryRouter>
    </AuthContext.Provider>,
  );
  return { login, user: userEvent.setup() };
}

describe('login field validation and authentication errors', () => {
  it('focuses Username on entry instead of making the login heading focusable', async () => {
    setup();
    await waitFor(() => expect(screen.getByLabelText(/^Username/)).toHaveFocus());
    const heading = screen.getByRole('heading', { name: 'Sign in to SIAMIS' });
    expect(heading).not.toHaveFocus();
    expect(heading).not.toHaveAttribute('tabindex');
  });

  it('preserves the keyboard focus order across every login control', async () => {
    const { user } = setup();
    await waitFor(() => expect(screen.getByLabelText(/^Username/)).toHaveFocus());
    for (const control of [
      screen.getByLabelText(/^Password/),
      screen.getByRole('button', { name: 'Show password' }),
      screen.getByRole('link', { name: 'Forgot password?' }),
      screen.getByRole('button', { name: 'Sign in' }),
    ]) {
      await user.tab();
      expect(control).toHaveFocus();
    }
  });

  it('never transfers retry focus to the login heading', async () => {
    const { user } = setup();
    await user.type(screen.getByLabelText(/^Username/), 'synthetic user');
    await user.type(screen.getByLabelText(/^Password/), 'synthetic password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByText(authenticationError);
    expect(screen.getByRole('heading', { name: 'Sign in to SIAMIS' })).not.toHaveFocus();
    await user.click(screen.getByLabelText(/^Username/));
    await user.type(screen.getByLabelText(/^Username/), ' edited');
    expect(screen.getByLabelText(/^Username/)).toHaveFocus();
  });

  it.each([
    ['', '', true, true],
    ['synthetic user', '', false, true],
    ['', 'synthetic password', true, false],
  ] as const)(
    'validates incomplete credentials locally (%s / %s)',
    async (name, password, missingName, missingPassword) => {
      const { user, login } = setup();
      if (name) await user.type(screen.getByLabelText(/^Username/), name);
      if (password) await user.type(screen.getByLabelText(/^Password/), password);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      await waitFor(() => {
        expect(Boolean(screen.queryByText('Enter your username.'))).toBe(missingName);
        expect(Boolean(screen.queryByText('Enter your password.'))).toBe(missingPassword);
      });
      expect(login).not.toHaveBeenCalled();
      expect(screen.queryByText(authenticationError)).not.toBeInTheDocument();
      expect(screen.queryByText(connectionError)).not.toBeInTheDocument();
    },
  );

  it('shows only the form-level authentication error for rejected complete credentials', async () => {
    const { user, login } = setup();
    await user.type(screen.getByLabelText(/^Username/), 'synthetic user');
    await user.type(screen.getByLabelText(/^Password/), 'synthetic password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(authenticationError);
    expect(login).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('Enter your username.')).not.toBeInTheDocument();
    expect(screen.queryByText('Enter your password.')).not.toBeInTheDocument();
    expect(screen.queryByText(connectionError)).not.toBeInTheDocument();
  });

  it.each(['network', 'server'] as const)(
    'separates %s failures from invalid credentials',
    async (kind) => {
      const { user } = setup(new ApiError(kind, kind === 'network' ? 0 : 500));
      await user.type(screen.getByLabelText(/^Username/), 'synthetic user');
      await user.type(screen.getByLabelText(/^Password/), 'synthetic password');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      expect(await screen.findByRole('alert')).toHaveTextContent(connectionError);
      expect(screen.queryByText(authenticationError)).not.toBeInTheDocument();
      expect(screen.queryByText('Enter your username.')).not.toBeInTheDocument();
      expect(screen.queryByText('Enter your password.')).not.toBeInTheDocument();
    },
  );

  it.each(['Username', 'Password'])(
    'clears stale general errors when editing %s',
    async (field) => {
      const { user } = setup();
      await user.type(screen.getByLabelText(/^Username/), 'synthetic user');
      await user.type(screen.getByLabelText(/^Password/), 'synthetic password');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      await screen.findByText(authenticationError);
      await user.clear(screen.getByLabelText(new RegExp(`^${field}`)));
      expect(screen.queryByText(authenticationError)).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      expect(
        await screen.findByText(
          field === 'Username' ? 'Enter your username.' : 'Enter your password.',
        ),
      ).toBeInTheDocument();
      expect(screen.queryByText(authenticationError)).not.toBeInTheDocument();
    },
  );

  it('clears the previous general error while a new complete attempt is pending', async () => {
    const { user, login } = setup();
    await user.type(screen.getByLabelText(/^Username/), 'synthetic user');
    await user.type(screen.getByLabelText(/^Password/), 'synthetic password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByText(authenticationError);
    let complete!: () => void;
    login.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => {
          complete = resolve;
        }),
    );
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(screen.queryByText(authenticationError)).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Please wait…' })).toBeDisabled();
    complete();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled());
  });
});
