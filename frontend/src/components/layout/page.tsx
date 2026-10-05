import type { ComponentProps } from 'react';
import { cn } from '../../lib/utils/cn';
export function PageContainer({ className, ...props }: ComponentProps<'main'>) {
  return (
    <main
      id="main"
      className={cn('mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-10 lg:py-10', className)}
      {...props}
    />
  );
}
export function PageHeader(props: ComponentProps<'header'>) {
  return (
    <header
      className="mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"
      {...props}
    />
  );
}
export function PageTitle(props: ComponentProps<'h1'>) {
  return (
    <h1 className="text-2xl leading-tight font-semibold tracking-tight sm:text-3xl" {...props} />
  );
}
export function PageDescription(props: ComponentProps<'p'>) {
  return <p className="mt-2 max-w-2xl text-sm text-muted-foreground sm:text-base" {...props} />;
}
export function PageActions(props: ComponentProps<'div'>) {
  return <div className="flex flex-wrap gap-2" {...props} />;
}
export function Section({
  title,
  children,
  ...props
}: ComponentProps<'section'> & { title: string }) {
  return (
    <section className="mb-10 space-y-4" {...props}>
      <h2 className="text-lg font-semibold">{title}</h2>
      {children}
    </section>
  );
}
export function ResponsiveGrid(props: ComponentProps<'div'>) {
  return <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3" {...props} />;
}
