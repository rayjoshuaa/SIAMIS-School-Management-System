import { useState, type ReactNode } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { Tooltip } from 'radix-ui';
import { createQueryClient } from './query-client';
import { Notifications } from './notifications';
export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <Tooltip.Provider delayDuration={250}>
        <Notifications>{children}</Notifications>
      </Tooltip.Provider>
    </QueryClientProvider>
  );
}
