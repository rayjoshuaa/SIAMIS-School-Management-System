import { useEffect, useState, useRef } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { Eye, EyeOff } from 'lucide-react';
import { FormField } from '../../components/shared/form-field';
import { Input } from '../../components/ui/controls';
import { Button } from '../../components/ui/button';
import { Alert } from '../../components/ui/feedback';
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
  activate: 'Activate your account',
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
  const heading = useRef<HTMLHeadingElement>(null);
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
    else heading.current?.focus();
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
      <div className="space-y-4">
        <h1 ref={heading} tabIndex={-1} className="text-2xl font-semibold">
          {mode === 'forgot' ? 'Check your email' : 'Password established'}
        </h1>
        <p role="status" className="text-sm">
          {mode === 'forgot'
            ? 'If an eligible account matches the information provided, password reset instructions have been issued.'
            : mode === 'activate'
              ? 'Your account has been activated. You can now sign in.'
              : 'Your password has been reset. Sign in with your new password.'}
        </p>
        <LoginLink />
      </div>
    );
  if (credential && !link)
    return (
      <div className="space-y-4">
        <h1 ref={heading} tabIndex={-1} className="text-2xl font-semibold">
          {titles[mode]}
        </h1>
        <Alert intent="warning" title="This link is unavailable">
          Use the complete link provided to you. It may be missing or no longer valid.
        </Alert>
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
      setError('root', { message });
    }
  }
  return (
    <div className={mode === 'login' ? 'space-y-4' : 'space-y-5'}>
      <div className={mode === 'login' ? 'text-center' : undefined}>
        <h1
          ref={mode === 'login' ? undefined : heading}
          tabIndex={mode === 'login' ? undefined : -1}
          className={mode === 'login' ? 'text-lg font-semibold' : 'text-2xl font-semibold'}
        >
          {titles[mode]}
        </h1>
        <p className={`${mode === 'login' ? 'mt-1' : 'mt-2'} text-sm text-muted-foreground`}>
          {mode === 'login'
            ? 'Use your school account to continue.'
            : mode === 'forgot'
              ? 'Enter the verified email associated with your account.'
              : 'Choose a password of 12–256 characters. No specific character combination is required.'}
        </p>
      </div>
      {mode === 'login' && state.status === 'expired' && (
        <p role="status" className="text-sm">
          Your session has ended. Please sign in again.
        </p>
      )}
      <form
        className={mode === 'login' ? 'space-y-3' : 'space-y-4'}
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
              <div className="flex gap-2">
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
                  variant="outline"
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
          <Alert intent="danger" title="Account access">
            <p role={mode === 'login' ? 'alert' : undefined}>{errors.root.message}</p>
          </Alert>
        )}
        {mode === 'login' && (
          <div className="flex justify-end">
            <Link
              to="/forgot-password"
              className="inline-flex min-h-11 items-center text-sm text-primary underline"
            >
              Forgot password?
            </Link>
          </div>
        )}
        <Button type="submit" disabled={isSubmitting} className="w-full">
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
