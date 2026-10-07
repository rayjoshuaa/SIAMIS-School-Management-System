import { useState } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { Table, TableHeader, TableRow, TableHead, TableCell } from '../../components/ui/table';
import { Badge } from '../../components/ui/feedback';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/controls';
import { Pagination } from '../../components/ui/navigation';
import { FilterBar, TableViewport, SystemState } from '../../components/shared/workspace';
import { useNotify } from '../../app/providers/notifications';
// Development specimens only: no operational records, API calls or domain actions.
const specimens = [
  {
    id: 'EX-01',
    name: 'Calendar specimen',
    category: 'Scheduling',
    status: 'Ready',
    intent: 'success' as const,
    items: 4,
  },
  {
    id: 'EX-02',
    name: 'Document specimen',
    category: 'Records',
    status: 'Review',
    intent: 'warning' as const,
    items: 2,
  },
  {
    id: 'EX-03',
    name: 'Workspace specimen',
    category: 'Administration',
    status: 'Draft',
    intent: 'neutral' as const,
    items: 1,
  },
  {
    id: 'EX-04',
    name: 'Queue specimen',
    category: 'Operations',
    status: 'Ready',
    intent: 'success' as const,
    items: 3,
  },
];
export function DemoTable() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [ascending, setAscending] = useState(true);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string[]>([]);
  const notify = useNotify();
  const filtered = specimens
    .filter(
      (row) =>
        row.name.toLowerCase().includes(search.toLowerCase()) &&
        (status === 'all' || row.status === status),
    )
    .sort((a, b) => (ascending ? a.name.localeCompare(b.name) : b.name.localeCompare(a.name)));
  const pages = Math.max(1, Math.ceil(filtered.length / 3));
  const rows = filtered.slice((page - 1) * 3, page * 3);
  return (
    <div>
      <FilterBar>
        <div className="min-w-0 flex-1">
          <label className="mb-2 block text-sm font-semibold" htmlFor="specimen-search">
            Search specimens
          </label>
          <Input
            id="specimen-search"
            type="search"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Search by title"
          />
        </div>
        <div>
          <label htmlFor="specimen-status" className="mb-2 block text-sm font-semibold">
            Status
          </label>
          <select
            id="specimen-status"
            className="ui-control min-h-11 rounded border border-input bg-surface px-3 text-base sm:text-sm"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="all">All statuses</option>
            <option>Ready</option>
            <option>Review</option>
            <option>Draft</option>
          </select>
        </div>
        <Button
          variant="ghost"
          disabled={!search && status === 'all'}
          onClick={() => {
            setSearch('');
            setStatus('all');
            setPage(1);
          }}
        >
          Clear filters
          {(search || status !== 'all') &&
            ` (${Number(Boolean(search)) + Number(status !== 'all')})`}
        </Button>
      </FilterBar>
      <p className="mb-3 text-sm text-muted-foreground" role="status">
        {filtered.length} specimens · {selected.length} selected · demonstration only
      </p>
      {rows.length ? (
        <TableViewport label="Component specimens, scroll horizontally for all columns">
          <Table className="min-w-[660px]">
            <caption className="sr-only">Fictional component specimens, not school records</caption>
            <TableHeader>
              <TableRow>
                <TableHead>
                  <span className="sr-only">Select</span>
                </TableHead>
                <TableHead aria-sort={ascending ? 'ascending' : 'descending'}>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setAscending(!ascending);
                      setPage(1);
                    }}
                    className="-ml-4"
                  >
                    Specimen{' '}
                    {ascending ? (
                      <ArrowUp aria-hidden="true" className="size-3" />
                    ) : (
                      <ArrowDown aria-hidden="true" className="size-3" />
                    )}
                  </Button>
                </TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="numeric">Items</TableHead>
                <TableHead>
                  <span className="sr-only">Actions</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <tbody>
              {rows.map((row) => (
                <TableRow key={row.id} aria-selected={selected.includes(row.id)}>
                  <TableCell>
                    <label className="flex min-h-11 min-w-11 items-center justify-center">
                      <input
                        type="checkbox"
                        aria-label={`Select ${row.name}`}
                        checked={selected.includes(row.id)}
                        onChange={(e) =>
                          setSelected(
                            e.target.checked
                              ? [...selected, row.id]
                              : selected.filter((id) => id !== row.id),
                          )
                        }
                        className="size-4 accent-primary"
                      />
                    </label>
                  </TableCell>
                  <TableCell>
                    <span className="block font-semibold">{row.name}</span>
                    <span className="text-xs text-muted-foreground">{row.id} · Example only</span>
                  </TableCell>
                  <TableCell>{row.category}</TableCell>
                  <TableCell>
                    <Badge intent={row.intent}>{row.status}</Badge>
                  </TableCell>
                  <TableCell className="numeric">{row.items}</TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      aria-label={`Inspect ${row.name}`}
                      onClick={() =>
                        notify('Component specimen only. No record opened or modified.')
                      }
                    >
                      Inspect
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </tbody>
          </Table>
        </TableViewport>
      ) : (
        <SystemState kind={search ? 'search' : 'filtered'} title="No matching specimens">
          Change your search or clear the filters. No school records are queried.
        </SystemState>
      )}
      <div className="pt-4">
        <Pagination page={page} pages={pages} onPage={setPage} />
      </div>
    </div>
  );
}
