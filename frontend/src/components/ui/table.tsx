import type { ComponentProps } from 'react';
import { cn } from '../../lib/utils/cn';
export function Table({ className, ...props }: ComponentProps<'table'>) {
  return (
    <table
      className={cn('ui-table w-full border-collapse text-left text-sm', className)}
      {...props}
    />
  );
}
export function TableHeader(props: ComponentProps<'thead'>) {
  return <thead className="border-b border-border bg-muted text-muted-foreground" {...props} />;
}
export function TableRow(props: ComponentProps<'tr'>) {
  return <tr className="ui-table-row border-b border-border last:border-0" {...props} />;
}
export function TableHead(props: ComponentProps<'th'>) {
  return <th scope="col" className="font-medium" {...props} />;
}
export function TableCell(props: ComponentProps<'td'>) {
  return <td {...props} />;
}
