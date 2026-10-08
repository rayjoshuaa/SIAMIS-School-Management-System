import { useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Dialog as D } from 'radix-ui';
import { Menu, X } from 'lucide-react';
import { Button } from '../ui/button';
import { Brand } from './brand';
import { ShellNavigation } from './shell-navigation';
import { useMediaQuery } from '../../hooks/use-media-query';
export function MobileNavigation() {
  const { pathname } = useLocation();
  const desktop = useMediaQuery('(min-width: 1024px)');
  return <NavigationDrawer key={`${pathname}-${desktop}`} />;
}
function NavigationDrawer() {
  const [open, setOpen] = useState(false);
  const navigated = useRef(false);
  return (
    <D.Root
      open={open}
      onOpenChange={(value) => {
        if (value) navigated.current = false;
        setOpen(value);
      }}
    >
      <D.Trigger asChild>
        <Button icon variant="ghost" aria-label="Open navigation" className="shell-navigation-open">
          <Menu aria-hidden="true" className="size-5" />
        </Button>
      </D.Trigger>
      <D.Portal>
        <D.Overlay className="ui-floating shell-drawer-overlay" />
        <D.Content
          className="ui-floating shell-drawer"
          onCloseAutoFocus={(event) => {
            if (navigated.current) {
              event.preventDefault();
              requestAnimationFrame(() => document.getElementById('main')?.focus());
            }
          }}
        >
          <div className="shell-drawer-header">
            <Brand />
            <D.Close asChild>
              <Button icon variant="ghost" aria-label="Close navigation">
                <X aria-hidden="true" className="size-5" />
              </Button>
            </D.Close>
          </div>
          <D.Title className="sr-only">SIAMIS navigation</D.Title>
          <D.Description className="sr-only">
            Switch modules or choose a destination. Escape closes navigation.
          </D.Description>
          <div className="shell-drawer-content">
            <ShellNavigation
              onNavigate={() => {
                navigated.current = true;
                setOpen(false);
              }}
            />
          </div>
        </D.Content>
      </D.Portal>
    </D.Root>
  );
}
