import { useLocation } from 'react-router-dom';
import { Card, Badge } from '../../components/ui/feedback';
import { activeRoute } from '../../app/router/navigation';
export function ModulePlaceholder() {
  const route = activeRoute(useLocation().pathname);
  return (
    <Card>
      <Badge>{route?.status === 'planned' ? 'Planned module' : 'Interface foundation'}</Badge>
      <h2 className="mt-5 text-lg font-semibold">
        {route?.path === '/'
          ? 'A place for your school day'
          : `${route?.label ?? 'Workspace'} foundation`}
      </h2>
      <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
        {route?.path === '/'
          ? 'Dashboard content will be introduced in a later checkpoint.'
          : route?.status === 'planned'
            ? 'School Management will be introduced in a separately approved checkpoint.'
            : 'Feature workflows will be connected in a later checkpoint. This page contains no live records or actions.'}
      </p>
      <div className="mt-8 border-t border-border pt-4 text-xs text-muted-foreground">
        SIAMIS · Siam International School
      </div>
    </Card>
  );
}
