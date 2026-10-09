import type { ReactNode } from 'react';
import { cn } from '../../lib/utils/cn';

/** Read-only record facts. Form labels, table headings and metrics have separate contracts. */
export function DetailFacts({
  items,
  className,
}: {
  items: [string, ReactNode][];
  className?: string;
}) {
  return (
    <dl className={cn('ui-detail-facts', className)}>
      {items.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value ?? <span className="text-muted-foreground">Not recorded</span>}</dd>
        </div>
      ))}
    </dl>
  );
}
