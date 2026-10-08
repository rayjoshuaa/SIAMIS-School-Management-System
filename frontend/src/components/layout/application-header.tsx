import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { currentModule, activeRoute, destinationLabel } from '../../app/router/navigation';
export function ApplicationHeader({
  navigation,
  account,
}: {
  navigation: ReactNode;
  account: ReactNode;
}) {
  const { pathname } = useLocation();
  const module = currentModule(pathname);
  const route = activeRoute(pathname);
  return (
    <header className="shell-header">
      <div className="shell-header-context">
        {navigation}
        <div>
          <span>{module?.label ?? 'Workspace'}</span>
          <strong>{route ? destinationLabel(route) : 'Page not found'}</strong>
        </div>
      </div>
      {account}
    </header>
  );
}
