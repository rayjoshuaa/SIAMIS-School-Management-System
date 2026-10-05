import { Navigate, Outlet, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../../lib/auth/auth-context';
import { activeRoute } from '../../app/router/navigation';
import { ShellLoading } from '../../components/layout/shell-states';
import { Alert } from '../../components/ui/feedback';
import { Button } from '../../components/ui/button';
export function SessionBoundary() {
  const { state, refresh } = useAuth();
  if (state.status === 'bootstrapping') return <ShellLoading />;
  if (state.status === 'error')
    return (
      <main className="mx-auto max-w-lg p-6">
        <Alert intent="danger" title="We couldn't connect to SIAMIS.">
          Please try again. Your credentials have not been rejected.
        </Alert>
        <Button className="mt-4" onClick={() => void refresh()}>
          Try again
        </Button>
      </main>
    );
  return <Outlet />;
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
