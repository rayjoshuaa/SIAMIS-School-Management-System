import { DropdownMenu as M } from 'radix-ui';
import { ChevronDown } from 'lucide-react';
import { Avatar } from '../ui/feedback';
import { Button } from '../ui/button';
import { useNavigationSession } from '../../lib/auth/navigation-session';
export function AccountMenu() {
  const session = useNavigationSession();
  return (
    <M.Root>
      <M.Trigger asChild>
        <Button
          variant="ghost"
          aria-label={`Account menu: ${session.userName}`}
          className="max-w-64 gap-2 px-2"
        >
          <Avatar name={session.userName} />
          <span className="hidden min-w-0 truncate text-left xl:block" title={session.userName}>
            {session.userName}
          </span>
          <ChevronDown aria-hidden="true" className="size-4 shrink-0" />
        </Button>
      </M.Trigger>
      <M.Portal>
        <M.Content
          align="end"
          sideOffset={8}
          className="z-50 w-72 max-w-[calc(100vw-2rem)] rounded-md border border-border bg-surface p-2 shadow-[var(--shadow-overlay)]"
        >
          <M.Label className="break-words px-3 py-2 text-sm font-semibold">
            {session.userName}
            <span className="mt-1 block text-xs font-normal text-muted-foreground">
              {session.mode === 'development'
                ? 'Preview only — not authenticated'
                : 'Account access is not connected'}
            </span>
          </M.Label>
          <M.Separator className="my-2 h-px bg-border" />
          {['Profile', 'Account settings', 'Sign out'].map((label) => (
            <M.Item
              key={label}
              aria-label={label}
              disabled
              className="flex min-h-11 items-center justify-between px-3 text-sm text-muted-foreground"
            >
              {label}
              <span aria-hidden="true" className="text-xs">
                Unavailable
              </span>
            </M.Item>
          ))}
        </M.Content>
      </M.Portal>
    </M.Root>
  );
}
