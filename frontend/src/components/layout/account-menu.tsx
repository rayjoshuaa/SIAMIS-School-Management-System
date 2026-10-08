import { useState } from 'react';
import { useAuth } from '../../lib/auth/auth-context';
import { DropdownMenu as M } from 'radix-ui';
import { ChevronDown } from 'lucide-react';
import { Avatar } from '../ui/feedback';
import { Button } from '../ui/button';
import { useNavigationSession } from '../../lib/auth/navigation-session';
export function AccountMenu() {
  const session = useNavigationSession();
  const { logout } = useAuth();
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  async function signOut() {
    if (busy) return;
    setBusy(true);
    setFailed(false);
    try {
      await logout();
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <M.Root>
      <M.Trigger asChild>
        <Button
          variant="ghost"
          aria-label={`Account menu: ${session.userName}`}
          className="shell-account-trigger"
        >
          <Avatar name={session.userName} />
          <span className="shell-account-name" title={session.userName}>
            {session.userName}
          </span>
          <ChevronDown aria-hidden="true" className="size-4 shrink-0" />
        </Button>
      </M.Trigger>
      <M.Portal>
        <M.Content align="end" sideOffset={8} className="ui-floating shell-account-menu">
          <M.Label className="shell-account-label">
            {session.userName}
            <span className="shell-account-roles">
              {session.roles?.join(' · ') || 'School account'}
            </span>
          </M.Label>
          <M.Separator className="my-2 h-px bg-border" />
          <M.Item
            disabled={busy}
            onSelect={(event) => {
              event.preventDefault();
              void signOut();
            }}
            className="ui-menu-item shell-account-action"
          >
            {busy ? 'Signing out…' : 'Sign out'}
          </M.Item>
          {busy && (
            <p role="status" className="shell-account-feedback">
              Ending your session
            </p>
          )}
          {failed && (
            <p role="alert" className="shell-account-feedback text-destructive">
              Unable to sign out. Please try again.
            </p>
          )}
        </M.Content>
      </M.Portal>
    </M.Root>
  );
}
