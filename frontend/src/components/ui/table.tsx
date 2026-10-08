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
export function TableHeader({ className, ...props }: ComponentProps<'thead'>) {
  return (
    <thead
      className={cn('border-b border-border bg-muted text-muted-foreground', className)}
      {...props}
    />
  );
}
export function TableRow({ className, ...props }: ComponentProps<'tr'>) {
  return (
    <tr className={cn('ui-table-row border-b border-border last:border-0', className)} {...props} />
  );
}
export function TableHead({ className, ...props }: ComponentProps<'th'>) {
  return <th scope="col" className={cn('font-medium', className)} {...props} />;
}
export function TableCell(props: ComponentProps<'td'>) {
  return <td {...props} />;
}
