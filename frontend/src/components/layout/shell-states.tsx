import { Link } from 'react-router-dom';
import { Alert, EmptyState, Skeleton } from '../ui/feedback';
import { paths } from '../../app/router/navigation';
export function ShellLoading() {
  return (
    <div
      role="status"
      aria-label="Preparing your workspace"
      className="mx-auto w-full max-w-6xl space-y-5 p-6"
    >
      <p className="text-sm text-muted-foreground">Preparing your workspace…</p>
      <Skeleton className="h-8 w-56" />
      <Skeleton className="h-32 w-full" />
    </div>
  );
}
export function ShellError() {
  return (
    <Alert intent="danger" title="This view could not be displayed">
      <p>
        Refresh the page, or return to your workspace. Contact support if the problem continues.
      </p>
      <Link
        to={paths.dashboard}
        className="mt-3 inline-flex min-h-11 items-center font-semibold underline"
      >
        Return to Dashboard
      </Link>
    </Alert>
  );
}
export function ShellNotFound() {
  return (
    <EmptyState
      title="Page not found"
      action={
        <Link
          to={paths.dashboard}
          className="inline-flex min-h-11 items-center text-primary underline"
        >
          Return to Dashboard
        </Link>
      }
    >
      The page is unavailable. Choose a workspace from the navigation.
    </EmptyState>
  );
}
