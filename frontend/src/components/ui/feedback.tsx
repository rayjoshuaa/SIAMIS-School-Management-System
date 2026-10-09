import type { ComponentProps, ReactNode } from 'react';
import { Avatar as A, Separator as S } from 'radix-ui';
import { LoaderCircle, Inbox } from 'lucide-react';
import { cn } from '../../lib/utils/cn';
export type Intent = 'success' | 'warning' | 'danger' | 'info' | 'neutral';
const intents: Record<Intent, string> = {
  success: 'text-success bg-[var(--success-surface)]',
  warning: 'text-warning bg-[var(--warning-surface)]',
  danger: 'text-destructive bg-[var(--danger-surface)]',
  info: 'text-info bg-[var(--info-surface)]',
  neutral: 'text-muted-foreground bg-muted',
};
export function Badge({ intent = 'neutral', children }: { intent?: Intent; children: ReactNode }) {
  return (
    <span
      data-intent={intent}
      className={cn('ui-status inline-flex border border-current/20', intents[intent])}
    >
      {children}
    </span>
  );
}
export function Card({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      className={cn('rounded-md border border-border bg-surface p-5 sm:p-6', className)}
      {...props}
    />
  );
}
export function Separator({ className, ...props }: ComponentProps<typeof S.Root>) {
  return <S.Root className={cn('my-4 h-px w-full bg-border', className)} {...props} />;
}
export function Alert({
  intent = 'info',
  title,
  children,
}: {
  intent?: Intent;
  title: string;
  children: ReactNode;
}) {
  return (
    <div
      className={cn('ui-alert rounded-md border border-current/20 p-4 text-sm', intents[intent])}
    >
      <p className="font-semibold">{title}</p>
      <div className="mt-1">{children}</div>
    </div>
  );
}
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-md bg-muted', className)} />;
}
export function Spinner({ label = 'Loading' }: { label?: string }) {
  return (
    <span role="status" className="inline-flex items-center gap-2 text-sm text-muted-foreground">
      <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />
      {label}
    </span>
  );
}
export function Avatar({ name, src }: { name: string; src?: string }) {
  return (
    <A.Root className="inline-flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-secondary text-sm font-semibold">
      <A.Image src={src} alt={name} />
      <A.Fallback aria-label={name}>
        {name
          .split(' ')
          .map((x) => x[0])
          .slice(0, 2)
          .join('')}
      </A.Fallback>
    </A.Root>
  );
}
export function EmptyState({
  title,
  children,
  action,
}: {
  title: string;
  children: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-md border border-dashed border-border px-5 py-10 text-center">
      <Inbox aria-hidden="true" className="size-6 text-muted-foreground" />
      <h3 className="font-semibold">{title}</h3>
      <div className="max-w-sm text-sm text-muted-foreground">{children}</div>
      {action}
    </div>
  );
}
