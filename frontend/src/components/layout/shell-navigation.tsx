import { NavLink, useLocation } from 'react-router-dom';
import { DropdownMenu as M } from 'radix-ui';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '../ui/button';
import {
  availableModules,
  currentModule,
  moduleDestinations,
  destinationLabel,
} from '../../app/router/navigation';
import { useNavigationSession } from '../../lib/auth/navigation-session';
export function ShellNavigation({ onNavigate }: { onNavigate?: () => void }) {
  const session = useNavigationSession();
  const { pathname } = useLocation();
  const available = availableModules(session.capabilities);
  const current = currentModule(pathname);
  const selected = available.find((module) => module.id === current?.id) ?? available[0];
  return (
    <div className="shell-navigation">
      <M.Root>
        <M.Trigger asChild>
          <Button
            variant="outline"
            className="shell-module-trigger"
            aria-label={`Switch module: ${selected?.label ?? 'Workspace'}`}
          >
            {selected && <selected.icon aria-hidden="true" className="size-5 shrink-0" />}
            <span>
              <strong>{selected?.label ?? 'Workspace'}</strong>
            </span>
            <ChevronsUpDown aria-hidden="true" className="size-4 shrink-0" />
          </Button>
        </M.Trigger>
        <M.Portal>
          <M.Content align="start" sideOffset={4} className="ui-floating shell-module-menu">
            <M.Label className="shell-menu-label">Switch module</M.Label>
            {available.map((module) => (
              <M.Item key={module.id} asChild>
                <NavLink
                  to={moduleDestinations(module, session.capabilities)[0].path}
                  onClick={onNavigate}
                  className="ui-menu-item shell-module-item"
                >
                  <module.icon aria-hidden="true" className="size-4 shrink-0" />
                  <span>{module.label}</span>
                  {selected?.id === module.id && (
                    <>
                      <Check aria-hidden="true" className="size-4" />
                      <span className="sr-only">Current module</span>
                    </>
                  )}
                </NavLink>
              </M.Item>
            ))}
          </M.Content>
        </M.Portal>
      </M.Root>
      <nav aria-label="Module navigation">
        <p className="shell-navigation-label">Destinations</p>
        {selected &&
          moduleDestinations(selected, session.capabilities).map((route) => (
            <NavLink
              key={route.path}
              to={route.path}
              end={route.path !== '/hr/employees' && route.path !== '/hr/attendance'}
              onClick={onNavigate}
              className="shell-destination"
            >
              <route.icon aria-hidden="true" className="size-5 shrink-0" />
              <span>{destinationLabel(route)}</span>
            </NavLink>
          ))}
      </nav>
      {selected?.id !== 'workspace' && (
        <nav aria-label="Workspace navigation" className="shell-workspace-link">
          <NavLink to="/" end onClick={onNavigate} className="shell-destination">
            Workspace overview
          </NavLink>
        </nav>
      )}
    </div>
  );
}
