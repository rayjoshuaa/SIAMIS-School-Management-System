import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext, type AuthState } from '../lib/auth/auth-context';
import { SessionBoundary } from '../features/auth/auth-boundaries';
import { AuthLayout } from '../features/auth/auth-layout';
import AuthForm from '../features/auth/auth-form';

function renderAccess(status: AuthState['status'], path = '/login') {
  const login = vi.fn();
  const refresh = vi.fn();
  render(
    <AuthContext.Provider
      value={{ state: { status } as AuthState, login, refresh, logout: vi.fn() }}
    >
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<SessionBoundary />}>
            <Route element={<AuthLayout />}>
              <Route path="/login" element={<AuthForm mode="login" />} />
            </Route>
            <Route path="/protected" element={<p>Protected fixture</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
  return { login, refresh };
}

describe('V3.3 authentication presentation', () => {
  it('keeps an unavailable session probe on login inside the real form', async () => {
    renderAccess('error');
    expect(screen.getByRole('main')).toHaveClass('auth-page');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Sign in to SIAMIS');
    await waitFor(() => expect(screen.getByLabelText(/^Username/)).toHaveFocus());
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });
  it('brands the connection boundary without rendering protected content', async () => {
    const { refresh, login } = renderAccess('error', '/protected');
    expect(screen.getByRole('main')).toHaveClass('auth-page');
    expect(
      screen.getByRole('img', { name: 'Siam International School crest' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Your credentials have not been rejected');
    expect(screen.queryByText('Protected fixture')).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Username/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(refresh).toHaveBeenCalledOnce();
    expect(login).not.toHaveBeenCalled();
  });
  it('announces an ended session with an ordinary accessible sign-in form', () => {
    renderAccess('expired');
    expect(screen.getByRole('status')).toHaveTextContent('Your session has ended');
    expect(screen.getByLabelText(/^Password/)).toHaveAttribute('autocomplete', 'current-password');
    expect(screen.queryByRole('link', { name: /activate/i })).not.toBeInTheDocument();
  });
  it('keeps field errors associated and does not submit an empty form', async () => {
    const { login } = renderAccess('anonymous');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(screen.getByLabelText(/^Username/)).toHaveAccessibleDescription('Enter your username.');
    expect(screen.getByLabelText(/^Password/)).toHaveAccessibleDescription('Enter your password.');
    expect(screen.getAllByRole('alert')).toHaveLength(2);
    expect(screen.queryByText('Request not completed')).not.toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });
});
