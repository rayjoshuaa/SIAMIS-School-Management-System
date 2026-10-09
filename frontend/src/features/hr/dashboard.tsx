import { useId, type ReactNode } from 'react';
import { ArrowUpRight } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Spinner } from '../../components/ui/feedback';
import { MetricStrip, QueueList, SystemState } from '../../components/shared/workspace';
import { Button } from '../../components/ui/button';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { useAuth } from '../../lib/auth/auth-context';
import { can } from '../../lib/auth/capabilities';
import { routes } from '../../app/router/navigation';
import type { Page, PendingLeave, AttendanceOverview } from './dashboard-contracts';
import './dashboard.css';

function Region({
  title,
  context,
  children,
}: {
  title: string;
  context?: ReactNode;
  children: ReactNode;
}) {
  const titleId = useId();
  return (
    <section className="hr-overview-region" aria-labelledby={titleId}>
      <header className="hr-overview-region-header">
        <h2 id={titleId}>{title}</h2>
        {context && <p>{context}</p>}
      </header>
      {children}
    </section>
  );
}
function RequestError({ error, retry }: { error: Error; retry: () => void }) {
  const forbidden = error instanceof ApiError && error.kind === 'forbidden';
  const conflict = error instanceof ApiError && error.kind === 'conflict';
  return (
    <SystemState
      kind={forbidden ? 'permission' : 'error'}
      title={forbidden ? 'Access denied' : 'Overview unavailable'}
      action={
        !forbidden ? (
          <Button variant="outline" onClick={retry}>
            Retry
          </Button>
        ) : undefined
      }
    >
      {forbidden
        ? 'You do not have permission to read this information.'
        : conflict
          ? 'The attendance scope could not be resolved. Open Attendance to narrow the scope or review the current context.'
          : 'This information could not be loaded. Please try again.'}
    </SystemState>
  );
}
function AreaLink({ to, children }: { to: string; children: ReactNode }) {
  return (
    <Link to={to} className="hr-overview-link">
      {children}
      <ArrowUpRight aria-hidden="true" className="size-4" />
    </Link>
  );
}
export function HrDashboard() {
  const { state } = useAuth();
  const user = state.status === 'authenticated' ? state.user : undefined;
  // User-scoped keys complement F3's cache clearing on every identity change.
  const employees = useQuery({
    queryKey: ['hr-dashboard', user?.userId, 'employees'],
    enabled: can(user, 'Employee.Read'),
    queryFn: ({ signal }) => api<Page<unknown>>('/api/employees?page=1&pageSize=1', { signal }),
  });
  const attendance = useQuery({
    queryKey: ['hr-dashboard', user?.userId, 'attendance-today'],
    enabled: can(user, 'Reporting.Read'),
    queryFn: ({ signal }) =>
      api<AttendanceOverview>('/api/attendance/today?page=1&pageSize=1', { signal }),
  });
  const leave = useQuery({
    queryKey: ['hr-dashboard', user?.userId, 'pending-leave'],
    enabled: can(user, 'Leave.Read'),
    queryFn: ({ signal }) =>
      api<Page<PendingLeave>>('/api/leave-requests/pending?page=1&pageSize=5', { signal }),
  });
  const shortcuts = routes.filter(
    (route) =>
      route.group === 'hr' &&
      route.path !== '/hr' &&
      route.visible &&
      route.capability &&
      can(user, route.capability),
  );
  return (
    <div className="hr-overview">
      <div className="hr-overview-summary">
        {can(user, 'Employee.Read') && (
          <Region title="Workforce overview" context="Directory coverage">
            {employees.isPending ? (
              <Spinner label="Loading employee overview" />
            ) : employees.isError ? (
              <RequestError error={employees.error} retry={() => void employees.refetch()} />
            ) : (
              <>
                <MetricStrip
                  density="compact"
                  items={[
                    {
                      label: 'Employee records',
                      value: employees.data.totalCount,
                      context: 'Includes inactive employees',
                    },
                  ]}
                />
                {employees.data.totalCount === 0 && (
                  <p className="hr-overview-note">No employee records.</p>
                )}
                <p className="hr-overview-note">
                  Workforce counts describe directory records, not today's effective employment.
                </p>
              </>
            )}
          </Region>
        )}
        {can(user, 'Reporting.Read') && (
          <Region
            title="Today's attendance"
            context={
              attendance.data
                ? `${attendance.data.businessDate} · ${attendance.data.businessTimeZone}`
                : 'Server-defined business date'
            }
          >
            {attendance.isPending ? (
              <Spinner label="Loading attendance overview" />
            ) : attendance.isError ? (
              <RequestError error={attendance.error} retry={() => void attendance.refetch()} />
            ) : (
              <>
                <MetricStrip
                  density="compact"
                  items={[
                    {
                      label: 'Effective employees',
                      value: attendance.data.counts.effectiveEmployees,
                      context: 'In the reporting scope',
                    },
                    {
                      label: 'Scheduled',
                      value: attendance.data.counts.scheduled,
                      context: 'For this business date',
                    },
                    {
                      label: 'Not scheduled',
                      value: attendance.data.counts.notScheduled,
                      context: 'For this business date',
                    },
                  ]}
                />
                <dl className="hr-overview-facts ui-detail-list">
                  <div>
                    <dt>Late</dt>
                    <dd>{attendance.data.counts.late}</dd>
                  </div>
                  <div>
                    <dt>Approved leave</dt>
                    <dd>{attendance.data.counts.approvedLeave}</dd>
                  </div>
                </dl>
                {attendance.data.populationCount === 0 && (
                  <p className="hr-overview-note">No effective employees for this business date.</p>
                )}
                <p className="hr-overview-note">
                  Live factual counts; categories may overlap. These are not current-presence or
                  payroll-deduction totals.
                </p>
              </>
            )}
          </Region>
        )}
      </div>
      <div className="hr-overview-workload">
        {can(user, 'Reporting.Read') && (
          <Region
            title="Attendance attention · today"
            context="Separate categories · not a combined backlog"
          >
            {attendance.isPending ? (
              <Spinner label="Loading attendance attention" />
            ) : attendance.isError ? (
              <p className="hr-overview-note">
                Attention information is unavailable. Retry the attendance overview above.
              </p>
            ) : (
              <>
                <dl className="hr-overview-attention ui-detail-list">
                  <div>
                    <dt>Requires review</dt>
                    <dd>{attendance.data.counts.requiresReview}</dd>
                  </div>
                  <div>
                    <dt>Ready to finalize</dt>
                    <dd>{attendance.data.counts.readyToFinalize}</dd>
                  </div>
                  <div>
                    <dt>Stale revisions</dt>
                    <dd>{attendance.data.counts.stale}</dd>
                  </div>
                  <div>
                    <dt>Configuration required</dt>
                    <dd>{attendance.data.counts.configurationRequired}</dd>
                  </div>
                </dl>
                {attendance.data.counts.requiresReview === 0 &&
                  attendance.data.counts.readyToFinalize === 0 &&
                  attendance.data.counts.stale === 0 &&
                  attendance.data.counts.configurationRequired === 0 && (
                    <p className="hr-overview-note">
                      No attendance attention items reported for today.
                    </p>
                  )}
                <p className="hr-overview-note">
                  Overlapping categories for today's scope only; historical queues are not included.
                  Staleness is a source change, not an HR violation.
                </p>
                {can(user, 'Attendance.Read') && (
                  <AreaLink to="/hr/attendance">Open Attendance</AreaLink>
                )}
              </>
            )}
          </Region>
        )}
        {can(user, 'Leave.Read') && (
          <Region title="Pending leave requests" context="Review workload · latest five requests">
            {leave.isPending ? (
              <Spinner label="Loading pending leave" />
            ) : leave.isError ? (
              <RequestError error={leave.error} retry={() => void leave.refetch()} />
            ) : (
              <>
                <MetricStrip
                  density="compact"
                  items={[
                    {
                      label: 'Pending requests',
                      value: leave.data.totalCount,
                      context: 'Total reported by the server',
                    },
                  ]}
                />
                {leave.data.totalCount === 0 ? (
                  <SystemState kind="empty" title="No Pending leave requests.">
                    New Pending requests will appear here.
                  </SystemState>
                ) : (
                  <QueueList
                    label="Recent pending leave requests"
                    items={leave.data.items.map((item) => ({
                      id: item.leaveId,
                      title: item.employeeName,
                      metadata: `${item.employeeNumber} · ${item.leaveTypeName} · ${item.startDate} – ${item.endDate}`,
                      status: <span className="hr-overview-status">{item.status}</span>,
                    }))}
                  />
                )}
                {leave.data.totalCount > leave.data.items.length && (
                  <p className="hr-overview-note">
                    Showing the latest {leave.data.items.length} of {leave.data.totalCount}{' '}
                    requests.
                  </p>
                )}
                <p className="hr-overview-note">
                  Review actions depend on your permissions. Leave operations are not yet connected
                  in the frontend.
                </p>
              </>
            )}
          </Region>
        )}
      </div>
      <section className="hr-overview-areas" aria-label="HR areas">
        <div>
          <h2>HR areas</h2>
          <p>Open a permitted HR workspace. Some destinations are not yet connected.</p>
        </div>
        <nav aria-label="HR area shortcuts">
          {shortcuts.map((route) => (
            <Link key={route.path} to={route.path} className="hr-overview-area-link">
              <route.icon aria-hidden="true" className="size-4" />
              <span>{route.label}</span>
            </Link>
          ))}
        </nav>
        {shortcuts.length === 0 && (
          <p className="hr-overview-note">
            No additional HR areas are available with your current permissions.
          </p>
        )}
      </section>
    </div>
  );
}
