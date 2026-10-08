import { useRef, useState } from 'react';
import { ArrowRight, Plus } from 'lucide-react';
import {
  ContentFrame,
  FilterBar,
  MetricStrip,
  SystemState,
  TableViewport,
  WorkspaceHeader,
} from '../../components/shared/workspace';
import { FormField } from '../../components/shared/form-field';
import { Button } from '../../components/ui/button';
import { Input, Select } from '../../components/ui/controls';
import { Alert, Badge, Skeleton } from '../../components/ui/feedback';
import { Dialog, Sheet } from '../../components/ui/overlays';
import { Pagination } from '../../components/ui/navigation';
import { Table, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';

// Isolated Development showcase only. No API client, persistence or domain commands.
const records = [
  {
    id: 'DEMO-001',
    name: 'Academic calendar review',
    area: 'School Management',
    status: 'Ready',
    owner: 'Academic Office',
  },
  {
    id: 'DEMO-002',
    name: 'New staff access request',
    area: 'Administration',
    status: 'Needs review',
    owner: 'School Office',
  },
  {
    id: 'DEMO-003',
    name: 'Campus document register',
    area: 'School Management',
    status: 'Draft',
    owner: 'Records Office',
  },
];

export function PremiumWorkspace() {
  const createTrigger = useRef<HTMLButtonElement | null>(null);
  const inspectionTrigger = useRef<HTMLButtonElement | null>(null);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [state, setState] = useState('ready');
  const [createOpen, setCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [area, setArea] = useState('administration');
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  const [pending, setPending] = useState(false);
  const [selected, setSelected] = useState<(typeof records)[number] | null>(null);
  const visible = records.filter(
    (record) =>
      record.name.toLowerCase().includes(search.toLowerCase()) &&
      (status === 'all' || record.status === status),
  );
  const changeCreate = (open: boolean) => {
    setCreateOpen(open);
    if (!open) {
      setName('');
      setArea('administration');
      setError('');
      setPending(false);
    }
  };
  return (
    <ContentFrame className="v31-workspace" density="compact">
      <WorkspaceHeader
        context="Administration · Demonstration"
        title="Administration workspace"
        description="School requests and administrative review."
        actions={
          <>
            <Button
              variant="outline"
              onClick={() => {
                setSearch('');
                setStatus('all');
                setState('ready');
                setSaved(false);
              }}
            >
              Reset view
            </Button>
            <Button
              ref={createTrigger}
              onClick={() => {
                setSaved(false);
                setCreateOpen(true);
              }}
            >
              <Plus aria-hidden="true" />
              Create record
            </Button>
          </>
        }
      />
      <p className="v31-demo-note">
        Synthetic demonstration data only · no API or database writes.
      </p>
      <MetricStrip
        density="compact"
        items={[
          { label: 'Open requests', value: '3', context: 'Demonstration records' },
          { label: 'Needs review', value: '1', context: 'Awaiting a decision' },
          { label: 'Ready', value: '1', context: 'Prepared for the next step' },
          { label: 'Drafts', value: '1', context: 'Not submitted' },
        ]}
      />
      <section className="v31-records" aria-label="Demonstration administrative records">
        <div className="v31-section-title">
          <div>
            <h2>Administrative requests</h2>
            <p>Find a record or inspect its context.</p>
          </div>
          <span>{visible.length} demonstration records</span>
        </div>
        <FilterBar className="v31-toolbar">
          <FormField id="v31-search" label="Search requests">
            {(props) => (
              <Input
                {...props}
                type="search"
                placeholder="Search by request name"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            )}
          </FormField>
          <FormField id="v31-status" label="Request status">
            {(props) => (
              <Select
                {...props}
                label="Request status"
                value={status}
                onValueChange={setStatus}
                options={[
                  { value: 'all', label: 'All statuses' },
                  ...['Ready', 'Needs review', 'Draft'].map((value) => ({ value, label: value })),
                ]}
              />
            )}
          </FormField>
          <FormField id="v31-state" label="Demonstration state">
            {(props) => (
              <Select
                {...props}
                label="Demonstration state"
                value={state}
                onValueChange={setState}
                options={['ready', 'loading', 'empty', 'error'].map((value) => ({
                  value,
                  label: value[0].toUpperCase() + value.slice(1),
                }))}
              />
            )}
          </FormField>
        </FilterBar>
        {saved && (
          <Alert intent="success" title="Demonstration completed">
            The form was validated locally. No record was written to a database.
          </Alert>
        )}
        {state === 'loading' ? (
          <div className="v31-loading" role="status" aria-label="Loading demonstration requests">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : state === 'error' ? (
          <SystemState
            kind="error"
            title="Requests could not be loaded"
            action={
              <Button variant="outline" onClick={() => setState('ready')}>
                Retry demonstration
              </Button>
            }
          >
            This is a simulated service error. Your search and filters are retained.
          </SystemState>
        ) : state === 'empty' || !visible.length ? (
          <SystemState
            kind={search ? 'search' : 'empty'}
            title="No requests to show"
            action={
              <Button
                variant="outline"
                onClick={() => {
                  setSearch('');
                  setStatus('all');
                  setState('ready');
                }}
              >
                Reset demonstration filters
              </Button>
            }
          >
            No matching demonstration records. No operational data is involved.
          </SystemState>
        ) : (
          <>
            <TableViewport label="Administrative demonstration records, scroll for all columns">
              <Table className="v31-table">
                <caption className="sr-only">
                  Synthetic administrative requests for design review
                </caption>
                <TableHeader>
                  <TableRow>
                    <TableHead>Request</TableHead>
                    <TableHead>Module</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Responsible team</TableHead>
                    <TableHead>
                      <span className="sr-only">Actions</span>
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <tbody>
                  {visible.map((record) => (
                    <TableRow key={record.id}>
                      <TableCell>
                        <strong>{record.name}</strong>
                        <span className="v31-record-id">{record.id} · demonstration</span>
                      </TableCell>
                      <TableCell>{record.area}</TableCell>
                      <TableCell>
                        <Badge
                          intent={
                            record.status === 'Ready'
                              ? 'success'
                              : record.status === 'Needs review'
                                ? 'warning'
                                : 'neutral'
                          }
                        >
                          {record.status}
                        </Badge>
                      </TableCell>
                      <TableCell>{record.owner}</TableCell>
                      <TableCell>
                        <Button
                          density="compact"
                          variant="ghost"
                          aria-label={`Inspect ${record.name}`}
                          onClick={(event) => {
                            inspectionTrigger.current = event.currentTarget;
                            setSelected(record);
                          }}
                        >
                          Inspect
                          <ArrowRight aria-hidden="true" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </tbody>
              </Table>
            </TableViewport>
            <div className="v31-pagination">
              <Pagination page={1} pages={1} onPage={() => undefined} />
            </div>
          </>
        )}
      </section>
      <Dialog
        open={createOpen}
        onOpenChange={changeCreate}
        title="Create demonstration record"
        description="A focused form using shared SIAMIS controls. This demonstration never calls the API."
        size="md"
        dirty={Boolean(name) || area !== 'administration'}
        pending={pending}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          document.getElementById('v31-name')?.focus();
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          createTrigger.current?.focus();
        }}
        footer={
          pending ? (
            <Button variant="outline" onClick={() => setPending(false)}>
              Finish loading demonstration
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => setPending(true)}>
                Preview pending state
              </Button>
              <Button type="submit" form="v31-create-form">
                Save demonstration
              </Button>
            </>
          )
        }
      >
        <form
          id="v31-create-form"
          className="v31-form"
          onSubmit={(event) => {
            event.preventDefault();
            if (pending) return;
            if (!name.trim()) {
              setError('Enter a record name.');
              document.getElementById('v31-name')?.focus();
              return;
            }
            setSaved(true);
            changeCreate(false);
          }}
        >
          <FormField id="v31-name" label="Record name" required error={error}>
            {(props) => (
              <Input
                {...props}
                disabled={pending}
                value={name}
                onChange={(event) => {
                  setName(event.target.value);
                  setError('');
                }}
                autoComplete="off"
              />
            )}
          </FormField>
          <FormField
            id="v31-area"
            label="Module"
            required
            hint="The selection affects this demonstration only."
          >
            {(props) => (
              <Select
                {...props}
                disabled={pending}
                label="Module"
                value={area}
                onValueChange={setArea}
                options={[
                  { value: 'administration', label: 'Administration' },
                  { value: 'school', label: 'School Management' },
                  { value: 'hr', label: 'Human Resources' },
                ]}
              />
            )}
          </FormField>
          <div className="v31-context-note">
            No account, employee or operational record will be created.
          </div>
        </form>
      </Dialog>
      <Sheet
        open={Boolean(selected)}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title="Request inspection"
        description="Contextual detail without leaving the workspace. Synthetic demonstration only."
        size="md"
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          inspectionTrigger.current?.focus();
        }}
        footer={
          <Button variant="outline" onClick={() => setSelected(null)}>
            Close inspection
          </Button>
        }
      >
        {selected && (
          <div className="v31-inspection">
            <Badge intent="info">Demonstration record</Badge>
            <h3>{selected.name}</h3>
            <dl>
              <dt>Reference</dt>
              <dd>{selected.id}</dd>
              <dt>Module</dt>
              <dd>{selected.area}</dd>
              <dt>Status</dt>
              <dd>{selected.status}</dd>
              <dt>Responsible team</dt>
              <dd>{selected.owner}</dd>
            </dl>
            <h3>Review context</h3>
            <p>
              A short contextual summary belongs here. Historical and confidential details follow
              the relevant backend authorization in production.
            </p>
            <h3>Demonstration history</h3>
            <ol>
              <li>Prepared for administrative review</li>
              <li>Added to the demonstration workspace</li>
            </ol>
          </div>
        )}
      </Sheet>
    </ContentFrame>
  );
}
