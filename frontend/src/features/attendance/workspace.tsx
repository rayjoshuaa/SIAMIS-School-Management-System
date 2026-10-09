import { RefreshCw, Search } from 'lucide-react';
import { useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { useAuth } from '../../lib/auth/auth-context';
import { Button, LinkButton } from '../../components/ui/button';
import { Input, FilterSelect } from '../../components/ui/controls';
import { Tabs, Pagination } from '../../components/ui/navigation';
import { Sheet } from '../../components/ui/overlays';
import { FormField } from '../../components/shared/form-field';
import { FilterToolbar, MetricStrip } from '../../components/shared/workspace';
import { Facts } from '../employees/presentation';
import type { EmployeePage, Master } from '../employees/contracts';
import type { History, Legacy, Overview, Row, Summary } from './contracts';
import { CoverageFacts, Findings, Rows, QueryState } from './presentation';
import { attendanceLabel, duration, payrollBoundary } from './format';
import '../employees/employees.css';
import './attendance.css';

export function AttendanceWorkspace() {
  const { state } = useAuth();
  const reporting = state.user?.capabilities.includes('Reporting.Read');
  return (
    <div className="attendance-workspace">
      <p className="attendance-boundary">{payrollBoundary}</p>
      <Tabs
        label="Attendance views"
        tabs={[
          ...(reporting
            ? [{ value: 'daily', label: 'Daily overview', content: <ReportPanel mode="daily" /> }]
            : []),
          { value: 'queue', label: 'Review queue', content: <ReportPanel mode="queue" /> },
          {
            value: 'employee',
            label: 'Employee history & evidence',
            content: <EmployeeHistory reporting={!!reporting} />,
          },
        ]}
      />
    </div>
  );
}
function SelectField({
  id,
  label,
  value,
  onChange,
  options,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <FormField id={id} label={label}>
      {(props) =>
        ['attendance-Department', 'attendance-Designation', 'attendance-employee'].includes(id) ? (
          <FilterSelect
            {...props}
            label={label}
            value={value}
            onChange={onChange}
            options={options}
          />
        ) : (
          <select
            {...props}
            className="ui-control min-h-11 w-full px-3 py-2"
            value={value}
            onChange={(e) => onChange(e.target.value)}
          >
            {options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        )
      }
    </FormField>
  );
}
function ReportPanel({ mode }: { mode: 'daily' | 'queue' }) {
  const [params, setParams] = useSearchParams();
  const date = params.get('date') ?? '';
  const from = params.get('from') ?? '';
  const to = params.get('to') ?? '';
  const rawPage = Number(params.get('page') ?? 1);
  const page = Number.isSafeInteger(rawPage) && rawPage > 0 && rawPage <= 1000000 ? rawPage : 1;
  const [selected, setSelected] = useState<Row | null>(null);
  const invoker = useRef<HTMLButtonElement | null>(null);
  const masters = useQuery({
    queryKey: ['attendance', 'filter-masters'],
    queryFn: async ({ signal }) =>
      Promise.all(
        ['departments', 'designations'].map((name) =>
          api<Master[]>(`/api/master-data/${name}?includeInactive=true`, { signal }),
        ),
      ),
  });
  const filters = new URLSearchParams();
  for (const name of [
    'departmentId',
    'designationId',
    'scheduled',
    'late',
    'hasApprovedLeave',
    'confirmedAbsent',
    'requiresReview',
    'isStale',
    'unfinalized',
    'recordState',
  ]) {
    if (params.get(name)) filters.set(name, params.get(name)!);
  }
  filters.set('page', String(page));
  filters.set('pageSize', '20');
  if (mode === 'queue') {
    filters.set('from', from);
    filters.set('to', to);
  }
  const path =
    mode === 'queue'
      ? '/api/attendance/review-queue'
      : date
        ? `/api/attendance/days/${date}`
        : '/api/attendance/today';
  const ready = mode === 'daily' || !!(from && to);
  const report = useQuery({
    queryKey: ['attendance', mode, path, filters.toString()],
    enabled: ready,
    queryFn: ({ signal }) => api<Overview>(`${path}?${filters}`, { signal }),
  });
  function change(name: string, value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set(name, value);
    else next.delete(name);
    if (name !== 'page') next.delete('page');
    setParams(next, { replace: true });
  }
  return (
    <div className="attendance-workspace">
      <FilterToolbar className="attendance-filter-bar" role="group" aria-label="Attendance filters">
        {mode === 'daily' ? (
          <FormField
            id="attendance-date"
            label="Business date"
            hint="Leave empty for the server's Bangkok today."
          >
            {(props) => (
              <Input
                {...props}
                type="date"
                value={date}
                onChange={(e) => change('date', e.target.value)}
              />
            )}
          </FormField>
        ) : (
          <>
            <FormField id="attendance-from" label="From" required>
              {(props) => (
                <Input
                  {...props}
                  type="date"
                  value={from}
                  onChange={(e) => change('from', e.target.value)}
                />
              )}
            </FormField>
            <FormField
              id="attendance-to"
              label="To"
              required
              hint="Maximum 31 days; source budgets may require a narrower range."
            >
              {(props) => (
                <Input
                  {...props}
                  type="date"
                  value={to}
                  onChange={(e) => change('to', e.target.value)}
                />
              )}
            </FormField>
          </>
        )}
        {['Department', 'Designation'].map((label, index) => (
          <SelectField
            key={label}
            id={`attendance-${label}`}
            label={label}
            value={params.get(`${label.toLowerCase()}Id`) ?? ''}
            onChange={(value) => change(`${label.toLowerCase()}Id`, value)}
            options={[
              { value: '', label: 'All values' },
              ...(masters.data?.[index] ?? []).map((value) => ({
                value: value.id,
                label: value.name,
              })),
            ]}
          />
        ))}
        <div className="ui-filter-action">
          <Button
            variant="secondary"
            loading={report.isFetching}
            disabled={!ready || report.isFetching}
            onClick={() => void report.refetch()}
          >
            <RefreshCw aria-hidden="true" /> Refresh attendance
          </Button>
        </div>
      </FilterToolbar>
      {masters.isError && (
        <QueryState loading={false} error={masters.error} retry={() => void masters.refetch()} />
      )}
      <details>
        <summary>Factual status filters</summary>
        <div className="attendance-filters">
          {[
            ['scheduled', 'Scheduled'],
            ['late', 'Late under policy'],
            ['hasApprovedLeave', 'Approved leave'],
            ['confirmedAbsent', 'Confirmed absence'],
            ['requiresReview', 'Requires review'],
            ['isStale', 'Stale sources'],
            ['unfinalized', 'Unfinalized'],
          ].map(([name, label]) => (
            <SelectField
              key={name}
              id={`attendance-${name}`}
              label={label}
              value={params.get(name) ?? ''}
              onChange={(value) => change(name, value)}
              options={[
                { value: '', label: 'Any' },
                { value: 'true', label: 'Yes' },
                { value: 'false', label: 'No' },
              ]}
            />
          ))}
          <SelectField
            id="attendance-record-state"
            label="Record state"
            value={params.get('recordState') ?? ''}
            onChange={(value) => change('recordState', value)}
            options={[
              '',
              'Live',
              'UnfinalizedPastDay',
              'Finalized',
              'Stale',
              'Reopened',
              'SnapshotInvalid',
            ].map((value) => ({ value, label: value ? attendanceLabel(value) : 'Any' }))}
          />
        </div>
      </details>
      {!ready ? (
        <p role="status">Select an inclusive date range to load the review queue.</p>
      ) : (
        <QueryState
          loading={report.isPending}
          error={report.error}
          retry={() => void report.refetch()}
        />
      )}
      {report.data && !report.isError && (
        <>
          <div className="attendance-section-heading">
            <h2 className="ui-section-title">
              {mode === 'queue' ? 'Review queue' : 'Attendance overview'}
            </h2>
            <p className="text-sm text-muted-foreground">
              {report.data.businessDate ?? `${report.data.from} → ${report.data.to}`} ·{' '}
              {report.data.businessTimeZone}
            </p>
          </div>
          <MetricStrip
            density="compact"
            items={[
              {
                label: 'Scheduled',
                value: report.data.counts.scheduled,
                context: 'Matching employee dates',
              },
              { label: 'Late', value: report.data.counts.late, context: 'Existing grace policy' },
              {
                label: 'Approved leave',
                value: report.data.counts.approvedLeave,
                context: 'Matching employee dates',
              },
              {
                label: 'Requires review',
                value: report.data.counts.requiresReview,
                context: 'Days needing attention',
              },
              {
                label: 'Sources changed',
                value: report.data.counts.stale,
                context: 'Changed sources; not a violation',
              },
              {
                label: 'Ready to finalize',
                value: report.data.counts.readyToFinalize,
                context: 'Subject to final review',
              },
            ]}
          />
          <p className="text-xs text-muted-foreground">
            Counts are server totals across matching records before pagination. Categories can
            overlap. Missing configuration is not zero scheduled work.
          </p>
          <div className="attendance-section-heading">
            <h2 className="ui-section-title">Attendance records</h2>
            <p className="text-sm text-muted-foreground">
              {report.data.rows.totalCount} matching records
            </p>
          </div>
          <Rows
            rows={report.data.rows.items}
            inspect={(row, button) => {
              invoker.current = button;
              setSelected(row);
            }}
          />
          <Pagination
            page={page}
            pages={Math.max(1, Math.ceil(report.data.rows.totalCount / 20))}
            onPage={(value) => change('page', String(value))}
          />
        </>
      )}
      <Sheet
        open={!!selected}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          invoker.current?.focus({ preventScroll: true });
        }}
        title="Attendance day details"
        size="lg"
        description="Inspect the server-reported day without leaving this filtered list."
        footer={
          selected && (
            <LinkButton to={`/hr/attendance/${selected.employeeId}/${selected.businessDate}`}>
              Open full attendance review
            </LinkButton>
          )
        }
      >
        {selected && (
          <div className="attendance-workspace">
            <Facts
              items={[
                ['Employee', `${selected.displayName} · ${selected.employeeNumber}`],
                ['Business date', selected.businessDate],
                ['Record state', attendanceLabel(selected.recordState)],
                ['Currently validated', selected.isCurrentlyValidated ? 'Yes' : 'No'],
                ['Reopen required', selected.requiresReopen ? 'Yes' : 'No'],
              ]}
            />
            <h3 className="ui-subsection-title">Live calculation</h3>
            <CoverageFacts value={selected.live} />
            <h3 className="ui-subsection-title">Currently validated official calculation</h3>
            {selected.official ? (
              <CoverageFacts value={selected.official} />
            ) : (
              <p className="text-sm">
                No currently validated official snapshot. Live facts are not a historical
                substitute.
              </p>
            )}
            <h3 className="ui-subsection-title">Findings</h3>
            <Findings values={selected.findings} />
            <h3 className="ui-subsection-title">Changed sources</h3>
            <Findings values={selected.changedSources} />
          </div>
        )}
      </Sheet>
    </div>
  );
}

function EmployeeHistory({ reporting }: { reporting: boolean }) {
  const { state } = useAuth();
  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [lookupPage, setLookupPage] = useState(1);
  const [employeeId, setEmployeeId] = useState('');
  const [label, setLabel] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [range, setRange] = useState<{ from: string; to: string; employeeId: string } | null>(null);
  const [error, setError] = useState('');
  const employeeRead = state.user?.capabilities.includes('Employee.Read');
  const lookup = useQuery({
    queryKey: ['attendance', 'employee-search', submitted, lookupPage],
    enabled: !!employeeRead && !!submitted,
    queryFn: ({ signal }) =>
      api<EmployeePage>(
        `/api/employees?search=${encodeURIComponent(submitted)}&page=${lookupPage}&pageSize=20`,
        { signal },
      ),
  });
  const suffix = range ? `?from=${range.from}&to=${range.to}` : '';
  const history = useQuery({
    queryKey: ['attendance', 'employee-history', range],
    enabled: reporting && !!range,
    queryFn: ({ signal }) =>
      api<History>(`/api/employees/${range!.employeeId}/attendance-history${suffix}`, { signal }),
  });
  const summary = useQuery({
    queryKey: ['attendance', 'employee-summary', range],
    enabled: reporting && !!range,
    queryFn: ({ signal }) =>
      api<Summary>(`/api/employees/${range!.employeeId}/attendance-summary${suffix}`, { signal }),
  });
  const legacy = useQuery({
    queryKey: ['attendance', 'legacy', range],
    enabled: !!range,
    queryFn: ({ signal }) =>
      api<Legacy[]>(
        `/api/employees/${range!.employeeId}/attendance?fromDate=${range!.from}&toDate=${range!.to}`,
        { signal },
      ),
  });
  return (
    <div className="attendance-workspace">
      <p className="text-sm text-muted-foreground">
        Select one employee and an inclusive range. Reporting.Read is required for calculated
        history and official summaries. Legacy records are read-only.
      </p>
      {employeeRead ? (
        <FilterToolbar>
          <FormField
            id="attendance-employee-search"
            label="Find employee"
            hint="Search name or employee number; includes active and inactive records."
          >
            {(props) => (
              <Input
                {...props}
                value={search}
                maxLength={100}
                onChange={(e) => setSearch(e.target.value)}
              />
            )}
          </FormField>
          <div className="ui-filter-action">
            <Button
              variant="secondary"
              disabled={!search.trim()}
              onClick={() => {
                setLookupPage(1);
                setSubmitted(search.trim());
              }}
            >
              <Search aria-hidden="true" /> Find employee
            </Button>
          </div>
          <SelectField
            id="attendance-employee"
            label="Employee"
            value={employeeId}
            onChange={(value) => {
              setEmployeeId(value);
              setRange(null);
              const found = lookup.data?.items.find((row) => row.employeeId === value);
              if (found) setLabel(`${found.firstName} ${found.lastName} · ${found.employeeNumber}`);
            }}
            options={[
              { value: '', label: 'Select employee' },
              ...(employeeId && !lookup.data?.items.some((row) => row.employeeId === employeeId)
                ? [{ value: employeeId, label }]
                : []),
              ...(lookup.data?.items ?? []).map((row) => ({
                value: row.employeeId,
                label: `${row.firstName} ${row.lastName} · ${row.employeeNumber}`,
              })),
            ]}
          />
        </FilterToolbar>
      ) : (
        <FormField
          id="attendance-employee-id"
          label="Employee ID"
          hint="Employee directory lookup requires Employee.Read; attendance ownership and authorization remain server-enforced."
        >
          {(props) => (
            <Input
              {...props}
              value={employeeId}
              onChange={(e) => {
                setEmployeeId(e.target.value);
                setRange(null);
              }}
            />
          )}
        </FormField>
      )}
      {submitted && employeeRead && (
        <>
          <QueryState
            loading={lookup.isPending}
            error={lookup.error}
            retry={() => void lookup.refetch()}
          />
          {lookup.data && (
            <Pagination
              page={lookupPage}
              pages={Math.max(1, Math.ceil(lookup.data.totalCount / 20))}
              onPage={setLookupPage}
            />
          )}
        </>
      )}
      <FilterToolbar
        as="form"
        onSubmit={(event) => {
          event.preventDefault();
          if (
            !/^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(employeeId) ||
            !from ||
            !to ||
            from > to
          ) {
            setError('Select an employee and valid From/To dates.');
            return;
          }
          setError('');
          setRange({ employeeId, from, to });
        }}
      >
        <FormField id="attendance-history-from" label="From" required>
          {(props) => (
            <Input {...props} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          )}
        </FormField>
        <FormField
          id="attendance-history-to"
          label="To"
          required
          hint="Calculated history and summary: maximum 366 days."
        >
          {(props) => (
            <Input {...props} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          )}
        </FormField>
        <div className="ui-filter-action">
          <Button type="submit">Load employee history</Button>
        </div>
      </FilterToolbar>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {range && (
        <>
          <LinkButton to={`/hr/attendance/${range.employeeId}/${range.to}`}>
            Review selected employee on {range.to}
          </LinkButton>
          {reporting && (
            <>
              <QueryState
                loading={summary.isPending}
                error={summary.error}
                retry={() => void summary.refetch()}
              />
              {summary.data && !summary.isError && (
                <>
                  <h2 className="ui-section-title">Official period summary</h2>
                  <Facts
                    items={[
                      ['Complete', summary.data.isComplete ? 'Yes' : 'No'],
                      ['Effective employment dates', summary.data.effectiveEmploymentDates],
                      ['Finalized working days', summary.data.finalizedWorkingDays],
                      ['Unfinalized working days', summary.data.unfinalizedWorkingDays],
                      ['Stale finalized days', summary.data.staleFinalizedDays],
                      ['Reopened days', summary.data.reopenedDays],
                      ['Configuration required days', summary.data.configurationRequiredDays],
                      ['Requires review days', summary.data.requiresReviewDays],
                      ['Scheduled coverage', duration(summary.data.scheduledMilliseconds)],
                      [
                        'Presence within schedule',
                        duration(summary.data.presenceCoveredScheduledMilliseconds),
                      ],
                      [
                        'Approved leave coverage',
                        duration(summary.data.approvedLeaveCoveredScheduledMilliseconds),
                      ],
                      [
                        'Unexplained coverage',
                        duration(summary.data.unexplainedScheduledMilliseconds),
                      ],
                      [
                        'Precision residual',
                        duration(summary.data.coverageTruncationResidualMilliseconds),
                      ],
                    ]}
                  />
                  <p className="text-xs text-muted-foreground">
                    Official totals include only currently validated frozen revisions. No live
                    fallback or equivalent-day calculation.
                  </p>
                </>
              )}
              <QueryState
                loading={history.isPending}
                error={history.error}
                retry={() => void history.refetch()}
              />
              {history.data && !history.isError && <Rows rows={history.data.dates} />}
            </>
          )}
          <h2 className="ui-section-title">Legacy records — read-only</h2>
          <QueryState
            loading={legacy.isPending}
            error={legacy.error}
            retry={() => void legacy.refetch()}
          />
          {legacy.data &&
            !legacy.isError &&
            (legacy.data.length ? (
              <ul className="space-y-3 text-sm">
                {legacy.data.map((record) => (
                  <li key={record.attendanceId}>
                    {record.attendanceDate} · {record.attendanceStatusName} · IN{' '}
                    {record.checkIn ?? 'Not recorded'} / OUT {record.checkOut ?? 'Not recorded'}
                    {record.remarks && <p>{record.remarks}</p>}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm">No legacy attendance records.</p>
            ))}
        </>
      )}
    </div>
  );
}
