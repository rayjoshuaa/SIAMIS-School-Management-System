import type { ReactNode } from 'react';
import { PageActions } from './page';
import { Button } from '../ui/button';
import { DropdownMenu } from '../ui/overlays';
export function PageActionGroup({
  primary,
  secondary = [],
}: {
  primary?: ReactNode;
  secondary?: { label: string; onSelect: () => void }[];
}) {
  return (
    <PageActions>
      {primary}
      {secondary.length > 0 && (
        <>
          <div className="hidden gap-2 sm:flex">
            {secondary.map((action) => (
              <Button key={action.label} variant="outline" onClick={action.onSelect}>
                {action.label}
              </Button>
            ))}
          </div>
          <div className="sm:hidden">
            <DropdownMenu label="More page actions" items={secondary} />
          </div>
        </>
      )}
    </PageActions>
  );
}
