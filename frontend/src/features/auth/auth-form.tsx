import { useEffect, useState } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { Eye, EyeOff } from 'lucide-react';
import { FormField } from '../../components/shared/form-field';
import { Input } from '../../components/ui/controls';
import { Button } from '../../components/ui/button';
import { SystemState } from '../../components/shared/workspace';
import { AuthHeader, AuthStatus } from './auth-presentation';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { useAuth } from '../../lib/auth/auth-context';
import { safeReturnUrl } from '../../lib/auth/return-url';
import { readCredentialLink, clearCredentialLink } from '../../lib/auth/credential-link';
import { LoginLink } from './auth-layout';
type Mode = 'login' | 'activate' | 'forgot' | 'reset' | 'change';
type Values = {
  userName: string;
  email: string;
  password: string;
  confirm: string;
  currentPassword: string;
};
const titles: Record<Mode, string> = {
  login: 'Sign in to SIAMIS',
  activate: 'Set up your SIAMIS account',
  forgot: 'Forgot your password?',
  reset: 'Reset your password',
  change: 'Change your password',
};
export default function AuthForm({ mode }: { mode: Mode }) {
  const { state, login, refresh, logout } = useAuth();
  const [params] = useSearchParams();
  const [link] = useState(readCredentialLink);
  const [success, setSuccess] = useState(false);
  const [visible, setVisible] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
    getValues,
    setError,
    clearErrors,
    setFocus,
  } = useForm<Values>({
    defaultValues: { userName: '', email: '', password: '', confirm: '', currentPassword: '' },
  });
  useEffect(() => {
    if (mode === 'login') setFocus('userName');
  }, [mode, success, setFocus]);
  const newPassword = ['activate', 'reset', 'change'].includes(mode);
  const credential = mode === 'activate' || mode === 'reset';
  useEffect(() => {
    document.title = `${titles[mode]} · SIAMIS`;
    return () => {
      if (credential) clearCredentialLink();
    };
  }, [mode, credential]);
  if (mode === 'login' && state.status === 'authenticated')
    return (
      <Navigate
        to={
          state.user?.requiresPasswordChange
            ? '/change-password'
            : safeReturnUrl(params.get('returnTo'))
        }
        replace
      />
    );
  if (mode === 'change' && state.status !== 'authenticated')
    return <Navigate to="/login" replace />;
  if (mode === 'change' && !state.user?.requiresPasswordChange) return <Navigate to="/" replace />;
  if (success)
    return (
      <div className="auth-content">
        <AuthHeader
          title={
            mode === 'forgot'
              ? 'Check your email'
              : mode === 'activate'
                ? 'Account setup complete'
                : 'Password updated'
          }
        />
        <AuthStatus intent="success">
          {mode === 'forgot'
            ? 'If an eligible account matches the information provided, password reset instructions have been issued.'
            : mode === 'activate'
              ? 'Your account has been activated. You can now sign in.'
              : 'Your password has been reset. Sign in with your new password.'}
        </AuthStatus>
        <LoginLink />
      </div>
    );
  if (credential && !link)
    return (
      <div className="auth-content">
        <AuthHeader title={titles[mode]} />
        <SystemState kind="configuration" title="This link is unavailable">
          Use the complete link provided to you. It may be missing or no longer valid.
          {mode === 'activate'
            ? ' Contact your school administrator if you need a new activation link.'
            : ' Return to sign in and use Forgot password? to request a new recovery link.'}
        </SystemState>
        <LoginLink />
      </div>
    );
  async function submit(v: Values) {
    try {
      if (mode === 'login') await login(v.userName, v.password);
      else if (mode === 'forgot') {
        await api('/api/auth/forgot-password', { method: 'POST', body: { email: v.email } });
        setSuccess(true);
      } else if (mode === 'change') {
        await api('/api/auth/change-password', {
          method: 'POST',
          body: { currentPassword: v.currentPassword, newPassword: v.password },
        });
        await refresh();
      } else {
        await api(`/api/auth/${mode === 'activate' ? 'activate' : 'reset-password'}`, {
          method: 'POST',
          body: { userId: link!.userId, token: link!.token, newPassword: v.password },
        });
        clearCredentialLink();
        setSuccess(true);
      }
      reset();
    } catch (error) {
      const e = error instanceof ApiError ? error : null;
      const message =
        e?.status === 429
          ? 'Too many requests. Please wait before trying again.'
          : e && (e.kind === 'network' || e.kind === 'server')
            ? "We couldn't connect to SIAMIS. Please try again."
            : mode === 'login'
              ? 'Unable to sign in with the provided credentials.'
              : mode === 'forgot'
                ? 'Unable to request recovery. Please try again.'
                : 'Unable to complete this password operation. Check your password or request a new link.';
      setError('root', {
        type:
          e?.status === 429
            ? 'throttled'
            : e && (e.kind === 'network' || e.kind === 'server')
              ? 'connection'
              : 'authentication',
        message,
      });
    }
  }
  return (
    <div className="auth-content">
      <AuthHeader title={titles[mode]}>
        {mode === 'login'
          ? 'Use your school account to continue.'
          : mode === 'forgot'
            ? 'Enter the verified email associated with your account.'
            : 'Choose a password of 12–256 characters. No specific character combination is required.'}
      </AuthHeader>
      {mode === 'login' && state.status === 'expired' && (
        <AuthStatus intent="info">Your session has ended. Please sign in again.</AuthStatus>
      )}
      <form
        className="auth-form"
        onSubmit={(event) => {
          if (mode === 'login') clearErrors('root');
          void handleSubmit(submit)(event);
        }}
        onChange={(event) => {
          if (
            mode === 'login' &&
            event.target instanceof HTMLInputElement &&
            (event.target.name === 'userName' || event.target.name === 'password')
          )
            clearErrors('root');
        }}
        noValidate
      >
        {mode === 'login' && (
          <FormField id="userName" label="Username" required error={errors.userName?.message}>
            {(props) => (
              <Input
                {...props}
                autoComplete="username"
                maxLength={256}
                {...register('userName', {
                  required: 'Enter your username.',
                  maxLength: { value: 256, message: 'Use no more than 256 characters.' },
                })}
              />
            )}
          </FormField>
        )}
        {mode === 'forgot' && (
          <FormField id="email" label="Email" required error={errors.email?.message}>
            {(props) => (
              <Input
                {...props}
                type="email"
                autoComplete="email"
                maxLength={256}
                {...register('email', {
                  required: 'Enter your email.',
                  maxLength: { value: 256, message: 'Use no more than 256 characters.' },
                  pattern: {
                    value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                    message: 'Enter a valid email address.',
                  },
                })}
              />
            )}
          </FormField>
        )}
        {mode === 'change' && (
          <FormField
            id="currentPassword"
            label="Current password"
            required
            error={errors.currentPassword?.message}
          >
            {(props) => (
              <Input
                {...props}
                type="password"
                autoComplete="current-password"
                maxLength={256}
                {...register('currentPassword', { required: 'Enter your current password.' })}
              />
            )}
          </FormField>
        )}
        {mode !== 'forgot' && (
          <FormField
            id="password"
            label={newPassword ? 'New password' : 'Password'}
            required
            error={errors.password?.message}
          >
            {(props) => (
              <div className="auth-password-field">
                <Input
                  {...props}
                  type={visible ? 'text' : 'password'}
                  autoComplete={newPassword ? 'new-password' : 'current-password'}
                  maxLength={256}
                  {...register('password', {
                    required: 'Enter your password.',
                    minLength: newPassword
                      ? { value: 12, message: 'Use at least 12 characters.' }
                      : undefined,
                    maxLength: { value: 256, message: 'Use no more than 256 characters.' },
                  })}
                />
                <Button
                  type="button"
                  icon
                  variant="ghost"
                  className="auth-password-toggle"
                  aria-label={visible ? 'Hide password' : 'Show password'}
                  aria-pressed={visible}
                  onClick={() => setVisible(!visible)}
                >
                  {visible ? (
                    <EyeOff aria-hidden="true" className="size-4" />
                  ) : (
                    <Eye aria-hidden="true" className="size-4" />
                  )}
                </Button>
              </div>
            )}
          </FormField>
        )}
        {newPassword && (
          <FormField id="confirm" label="Confirm password" required error={errors.confirm?.message}>
            {(props) => (
              <Input
                {...props}
                type="password"
                autoComplete="new-password"
                maxLength={256}
                {...register('confirm', {
                  required: 'Confirm your password.',
                  validate: (value) => value === getValues('password') || 'Passwords must match.',
                })}
              />
            )}
          </FormField>
        )}
        {errors.root?.message && (
          <AuthStatus
            intent={
              errors.root.type === 'throttled'
                ? 'warning'
                : errors.root.type === 'connection'
                  ? 'connection'
                  : 'error'
            }
          >
            {errors.root.message}
          </AuthStatus>
        )}
        {mode === 'login' && (
          <div className="auth-recovery-link">
            <Link to="/forgot-password" className="auth-secondary-link">
              Forgot password?
            </Link>
          </div>
        )}
        <Button
          type="submit"
          disabled={isSubmitting}
          loading={isSubmitting}
          aria-busy={isSubmitting || undefined}
          className="auth-submit w-full"
        >
          {isSubmitting
            ? 'Please wait…'
            : mode === 'login'
              ? 'Sign in'
              : mode === 'forgot'
                ? 'Request password reset'
                : mode === 'activate'
                  ? 'Activate account'
                  : mode === 'change'
                    ? 'Change password'
                    : 'Reset password'}
        </Button>
        {isSubmitting && (
          <p role="status" className="sr-only">
            Submitting your request
          </p>
        )}
      </form>
      {mode === 'login' ? null : mode === 'change' ? (
        <Button
          variant="ghost"
          disabled={isSubmitting || signingOut}
          onClick={async () => {
            setSigningOut(true);
            try {
              await logout();
            } catch {
              setError('root', { message: 'Unable to sign out. Please try again.' });
            } finally {
              setSigningOut(false);
            }
          }}
        >
          {signingOut ? 'Signing out…' : 'Sign out'}
        </Button>
      ) : (
        <LoginLink />
      )}
    </div>
  );
}
