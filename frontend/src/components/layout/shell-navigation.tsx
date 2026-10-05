import { useId, useState } from 'react';
import { NavLink, useLocation, matchPath } from 'react-router-dom';
import { ChevronDown } from 'lucide-react';
import { Button } from '../ui/button';
import { Tooltip } from '../ui/overlays';
import {
  visibleRoutes,
  hrIsActive,
  modules,
  schoolNavigation,
  paths,
  type RouteMeta,
} from '../../app/router/navigation';
import { useNavigationSession } from '../../lib/auth/navigation-session';
import { cn } from '../../lib/utils/cn';
export function ShellNavigation({
  collapsed = false,
  onNavigate,
  onExpand,
}: {
  collapsed?: boolean;
  onNavigate?: () => void;
  onExpand?: () => void;
}) {
  const session = useNavigationSession();
  const { pathname } = useLocation();
  const [hrExpanded, setHrExpanded] = useState(true);
  const [schoolExpanded, setSchoolExpanded] = useState(false);
  const groupId = useId();
  const visible = visibleRoutes(session.capabilities);
  const hr = visible.filter((route) => route.group === 'hr');
  function item(route: RouteMeta, nested = false) {
    const link = (
      <NavLink
        key={route.path}
        to={route.path}
        end
        onClick={onNavigate}
        aria-label={
          collapsed
            ? route.label
            : nested && route.path === paths.school
              ? 'School Management overview'
              : undefined
        }
        className={cn(
          'flex min-h-11 min-w-0 items-center gap-2 rounded-md border-l-2 border-transparent px-3 py-2 text-sm hover:bg-muted',
          nested && 'pl-5',
          collapsed && 'justify-center px-2',
          matchPath({ path: route.path, end: true }, pathname) &&
            'border-primary bg-sidebar-active font-semibold text-primary',
        )}
      >
        <route.icon className="size-4 shrink-0" aria-hidden="true" />
        {!collapsed && (
          <span className="min-w-0 break-words">
            {nested && route.path === paths.school ? 'Overview' : route.label}
          </span>
        )}
      </NavLink>
    );
    return collapsed ? (
      <Tooltip key={route.path} label={route.label}>
        {link}
      </Tooltip>
    ) : (
      link
    );
  }
  return (
    <nav aria-label="Main navigation" className="space-y-1">
      {visible.filter((route) => route.group === 'workspace').map((route) => item(route))}
      {!collapsed && (
        <p className="px-3 pt-3 pb-1 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
          School platform
        </p>
      )}
      {modules.map((module) => {
        if (module.id === 'hr' && hr.length === 0) return null;
        const school = module.id === 'school';
        const humanResources = module.id === 'hr';
        const group = school || humanResources;
        const active = school ? pathname === paths.school : humanResources && hrIsActive(pathname);
        const expanded = school ? schoolExpanded : hrExpanded;
        const label = `${module.label}${active ? ', current module' : ''}`;
        const trigger = (
          <Button
            variant="ghost"
            icon={collapsed}
            aria-label={collapsed ? `${label}${!group ? ', planned' : ''}` : label}
            aria-disabled={!group || undefined}
            aria-expanded={group ? !collapsed && expanded : undefined}
            aria-controls={group && !collapsed ? `${groupId}-${module.id}` : undefined}
            onClick={
              group
                ? () => {
                    if (collapsed) {
                      onExpand?.();
                      if (school) setSchoolExpanded(true);
                      else setHrExpanded(true);
                    } else if (school) setSchoolExpanded(!expanded);
                    else setHrExpanded(!expanded);
                  }
                : undefined
            }
            className={cn(
              'w-full justify-start gap-2 border-l-2 border-transparent px-3 text-sm font-medium',
              collapsed && 'justify-center px-2',
              active && 'border-primary bg-sidebar-active font-semibold text-primary',
              !group && 'text-muted-foreground',
            )}
          >
            <module.icon aria-hidden="true" className="size-4 shrink-0" />
            {!collapsed && (
              <>
                <span className="min-w-0 flex-1 text-left break-words">{module.label}</span>
                {group ? (
                  <ChevronDown
                    aria-hidden="true"
                    className={cn('size-3.5 shrink-0', !expanded && '-rotate-90')}
                  />
                ) : (
                  <span aria-hidden="true" className="size-1.5 shrink-0 rounded-full bg-border" />
                )}
              </>
            )}
          </Button>
        );
        return (
          <div key={module.id} data-active={active}>
            {collapsed ? (
              <Tooltip label={`${module.label}${module.status === 'planned' ? ' — planned' : ''}`}>
                {trigger}
              </Tooltip>
            ) : (
              trigger
            )}
            {!collapsed && group && expanded && (
              <div id={`${groupId}-${module.id}`} className="ml-4 border-l border-border pl-1">
                {school ? (
                  <>
                    {visible
                      .filter((route) => route.path === paths.school)
                      .map((route) => item(route, true))}
                    <p className="px-5 py-1 text-xs text-muted-foreground">Planned destinations</p>
                    {schoolNavigation.map((name) => (
                      <button
                        key={name}
                        type="button"
                        disabled
                        title="Planned — not available yet"
                        className="flex min-h-11 w-full items-center px-5 py-2 text-left text-sm text-muted-foreground"
                      >
                        {name}
                      </button>
                    ))}
                  </>
                ) : (
                  hr.map((route) => item(route, true))
                )}
              </div>
            )}
          </div>
        );
      })}
      {!collapsed && (
        <p className="px-3 pt-3 text-xs text-muted-foreground">
          Planned modules are not available yet.
        </p>
      )}
    </nav>
  );
}
