import { useState } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { Table, TableHeader, TableRow, TableHead, TableCell } from '../../components/ui/table';
import { StatusBadge } from '../../components/shared/status-badge';
import { Button } from '../../components/ui/button';
import { DropdownMenu } from '../../components/ui/overlays';
import { Pagination } from '../../components/ui/navigation';
import { useNotify } from '../../app/providers/notifications';
const sample = [
  {
    name: 'Calendar review',
    area: 'Foundation example',
    intent: 'success' as const,
    status: 'Ready',
  },
  {
    name: 'Document template',
    area: 'Foundation example',
    intent: 'warning' as const,
    status: 'Needs review',
  },
  { name: 'Workspace guide', area: 'Foundation example', intent: 'info' as const, status: 'Draft' },
];
export function DemoTable() {
  const [ascending, setAscending] = useState(true);
  const [page, setPage] = useState(1);
  const notify = useNotify();
  const rows = [...sample]
    .sort((a, b) => (ascending ? a.name.localeCompare(b.name) : b.name.localeCompare(a.name)))
    .slice((page - 1) * 2, page * 2);
  const action = (name: string) => (
    <DropdownMenu
      label={`Actions for ${name}`}
      items={[
        {
          label: 'View example',
          onSelect: () => notify('Static showcase item. No domain workflow is implemented.'),
        },
      ]}
    />
  );
  return (
    <div className="space-y-4">
      <div className="hidden overflow-hidden rounded-md border border-border md:block">
        <Table>
          <caption className="sr-only">Static design-system examples, not HR data</caption>
          <TableHeader>
            <TableRow>
              <TableHead aria-sort={ascending ? 'ascending' : 'descending'}>
                <Button variant="ghost" onClick={() => setAscending(!ascending)} className="-ml-3">
                  Example{' '}
                  {ascending ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />}
                </Button>
              </TableHead>
              <TableHead>Category</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>
                <span className="sr-only">Actions</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <tbody>
            {rows.map((row) => (
              <TableRow key={row.name}>
                <TableCell>
                  <span className="font-medium">{row.name}</span>
                </TableCell>
                <TableCell>{row.area}</TableCell>
                <TableCell>
                  <StatusBadge intent={row.intent}>{row.status}</StatusBadge>
                </TableCell>
                <TableCell>{action(row.name)}</TableCell>
              </TableRow>
            ))}
          </tbody>
        </Table>
      </div>
      <ul aria-label="Static design-system examples" className="space-y-3 md:hidden">
        {rows.map((row) => (
          <li key={row.name} className="rounded-md border border-border p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="font-medium">{row.name}</h3>
                <p className="text-xs text-muted-foreground">{row.area}</p>
              </div>
              {action(row.name)}
            </div>
            <div className="mt-3">
              <StatusBadge intent={row.intent}>{row.status}</StatusBadge>
            </div>
          </li>
        ))}
      </ul>
      <Pagination page={page} pages={2} onPage={setPage} />
    </div>
  );
}
