import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { currentModule } from '../../app/router/navigation';
export function ApplicationHeader({
  navigation,
  account,
}: {
  navigation: ReactNode;
  account: ReactNode;
}) {
  const { pathname } = useLocation();
  const module = currentModule(pathname);
  return (
    <header className="shell-header">
      <div className="shell-header-context">
        {navigation}
        {module && <module.icon aria-hidden="true" className="shell-context-icon" />}
        <strong>{module?.label ?? 'Workspace'}</strong>
      </div>
      {account}
    </header>
  );
}
