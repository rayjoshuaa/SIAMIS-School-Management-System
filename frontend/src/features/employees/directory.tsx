import { useRef, useState } from 'react';
import { Plus, Eye } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../../lib/auth/auth-context';
import { api } from '../../lib/api/client';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/feedback';
import { Input } from '../../components/ui/controls';
import { FormField } from '../../components/shared/form-field';
import { Table, TableHeader, TableRow, TableHead, TableCell } from '../../components/ui/table';
import { TableViewport, SystemState } from '../../components/shared/workspace';
import { Pagination } from '../../components/ui/navigation';
import { Sheet } from '../../components/ui/overlays';
import { employeeName, type EmployeePage } from './contracts';
import { useMasters, useEmployee } from './data';
import { ContactSummary, EmploymentSummary, QueryState } from './presentation';
import './employees.css';

function QuickView({ id }: { id: string }) {
  const query = useEmployee(id);
  if (!query.data || query.isError)
    return (
      <QueryState
        loading={query.isPending}
        error={query.error}
        retry={() => void query.refetch()}
      />
    );
  return (
    <div className="employee-workspace">
      <div>
        <h2 className="text-lg font-semibold">{employeeName(query.data)}</h2>
        <p className="text-sm text-muted-foreground">{query.data.employeeNumber}</p>
      </div>
      <EmploymentSummary employee={query.data} />
      <section className="employee-section">
        <h2>Contact information</h2>
        <ContactSummary employee={query.data} />
      </section>
      <Link
        className="ui-link inline-flex min-h-11 items-center font-semibold"
        to={`/hr/employees/${id}`}
      >
        Open Employee 360 →
      </Link>
    </div>
  );
}
export function EmployeeDirectory() {
  const { state } = useAuth();
  const capabilities = state.user?.capabilities ?? [];
  const [params, setParams] = useSearchParams();
  const rawPage = Number(params.get('page') ?? 1);
  const page = Number.isSafeInteger(rawPage) && rawPage > 0 && rawPage <= 107374182 ? rawPage : 1;
  const [search, setSearch] = useState(params.get('search') ?? '');
  const [selected, setSelected] = useState<string | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const masters = useMasters();
  const queryParams = new URLSearchParams({ page: String(page), pageSize: '20' });
  for (const key of ['search', 'departmentId', 'designationId', 'employmentStatusId', 'isActive']) {
    const value = params.get(key);
    if (value) queryParams.set(key, value);
  }
  const query = useQuery({
    queryKey: ['employees', 'directory', queryParams.toString()],
    enabled: capabilities.includes('Employee.Read'),
    queryFn: ({ signal }) => api<EmployeePage>(`/api/employees?${queryParams}`, { signal }),
  });
  function change(key: string, value: string) {
    setParams((previous) => {
      const next = new URLSearchParams(previous);
      next.delete('page');
      next.delete('notice');
      if (value) next.set(key, value);
      else next.delete(key);
      return next;
    });
  }
  if (!capabilities.includes('Employee.Read'))
    return (
      <SystemState kind="permission" title="Employee access required">
        You do not have access to the employee directory.
      </SystemState>
    );
  return (
    <div className="employee-workspace employee-directory">
      <div className="employee-toolbar">
        <p className="employee-context">Search records and review employment context.</p>
        {capabilities.includes('Employee.Manage') && (
          <Link
            className="ui-button ui-button-primary employee-action"
            data-variant="primary"
            data-density="compact"
            to="/hr/employees/new"
          >
            <Plus aria-hidden="true" className="size-4" />
            Create employee
          </Link>
        )}
      </div>
      {params.get('notice') === 'created' && (
        <p role="status" className="text-sm">
          Employee created successfully.
        </p>
      )}
      <form
        className="employee-filters"
        aria-label="Employee directory filters"
        onSubmit={(event) => {
          event.preventDefault();
          change('search', search.trim());
        }}
      >
        <div className="employee-search-row">
          <FormField id="employee-search" label="Search employees">
            {(props) => (
              <Input
                {...props}
                value={search}
                maxLength={100}
                placeholder="Name or employee number"
                onChange={(event) => setSearch(event.target.value)}
              />
            )}
          </FormField>
          <Button type="submit" variant="outline">
            Search
          </Button>
        </div>
        {(
          [
            ['departmentId', 'departments', 'Department'],
            ['designationId', 'designations', 'Designation'],
            ['employmentStatusId', 'employment-statuses', 'Employment status'],
          ] as const
        ).map(([key, name, label]) => (
          <FormField key={key} id={`filter-${key}`} label={label}>
            {(props) => (
              <select
                {...props}
                className="ui-control employee-native-select"
                value={params.get(key) ?? ''}
                disabled={!masters.data}
                onChange={(event) => change(key, event.target.value)}
              >
                <option value="">All {label.toLowerCase()} values</option>
                {masters.data?.[name].map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                    {item.isActive ? '' : ' (inactive)'}
                  </option>
                ))}
              </select>
            )}
          </FormField>
        ))}
        <FormField id="filter-active" label="Employee record">
          {(props) => (
            <select
              {...props}
              className="ui-control employee-native-select"
              value={params.get('isActive') ?? ''}
              onChange={(event) => change('isActive', event.target.value)}
            >
              <option value="">Active and inactive</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          )}
        </FormField>
      </form>
      {masters.isError && (
        <QueryState loading={false} error={masters.error} retry={() => void masters.refetch()} />
      )}
      {query.isPending || query.isError ? (
        <QueryState
          loading={query.isPending}
          error={query.error}
          retry={() => void query.refetch()}
        />
      ) : (
        query.data && (
          <>
            <p className="text-sm text-muted-foreground" role="status">
              {query.data.totalCount} employee records{query.isFetching ? ' · Refreshing…' : ''}
            </p>
            {!query.data.items.length ? (
              <SystemState
                kind={queryParams.size > 2 ? 'filtered' : 'empty'}
                title="No employees found"
                action={
                  <Button
                    variant="outline"
                    onClick={() => {
                      setSearch('');
                      setParams({});
                    }}
                  >
                    Clear filters
                  </Button>
                }
              >
                No employee records match this view.
              </SystemState>
            ) : (
              <TableViewport label="Employee directory">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {[
                        'Employee',
                        'Department / designation',
                        'Employment status',
                        'Employee record',
                        'Actions',
                      ].map((label) => (
                        <TableHead key={label}>{label}</TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <tbody>
                    {query.data.items.map((employee) => (
                      <TableRow key={employee.employeeId}>
                        <TableCell>
                          <Link
                            className="ui-link font-semibold"
                            to={`/hr/employees/${employee.employeeId}`}
                          >
                            {employeeName(employee)}
                          </Link>
                          <div className="mt-1 text-sm text-muted-foreground">
                            {employee.employeeNumber}
                          </div>
                        </TableCell>
                        <TableCell>
                          {employee.department ?? 'Not recorded'}
                          <div className="text-sm text-muted-foreground">
                            {employee.designation ?? 'Not recorded'}
                          </div>
                        </TableCell>
                        <TableCell>
                          {employee.employmentStatus ?? 'No current employment'}
                        </TableCell>
                        <TableCell>
                          <Badge>{employee.isActive ? 'Active' : 'Inactive'}</Badge>
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            density="compact"
                            aria-label={`Quick view ${employee.employeeNumber}`}
                            onClick={(event) => {
                              trigger.current = event.currentTarget;
                              setSelected(employee.employeeId);
                            }}
                          >
                            <Eye aria-hidden="true" className="size-4" />
                            Quick view
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </tbody>
                </Table>
              </TableViewport>
            )}
            <Pagination
              page={page}
              pages={Math.max(1, Math.ceil(query.data.totalCount / 20))}
              onPage={(value) =>
                setParams((previous) => {
                  const next = new URLSearchParams(previous);
                  next.set('page', String(value));
                  return next;
                })
              }
            />
          </>
        )
      )}
      <Sheet
        open={!!selected}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title="Employee quick view"
        size="md"
        description="Employee record and current employment information."
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          trigger.current?.focus({ preventScroll: true });
        }}
      >
        {selected && <QuickView id={selected} />}
      </Sheet>
    </div>
  );
}
