import {
  createElement,
  type ReactNode,
  type ComponentPropsWithoutRef,
  type ComponentProps,
} from 'react';
import { CircleAlert, Inbox, Search, Settings2, Shield, Unplug } from 'lucide-react';
import { cn } from '../../lib/utils/cn';

// The frame owns outer gutters and section rhythm; its header adds no second inset.
export function ContentFrame({
  width = 'workspace',
  density = 'comfortable',
  className,
  ...props
}: ComponentProps<'div'> & {
  width?: 'workspace' | 'reading' | 'form';
  density?: 'comfortable' | 'compact';
}) {
  return (
    <div
      className={cn('ui-content-frame', className)}
      data-width={width}
      data-density={density}
      {...props}
    />
  );
}

export function WorkspaceHeader({
  title,
  description,
  context,
  actions,
}: {
  title: string;
  description?: string;
  context?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="ui-workspace-header">
      <div>
        {context && <div className="ui-context">{context}</div>}
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </header>
  );
}
export function FilterBar({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('ui-filter-bar', className)} {...props} />;
}
// Three shared rows keep controls aligned even when only one field has help text.
export function FilterToolbar({
  as: Tag = 'div',
  className,
  ...props
}: ComponentPropsWithoutRef<'form'> & { as?: 'div' | 'form' }) {
  return createElement(Tag, { ...props, className: cn('ui-filter-toolbar', className) });
}
export function TableViewport({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="ui-table-viewport" role="region" aria-label={label} tabIndex={0}>
      {children}
    </div>
  );
}
export function MetricStrip({
  items,
  density = 'comfortable',
}: {
  items: { label: string; value: ReactNode; context: string }[];
  density?: 'comfortable' | 'compact';
}) {
  return (
    <dl className="ui-metrics" data-density={density}>
      {items.map((item) => (
        <div key={item.label}>
          <dt>{item.label}</dt>
          <dd>
            {item.value}
            <p>{item.context}</p>
          </dd>
        </div>
      ))}
    </dl>
  );
}
export function QueueList({
  label,
  items,
}: {
  label: string;
  items: { id: string; title: string; metadata: string; status?: ReactNode; action?: ReactNode }[];
}) {
  return (
    <ul className="ui-queue" aria-label={label}>
      {items.map((item) => (
        <li key={item.id}>
          <div className="min-w-0">
            <h3>{item.title}</h3>
            <p>{item.metadata}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {item.status}
            {item.action}
          </div>
        </li>
      ))}
    </ul>
  );
}
const stateIcons = {
  empty: Inbox,
  search: Search,
  filtered: Search,
  configuration: Settings2,
  disconnected: Unplug,
  permission: Shield,
  error: CircleAlert,
};
export function SystemState({
  kind,
  title,
  children,
  action,
}: {
  kind: keyof typeof stateIcons;
  title: string;
  children: ReactNode;
  action?: ReactNode;
}) {
  const Icon = stateIcons[kind];
  return (
    <div className="ui-state" data-kind={kind} role={kind === 'error' ? 'alert' : undefined}>
      <Icon aria-hidden="true" />
      <div className="min-w-0">
        <h3>{title}</h3>
        <div className="ui-state-description">{children}</div>
        {action && <div className="mt-3">{action}</div>}
      </div>
    </div>
  );
}
export function RecordSummary({
  title,
  metadata,
  status,
  actions,
}: {
  title: string;
  metadata: string;
  status?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="ui-record">
      <div>
        <h3>{title}</h3>
        <p>{metadata}</p>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        {status}
        {actions}
      </div>
    </header>
  );
}
