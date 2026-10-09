import { Navigate, Outlet, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../../lib/auth/auth-context';
import { activeRoute } from '../../app/router/navigation';
import { ShellLoading } from '../../components/layout/shell-states';
import { Alert } from '../../components/ui/feedback';
import { Button } from '../../components/ui/button';
import { AuthSurface } from './auth-layout';
import { AuthHeader, AuthStatus } from './auth-presentation';
import { useLayoutEffect, useRef } from 'react';
import { createPortal } from 'react-dom';

function SessionConnectionFailure({ retry }: { retry: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useLayoutEffect(() => {
    const element = dialog.current!;
    element.showModal();
    return () => element.close();
  }, []);
  // The native top layer also suspends existing Radix portals without unmounting their drafts.
  // Portal to body so an open Radix dialog's aria-hidden root does not hide this recovery UI.
  return createPortal(
    <dialog
      ref={dialog}
      aria-label="Session connection unavailable"
      onCancel={(event) => event.preventDefault()}
      style={{
        position: 'fixed',
        inset: 0,
        margin: 0,
        border: 0,
        padding: 0,
        width: '100vw',
        height: '100dvh',
        maxWidth: 'none',
        maxHeight: 'none',
        background: 'var(--background, #F8F7F4)',
        pointerEvents: 'auto',
      }}
    >
      <AuthSurface>
        <div className="auth-content">
          <AuthHeader title="We couldn't connect to SIAMIS." />
          <AuthStatus intent="connection">
            Your unsaved work is retained. Reconnect to verify your session before continuing.
          </AuthStatus>
          <Button autoFocus className="auth-submit" onClick={retry}>
            Try again
          </Button>
        </div>
      </AuthSurface>
    </dialog>,
    document.body,
  );
}

export function SessionBoundary() {
  const { state, refresh } = useAuth();
  const location = useLocation();
  if (state.status === 'bootstrapping') return <ShellLoading />;
  if (state.status === 'error' && location.pathname !== '/login')
    return (
      <AuthSurface>
        <div className="auth-content">
          <AuthHeader title="We couldn't connect to SIAMIS." />
          <AuthStatus intent="connection">
            Please try again. Your credentials have not been rejected.
          </AuthStatus>
          <Button className="auth-submit" onClick={() => void refresh()}>
            Try again
          </Button>
        </div>
      </AuthSurface>
    );
  const unavailable = state.revalidation === 'unavailable';
  return (
    <>
      {unavailable && <SessionConnectionFailure retry={() => void refresh()} />}
      <div hidden={!!unavailable} inert={!!unavailable} key={state.contextVersion ?? 0}>
        <Outlet />
      </div>
    </>
  );
}
export function ProtectedRoutes() {
  const { state } = useAuth();
  const location = useLocation();
  if (state.status !== 'authenticated')
    return <Navigate to={`/login?returnTo=${encodeURIComponent(location.pathname)}`} replace />;
  if (state.user?.requiresPasswordChange) return <Navigate to="/change-password" replace />;
  return <Outlet />;
}
export function CapabilityRoute() {
  const { state } = useAuth();
  const { pathname } = useLocation();
  const capability = activeRoute(pathname)?.capability;
  if (capability && !state.user?.capabilities.includes(capability))
    return (
      <Alert intent="warning" title="Access denied">
        <p>You don't have permission to access this area.</p>
        <Link to="/" className="mt-2 inline-flex min-h-11 items-center text-primary underline">
          Return to Dashboard
        </Link>
      </Alert>
    );
  return <Outlet />;
}
