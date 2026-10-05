import type { ComponentProps } from 'react';
import { cn } from '../../lib/utils/cn';
export function Table({ className, ...props }: ComponentProps<'table'>) {
  return <table className={cn('w-full border-collapse text-left text-sm', className)} {...props} />;
}
export function TableHeader(props: ComponentProps<'thead'>) {
  return <thead className="border-b border-border bg-muted text-muted-foreground" {...props} />;
}
export function TableRow(props: ComponentProps<'tr'>) {
  return (
    <tr
      className="border-b border-border last:border-0 hover:bg-muted/60 focus-within:bg-muted/60"
      {...props}
    />
  );
}
export function TableHead(props: ComponentProps<'th'>) {
  return <th scope="col" className="px-4 py-3 font-medium" {...props} />;
}
export function TableCell(props: ComponentProps<'td'>) {
  return <td className="px-4 py-3" {...props} />;
}
