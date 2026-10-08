import { useEffect, useRef, useState, type ReactNode, type ComponentProps } from 'react';
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
  open,
  onOpenChange,
  dismissible = true,
  onCloseAutoFocus,
  onOpenAutoFocus,
  size,
  footer,
  dirty = false,
  pending = false,
}: {
  trigger?: ReactNode;
  title: string;
  description: string;
  children: ReactNode;
  sheet?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  dismissible?: boolean;
  onCloseAutoFocus?: ComponentProps<typeof D.Content>['onCloseAutoFocus'];
  onOpenAutoFocus?: ComponentProps<typeof D.Content>['onOpenAutoFocus'];
  size?: 'sm' | 'md' | 'lg';
  footer?: ReactNode;
  dirty?: boolean;
  pending?: boolean;
}) {
  const [internalOpen, setInternalOpen] = useState(false);
  const [discardRequested, setDiscardRequested] = useState(false);
  const isOpen = open ?? internalOpen;
  const [previousOpen, setPreviousOpen] = useState(isOpen);
  // A caller may close after saving. Do not carry a discard prompt into a later opening.
  if (previousOpen !== isOpen) {
    setPreviousOpen(isOpen);
    if (!isOpen && discardRequested) setDiscardRequested(false);
  }
  const returnFocus = useRef<HTMLElement | null>(null);
  const keepEditing = useRef<HTMLButtonElement | null>(null);
  useEffect(() => {
    if (discardRequested) keepEditing.current?.focus();
  }, [discardRequested]);
  const canDismiss = dismissible && !pending;
  const changeOpen = (next: boolean) => {
    if (!next && !canDismiss) return;
    if (!next && dirty) {
      returnFocus.current = document.activeElement as HTMLElement;
      setDiscardRequested(true);
      return;
    }
    setDiscardRequested(false);
    setInternalOpen(next);
    onOpenChange?.(next);
  };
  return (
    <D.Root open={isOpen} onOpenChange={changeOpen}>
      {trigger && <D.Trigger asChild>{trigger}</D.Trigger>}
      <D.Portal>
        <D.Overlay className={overlay} data-overlay-backdrop />
        <D.Content
          onCloseAutoFocus={onCloseAutoFocus}
          onOpenAutoFocus={onOpenAutoFocus}
          onEscapeKeyDown={(event) => {
            if (!canDismiss) event.preventDefault();
          }}
          onPointerDownOutside={(event) => {
            if (!canDismiss) event.preventDefault();
          }}
          onInteractOutside={(event) => {
            if (!canDismiss) event.preventDefault();
          }}
          className={cn(
            floating,
            'ui-overlay-panel p-0',
            sheet
              ? 'fixed inset-y-0 right-0 w-full max-w-lg rounded-none'
              : 'fixed top-1/2 left-1/2 max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2',
          )}
          data-size={size}
          data-sheet={sheet}
          aria-busy={pending || undefined}
          style={
            size
              ? {
                  maxWidth: `var(--${sheet ? 'drawer' : 'modal'}-${sheet && size === 'sm' ? 'md' : size})`,
                }
              : undefined
          }
        >
          <div className="ui-overlay-header">
            <D.Title className="text-lg font-semibold">{title}</D.Title>
            <D.Description className="mt-2 text-sm text-muted-foreground">
              {description}
            </D.Description>
            <D.Close asChild>
              <Button
                disabled={!canDismiss}
                aria-label="Close"
                variant="ghost"
                icon
                className="absolute top-3 right-3"
              >
                <X className="size-4" />
              </Button>
            </D.Close>
          </div>
          <div className="ui-overlay-body">
            {discardRequested && (
              <div role="alert" className="ui-overlay-discard">
                <p className="font-semibold">Discard unsaved changes?</p>
                <p className="mt-1 text-sm">Your changes have not been saved.</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    ref={keepEditing}
                    variant="outline"
                    onClick={() => {
                      setDiscardRequested(false);
                      returnFocus.current?.focus();
                    }}
                  >
                    Keep editing
                  </Button>
                  <Button
                    variant="destructive"
                    disabled={pending}
                    onClick={() => {
                      setDiscardRequested(false);
                      setInternalOpen(false);
                      onOpenChange?.(false);
                    }}
                  >
                    Discard changes
                  </Button>
                </div>
              </div>
            )}
            {children}
          </div>
          {footer && <div className="ui-overlay-footer">{footer}</div>}
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
  open,
  onOpenChange,
  loading = false,
  closeOnConfirm = true,
  variant = 'destructive',
  onCloseAutoFocus,
  children,
}: {
  trigger?: ReactNode;
  title: string;
  description: string;
  onConfirm: () => void;
  confirmLabel?: string;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  loading?: boolean;
  closeOnConfirm?: boolean;
  variant?: 'primary' | 'destructive';
  onCloseAutoFocus?: ComponentProps<typeof A.Content>['onCloseAutoFocus'];
  children?: ReactNode;
}) {
  return (
    <A.Root open={open} onOpenChange={onOpenChange}>
      {trigger && <A.Trigger asChild>{trigger}</A.Trigger>}
      <A.Portal>
        <A.Overlay className={overlay} data-overlay-backdrop />
        <A.Content
          onCloseAutoFocus={onCloseAutoFocus}
          onEscapeKeyDown={(event) => {
            if (loading) event.preventDefault();
          }}
          className={cn(
            floating,
            'fixed top-1/2 left-1/2 max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto',
          )}
        >
          <A.Title className="text-lg font-semibold">{title}</A.Title>
          <A.Description className="mt-2 text-sm text-muted-foreground">
            {description}
          </A.Description>
          {children && <div className="mt-4">{children}</div>}
          <div className="mt-6 flex flex-wrap justify-end gap-2">
            <A.Cancel asChild>
              <Button disabled={loading} variant="outline">
                Cancel
              </Button>
            </A.Cancel>
            <A.Action asChild>
              <Button
                variant={variant}
                loading={loading}
                onClick={(event) => {
                  if (!closeOnConfirm) event.preventDefault();
                  onConfirm();
                }}
              >
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
