import type { ReactNode } from 'react';
import {
  Dialog as D,
  AlertDialog as A,
  Tooltip as T,
  Popover as P,
  DropdownMenu as M,
} from 'radix-ui';
import { X, MoreHorizontal } from 'lucide-react';
import { Button } from './button';
import { cn } from '../../lib/utils/cn';
const overlay = 'ui-floating fixed inset-0 z-40 bg-[var(--overlay)]';
const floating =
  'ui-floating z-50 rounded-md border border-border bg-surface p-5 shadow-[var(--shadow-overlay)]';
export function Dialog({
  trigger,
  title,
  description,
  children,
  sheet = false,
}: {
  trigger: ReactNode;
  title: string;
  description: string;
  children: ReactNode;
  sheet?: boolean;
}) {
  return (
    <D.Root>
      <D.Trigger asChild>{trigger}</D.Trigger>
      <D.Portal>
        <D.Overlay className={overlay} />
        <D.Content
          className={cn(
            floating,
            sheet
              ? 'fixed inset-y-0 right-0 w-[min(90vw,24rem)] overflow-y-auto rounded-none pt-16'
              : 'fixed top-1/2 left-1/2 max-h-[85dvh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto',
          )}
        >
          <D.Title className="pr-10 text-lg font-semibold">{title}</D.Title>
          <D.Description className="mt-2 text-sm text-muted-foreground">
            {description}
          </D.Description>
          <div className="mt-6">{children}</div>
          <D.Close asChild>
            <Button aria-label="Close" variant="ghost" icon className="absolute top-3 right-3">
              <X className="size-4" />
            </Button>
          </D.Close>
        </D.Content>
      </D.Portal>
    </D.Root>
  );
}
export function Sheet(props: Omit<Parameters<typeof Dialog>[0], 'sheet'>) {
  return <Dialog {...props} sheet />;
}
export function AlertDialog({
  trigger,
  title,
  description,
  onConfirm,
  confirmLabel = 'Confirm',
}: {
  trigger: ReactNode;
  title: string;
  description: string;
  onConfirm: () => void;
  confirmLabel?: string;
}) {
  return (
    <A.Root>
      <A.Trigger asChild>{trigger}</A.Trigger>
      <A.Portal>
        <A.Overlay className={overlay} />
        <A.Content
          className={cn(
            floating,
            'fixed top-1/2 left-1/2 w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2',
          )}
        >
          <A.Title className="text-lg font-semibold">{title}</A.Title>
          <A.Description className="mt-2 text-sm text-muted-foreground">
            {description}
          </A.Description>
          <div className="mt-6 flex flex-wrap justify-end gap-2">
            <A.Cancel asChild>
              <Button variant="outline">Cancel</Button>
            </A.Cancel>
            <A.Action asChild>
              <Button variant="destructive" onClick={onConfirm}>
                {confirmLabel}
              </Button>
            </A.Action>
          </div>
        </A.Content>
      </A.Portal>
    </A.Root>
  );
}
export function Tooltip({ label, children }: { label: string; children: ReactNode }) {
  return (
    <T.Root>
      <T.Trigger asChild>{children}</T.Trigger>
      <T.Portal>
        <T.Content sideOffset={6} className={cn(floating, 'max-w-64 px-3 py-2 text-xs')}>
          {label}
        </T.Content>
      </T.Portal>
    </T.Root>
  );
}
export function Popover({
  trigger,
  children,
  label,
}: {
  trigger: ReactNode;
  children: ReactNode;
  label: string;
}) {
  return (
    <P.Root>
      <P.Trigger asChild>{trigger}</P.Trigger>
      <P.Portal>
        <P.Content
          sideOffset={8}
          aria-label={label}
          className={cn(floating, 'max-w-[calc(100vw-2rem)] w-72')}
        >
          <div className="pr-9">{children}</div>
          <P.Close asChild>
            <Button
              variant="ghost"
              icon
              aria-label="Close popover"
              className="absolute top-2 right-2"
            >
              <X className="size-4" />
            </Button>
          </P.Close>
        </P.Content>
      </P.Portal>
    </P.Root>
  );
}
export function DropdownMenu({
  label = 'More actions',
  items,
}: {
  label?: string;
  items: { label: string; onSelect: () => void }[];
}) {
  return (
    <M.Root>
      <M.Trigger asChild>
        <Button icon variant="ghost" aria-label={label}>
          <MoreHorizontal className="size-5" />
        </Button>
      </M.Trigger>
      <M.Portal>
        <M.Content sideOffset={4} className={cn(floating, 'min-w-44 p-1')}>
          {items.map((item) => (
            <M.Item
              key={item.label}
              onSelect={item.onSelect}
              className="ui-menu-item flex min-h-11 cursor-pointer items-center rounded-md px-3 text-sm data-[highlighted]:bg-muted"
            >
              {item.label}
            </M.Item>
          ))}
        </M.Content>
      </M.Portal>
    </M.Root>
  );
}
