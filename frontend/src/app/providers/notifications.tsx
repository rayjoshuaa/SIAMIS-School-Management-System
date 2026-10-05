import { createContext, useContext, useState, type ReactNode } from 'react';
import { Toast as T } from 'radix-ui';
import { X } from 'lucide-react';
import { Button } from '../../components/ui/button';
const NotificationContext = createContext<(message: string) => void>(() => {
  throw new Error('Notification provider is missing.');
});
export function Notifications({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('');
  const [open, setOpen] = useState(false);
  return (
    <NotificationContext.Provider
      value={(text) => {
        setMessage(text);
        setOpen(true);
      }}
    >
      <T.Provider duration={5000} swipeDirection="right">
        {children}
        <T.Root
          open={open}
          onOpenChange={setOpen}
          className="flex items-center justify-between gap-4 rounded-md border border-border bg-surface p-4 shadow-[var(--shadow-overlay)]"
        >
          <T.Title className="text-sm font-medium">{message}</T.Title>
          <T.Close asChild>
            <Button icon variant="ghost" aria-label="Dismiss notification">
              <X className="size-4" />
            </Button>
          </T.Close>
        </T.Root>
        <T.Viewport className="fixed right-4 bottom-4 z-50 m-0 w-[calc(100%-2rem)] max-w-sm list-none p-0" />
      </T.Provider>
    </NotificationContext.Provider>
  );
}
// eslint-disable-next-line react-refresh/only-export-components
export const useNotify = () => useContext(NotificationContext);
