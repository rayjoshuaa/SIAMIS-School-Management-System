import { useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { LogIn, LogOut, RefreshCw } from 'lucide-react';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { useAuth } from '../../lib/auth/auth-context';
import { Button } from '../../components/ui/button';
import { Input, Select } from '../../components/ui/controls';
import { Alert, Badge, Spinner } from '../../components/ui/feedback';
import { Pagination } from '../../components/ui/navigation';
import { Table, TableHeader, TableRow, TableHead, TableCell } from '../../components/ui/table';
import { FormField } from '../../components/shared/form-field';
import { FilterToolbar, SystemState, TableViewport } from '../../components/shared/workspace';
import {
  arrangements,
  arrangementLabel,
  type WorkArrangement,
  type ClockSession,
  type ClockResult,
  type SessionPage,
  type ClockCommand,
} from './contracts';
import { clockTime, sessionDuration, validDate } from './format';
import './clocking.css';

export function MyAttendance() {
  const { state } = useAuth();
  const user = state.user;
  if (
    state.status !== 'authenticated' ||
    !user?.capabilities.includes('SelfService') ||
    !user.employeeId
  )
    return (
      <SystemState kind="permission" title="Linked employee access required">
        My Attendance requires an authenticated account with employee self-service access and an
        Employee linkage. Contact your administrator.
      </SystemState>
    );
  return (
    <ClockWorkspace
      key={`${user.userId}/${user.employeeId}`}
      identity={`${user.userId}/${user.employeeId}`}
    />
  );
}
function commandError(error: unknown) {
  if (!(error instanceof ApiError))
    return 'The result could not be confirmed. Retry the same command safely.';
  switch (error.kind) {
    case 'network':
    case 'server':
    case 'request':
      return 'The clocking result could not be confirmed. Check your connection and retry the same command; do not assume it succeeded or failed.';
    case 'conflict':
      return 'Clocking state changed or the server clock has not advanced. Current status is being refreshed. Review it before trying again.';
    case 'not-found':
      return 'That open session is no longer available. Current status is being refreshed.';
    case 'validation':
      return 'Clocking was not accepted. Check your arrangement and employment eligibility, refresh, and try again. If it continues, contact HR or your administrator.';
    case 'forbidden':
      return 'Employee clocking access or account linkage is no longer valid. Contact your administrator.';
    case 'unauthorized':
      return 'Your session expired. Sign in again to continue.';
  }
}
function ClockWorkspace({ identity }: { identity: string }) {
  const cache = useQueryClient();
  const key = ['employee-clocking', identity];
  const [params, setParams] = useSearchParams();
  const from = params.get('from') ?? '',
    to = params.get('to') ?? '';
  const rawPage = Number(params.get('page') ?? 1);
  const page = Number.isSafeInteger(rawPage) && rawPage >= 1 && rawPage <= 107374183 ? rawPage : 1;
  const validFilters = validDate(from) && validDate(to) && (!from || !to || from <= to);
  const [draftFrom, setDraftFrom] = useState(from),
    [draftTo, setDraftTo] = useState(to);
  const [filterError, setFilterError] = useState('');
  const [arrangement, setArrangement] = useState<WorkArrangement>('OnCampus');
  const [retryCommand, setRetryCommand] = useState<ClockCommand | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const busy = useRef(false);
  const feedback = useRef<HTMLDivElement>(null);
  const current = useQuery({
    queryKey: [...key, 'current'],
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: true,
    queryFn: ({ signal }) => api<ClockSession | null>('/api/self/attendance/current', { signal }),
  });
  const filters = new URLSearchParams({ page: String(page), pageSize: '20' });
  if (from) filters.set('from', from);
  if (to) filters.set('to', to);
  const history = useQuery({
    queryKey: [...key, 'sessions', filters.toString()],
    enabled: validFilters,
    queryFn: ({ signal }) =>
      api<SessionPage>(`/api/self/attendance/sessions?${filters}`, { signal }),
  });
  const command = useMutation({
    retry: false,
    mutationFn: (attempt: ClockCommand) =>
      api<ClockResult>(`/api/self/attendance/clock-${attempt.direction}`, {
        method: 'POST',
        body: attempt.body,
      }),
  });
  async function refresh() {
    await cache.invalidateQueries({ queryKey: key });
  }
  async function submit() {
    if (
      busy.current ||
      (!retryCommand && (current.isFetching || current.isError || current.isPending))
    )
      return;
    busy.current = true;
    const attempt: ClockCommand =
      retryCommand ??
      (current.data
        ? {
            direction: 'out',
            body: { requestKey: crypto.randomUUID(), sessionId: current.data.sessionId },
          }
        : {
            direction: 'in',
            body: { requestKey: crypto.randomUUID(), workArrangement: arrangement },
          });
    setRetryCommand(attempt);
    setMessage('');
    setError('');
    try {
      const result = await command.mutateAsync(attempt);
      setRetryCommand(null);
      setMessage(
        `${attempt.direction === 'in' ? 'Clock-in' : 'Clock-out'} ${result.isReplay ? 'confirmed from your previous request' : 'recorded'} at ${clockTime(attempt.direction === 'in' ? result.session.clockedInAtUtc : result.session.clockedOutAtUtc!)} (Bangkok).`,
      );
      await refresh();
    } catch (e) {
      const ambiguous =
        !(e instanceof ApiError) || ['network', 'server', 'request'].includes(e.kind);
      if (!ambiguous) {
        setRetryCommand(null);
        await refresh();
      }
      setError(commandError(e));
    } finally {
      busy.current = false;
      feedback.current?.focus();
    }
  }
  const pending = command.isPending;
  const blocked = pending || current.isFetching || current.isError || current.isPending;
  const open = current.data;
  return (
    <div className="clock-workspace">
      <section
        className="clock-panel"
        aria-labelledby="clock-current-title"
        aria-busy={current.isFetching || pending}
      >
        <div className="clock-section-heading">
          <h2 id="clock-current-title" className="ui-section-title">
            Current session
          </h2>
          <Button
            variant="outline"
            disabled={pending || current.isFetching}
            onClick={() => void refresh()}
          >
            <RefreshCw aria-hidden="true" />
            Refresh status
          </Button>
        </div>
        {current.isPending ? (
          <Spinner label="Checking your current session…" />
        ) : current.isError ? (
          <SystemState kind="error" title="Current status unavailable">
            {current.error.message} Clocking is unavailable until status can be verified.
          </SystemState>
        ) : (
          <div className="clock-current-grid">
            <div className="clock-current-facts">
              <Badge intent={open ? 'info' : 'neutral'}>
                {open ? 'Clocked In' : 'Not Clocked In'}
              </Badge>
              {open ? (
                <dl className="clock-facts">
                  <div>
                    <dt>Clocked in</dt>
                    <dd>
                      <time dateTime={open.clockedInAtUtc}>{clockTime(open.clockedInAtUtc)}</time>
                    </dd>
                  </div>
                  <div>
                    <dt>Work arrangement</dt>
                    <dd>{arrangementLabel(open.workArrangement)}</dd>
                  </div>
                </dl>
              ) : (
                <p>Choose your work arrangement to begin a session.</p>
              )}
              <p className="clock-helper">All times are shown in Asia/Bangkok (UTC+07:00).</p>
            </div>
            <div className="clock-actions">
              {!open && (
                <FormField id="clock-arrangement" label="Work arrangement" required>
                  {(props) => (
                    <Select
                      {...props}
                      label="Work arrangement"
                      options={[...arrangements]}
                      value={arrangement}
                      onValueChange={(value) => setArrangement(value as WorkArrangement)}
                      disabled={pending || !!retryCommand}
                    />
                  )}
                </FormField>
              )}
              <Button
                disabled={blocked || !!retryCommand}
                onClick={() => void submit()}
                loading={pending}
              >
                {open ? <LogOut aria-hidden="true" /> : <LogIn aria-hidden="true" />}
                {open ? 'Clock Out' : 'Clock In'}
              </Button>
            </div>
          </div>
        )}
        <div ref={feedback} tabIndex={-1} className="clock-feedback">
          {message && <p role="status">{message}</p>}
          {error && (
            <div role="alert">
              <Alert intent="warning" title="Clocking needs attention">
                {error}
              </Alert>
            </div>
          )}
        </div>
        {retryCommand && !pending && (
          <Button variant="outline" onClick={() => void submit()}>
            Retry same clock-{retryCommand.direction} request
          </Button>
        )}
        {open && !current.isError && (
          <p className="clock-helper">
            This session is open until you clock out. A missing clock-out may need HR review; it is
            not an automatic absence or deduction.
          </p>
        )}
      </section>
      <section className="clock-history" aria-labelledby="clock-history-title">
        <div className="clock-section-heading">
          <div>
            <h2 id="clock-history-title" className="ui-section-title">
              Personal session history
            </h2>
            <p className="clock-helper">Filter by session opening date in Bangkok.</p>
          </div>
        </div>
        <FilterToolbar
          as="form"
          aria-label="Session history filters"
          onSubmit={(event) => {
            event.preventDefault();
            if (
              !validDate(draftFrom) ||
              !validDate(draftTo) ||
              (draftFrom && draftTo && draftFrom > draftTo)
            ) {
              setFilterError('Choose valid dates with From on or before To.');
              return;
            }
            setFilterError('');
            setParams(
              new URLSearchParams({
                ...(draftFrom ? { from: draftFrom } : {}),
                ...(draftTo ? { to: draftTo } : {}),
              }),
            );
          }}
        >
          <FormField id="clock-from" label="From">
            {(props) => (
              <Input
                {...props}
                type="date"
                value={draftFrom}
                onChange={(e) => setDraftFrom(e.target.value)}
              />
            )}
          </FormField>
          <FormField id="clock-to" label="To">
            {(props) => (
              <Input
                {...props}
                type="date"
                value={draftTo}
                onChange={(e) => setDraftTo(e.target.value)}
              />
            )}
          </FormField>
          <div className="ui-filter-action">
            <Button type="submit" variant="outline">
              Apply filters
            </Button>
          </div>
          <div className="ui-filter-action">
            <Button
              variant="outline"
              onClick={() => {
                setDraftFrom('');
                setDraftTo('');
                setFilterError('');
                setParams({});
              }}
            >
              Clear filters
            </Button>
          </div>
        </FilterToolbar>
        {(filterError || !validFilters) && (
          <p role="alert" className="text-sm text-destructive">
            {filterError || 'Choose valid dates with From on or before To.'}
          </p>
        )}
        {!validFilters ? null : history.isPending ? (
          <Spinner label="Loading your session history…" />
        ) : history.isError ? (
          <SystemState
            kind="error"
            title="History unavailable"
            action={
              <Button variant="outline" onClick={() => void history.refetch()}>
                Retry history
              </Button>
            }
          >
            {history.error.message}
          </SystemState>
        ) : history.data.items.length === 0 ? (
          <SystemState kind="empty" title="No sessions found">
            {from || to
              ? 'No sessions opened in this date range.'
              : 'Your recorded work sessions will appear here after clocking in.'}
          </SystemState>
        ) : (
          <>
            <TableViewport label="Personal clocking sessions">
              <Table>
                <caption className="sr-only">
                  Your sessions. Times in Bangkok; elapsed duration is not payroll time.
                </caption>
                <TableHeader>
                  <TableRow>
                    {[
                      'Clock in · Bangkok',
                      'Clock out · Bangkok',
                      'Work arrangement',
                      'Status',
                      'Elapsed duration',
                    ].map((title) => (
                      <TableHead key={title}>{title}</TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <tbody>
                  {history.data.items.map((session) => (
                    <TableRow key={session.sessionId}>
                      <TableCell>
                        <time dateTime={session.clockedInAtUtc}>
                          {clockTime(session.clockedInAtUtc)}
                        </time>
                      </TableCell>
                      <TableCell>
                        {session.clockedOutAtUtc ? (
                          <time dateTime={session.clockedOutAtUtc}>
                            {clockTime(session.clockedOutAtUtc)}
                          </time>
                        ) : (
                          'Not clocked out'
                        )}
                      </TableCell>
                      <TableCell>{arrangementLabel(session.workArrangement)}</TableCell>
                      <TableCell>
                        <Badge intent={session.isOpen ? 'warning' : 'neutral'}>
                          {session.isOpen ? 'Open' : 'Closed'}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        {sessionDuration(session.clockedInAtUtc, session.clockedOutAtUtc)}
                      </TableCell>
                    </TableRow>
                  ))}
                </tbody>
              </Table>
            </TableViewport>
          </>
        )}
        {validFilters && history.isSuccess && (
          <Pagination
            page={page}
            pages={Math.max(1, Math.ceil(history.data.totalCount / 20))}
            onPage={(next) =>
              setParams((previous) => {
                const updated = new URLSearchParams(previous);
                updated.set('page', String(next));
                return updated;
              })
            }
          />
        )}
      </section>
      <p className="clock-boundary">
        Clocking records your work sessions. It does not approve attendance, determine payroll
        hours, deduct salary or change Leave balances. HR review remains separate.
      </p>
    </div>
  );
}
