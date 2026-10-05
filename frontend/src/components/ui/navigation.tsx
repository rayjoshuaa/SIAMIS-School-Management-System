import type { ReactNode } from 'react';
import { Tabs as T } from 'radix-ui';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './button';
export function Tabs({ tabs }: { tabs: { value: string; label: string; content: ReactNode }[] }) {
  return (
    <T.Root defaultValue={tabs[0]?.value}>
      <T.List
        aria-label="Example views"
        className="mb-4 flex flex-wrap gap-1 border-b border-border"
      >
        {tabs.map((tab) => (
          <T.Trigger
            key={tab.value}
            value={tab.value}
            className="min-h-11 border-b-2 border-transparent px-4 text-sm text-muted-foreground data-[state=active]:border-primary data-[state=active]:text-primary"
          >
            {tab.label}
          </T.Trigger>
        ))}
      </T.List>
      {tabs.map((tab) => (
        <T.Content key={tab.value} value={tab.value}>
          {tab.content}
        </T.Content>
      ))}
    </T.Root>
  );
}
export function Breadcrumb({ items }: { items: { label: string; href?: string }[] }) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
        {items.map((item, i) => (
          <li key={item.label} className="flex items-center gap-2">
            {i > 0 && <ChevronRight aria-hidden="true" className="size-3" />}
            {item.href ? (
              <a
                href={item.href}
                className="inline-flex min-h-11 items-center underline-offset-4 hover:underline"
              >
                {item.label}
              </a>
            ) : (
              <span aria-current={i === items.length - 1 ? 'page' : undefined}>{item.label}</span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
export function Pagination({
  page,
  pages,
  onPage,
}: {
  page: number;
  pages: number;
  onPage: (page: number) => void;
}) {
  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center justify-between gap-3">
      <p className="text-sm text-muted-foreground" aria-live="polite">
        Page {page} of {pages}
      </p>
      <div className="flex gap-2">
        <Button variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          <ChevronLeft aria-hidden="true" className="size-4" />
          Previous
        </Button>
        <Button variant="outline" disabled={page >= pages} onClick={() => onPage(page + 1)}>
          Next
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
    </nav>
  );
}
