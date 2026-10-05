import { useRef, useState } from 'react';
import { Dialog as D } from 'radix-ui';
import { Menu, X } from 'lucide-react';
import { Button } from '../ui/button';
import { Brand } from './brand';
import { ShellNavigation } from './shell-navigation';
export function MobileNavigation() {
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
        <Button icon variant="ghost" aria-label="Open navigation" className="md:hidden">
          <Menu className="size-5" />
        </Button>
      </D.Trigger>
      <D.Portal>
        <D.Overlay className="fixed inset-0 z-40 bg-[var(--overlay)]" />
        <D.Content
          onCloseAutoFocus={(event) => {
            if (navigated.current) {
              event.preventDefault();
              requestAnimationFrame(() => document.getElementById('main')?.focus());
            }
          }}
          className="fixed inset-y-0 left-0 z-50 flex w-[min(90vw,20rem)] flex-col border-r border-border bg-sidebar shadow-[var(--shadow-overlay)]"
        >
          <div className="flex items-center justify-between border-b border-border p-4">
            <Brand />
            <D.Close asChild>
              <Button icon variant="ghost" aria-label="Close navigation">
                <X className="size-5" />
              </Button>
            </D.Close>
          </div>
          <D.Title className="sr-only">SIAMIS navigation</D.Title>
          <D.Description className="sr-only">
            Choose a workspace. Escape closes navigation.
          </D.Description>
          <div className="flex-1 overflow-y-auto p-4">
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
