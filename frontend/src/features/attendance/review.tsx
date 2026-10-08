import { useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { useAuth } from '../../lib/auth/auth-context';
import { Button } from '../../components/ui/button';
import { Sheet } from '../../components/ui/overlays';
import { Tabs, Pagination } from '../../components/ui/navigation';
import { TableViewport } from '../../components/shared/workspace';
import { Table, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { Facts } from '../employees/presentation';
import type { Day, Event, Review, Revision } from './contracts';
import type { Page } from '../hr/dashboard-contracts';
import { CoverageFacts, Findings, QueryState } from './presentation';
import { payrollBoundary } from './format';
import { AttendanceCommand, type Command } from './command';
import '../employees/employees.css';
import './attendance.css';

export function AttendanceReview() {
  const { employeeId = '', date = '' } = useParams();
  const { state } = useAuth();
  const valid =
    /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(employeeId) &&
    /^\d{4}-\d{2}-\d{2}$/.test(date);
  const path = `/api/employees/${employeeId}/attendance-days/${date}`;
  const review = useQuery({
    queryKey: ['attendance', 'review', employeeId, date],
    enabled: valid,
    queryFn: ({ signal }) => api<Review>(`${path}/review`, { signal }),
  });
  const [command, setCommand] = useState<Command | null>(null);
  const invoker = useRef<HTMLButtonElement | null>(null);
  const manage = state.user?.capabilities.includes('Attendance.Manage');
  const finalize = state.user?.capabilities.includes('Attendance.Finalize');
  if (!valid) return <p role="alert">A valid Employee ID and business date are required.</p>;
  if (!review.data || review.isError)
    return (
      <QueryState
        loading={review.isPending}
        error={review.error}
        retry={() => void review.refetch()}
      />
    );
  const value = review.data;
  const frozen = !!value.latestHistoricalFinalizedRevision && !value.isReopened;
  function open(action: Command, button: HTMLButtonElement) {
    invoker.current = button;
    setCommand(action);
  }
  return (
    <div className="attendance-workspace">
      <Link className="ui-link" to="/hr/attendance">
        ← Attendance workspace
      </Link>
      <Facts
        items={[
          ['Employee ID', employeeId],
          ['Business date', date],
          ['Business time zone', value.calculation.businessTimeZone],
          ['Review case', value.reviewCase?.state ?? 'No review case'],
          [
            'Latest historical revision',
            value.latestHistoricalFinalizedRevision?.revision ?? 'None',
          ],
          ['Currently validated', value.isCurrentlyValidated ? 'Yes' : 'No'],
          ['Stale sources', value.isStale ? 'Yes' : 'No'],
          ['Explicit reopening required', value.requiresReopen ? 'Yes' : 'No'],
          ['Reopened', value.isReopened ? 'Yes' : 'No'],
          ['Absence confirmed', value.isConfirmedAbsent ? 'Yes' : 'No'],
        ]}
      />
      <p className="text-sm text-muted-foreground">{payrollBoundary}</p>
      {value.isStale && (
        <section aria-label="Changed authoritative sources">
          <h2 className="font-semibold">Historical finalization requires source review</h2>
          <p className="text-sm">
            The previous revision remains immutable. Staleness is not an HR violation; reopening and
            refinalization are explicit actions.
          </p>
          <Findings values={value.changedSources} />
        </section>
      )}
      <div className="flex flex-wrap gap-3">
        <Button
          variant="outline"
          disabled={review.isFetching}
          onClick={() => void review.refetch()}
        >
          Refresh review
        </Button>
        {manage && (
          <Button variant="outline" onClick={(e) => open('manual', e.currentTarget)}>
            Record manual evidence
          </Button>
        )}
        {manage && !frozen && (
          <>
            <Button variant="outline" onClick={(e) => open('corrections', e.currentTarget)}>
              Add correction evidence
            </Button>
            <Button
              variant="outline"
              disabled={!value.rawCalculation.events.length}
              onClick={(e) => open('adjudications', e.currentTarget)}
            >
              Include / exclude evidence
            </Button>
          </>
        )}
        {finalize && !frozen && (
          <>
            <Button variant="outline" onClick={(e) => open('confirm-absence', e.currentTarget)}>
              Confirm potential absence
            </Button>
            <Button onClick={(e) => open('finalize', e.currentTarget)}>Finalize day</Button>
          </>
        )}
        {finalize && frozen && (
          <Button variant="outline" onClick={(e) => open('reopen', e.currentTarget)}>
            Reopen finalized day
          </Button>
        )}
      </div>
      <p className="text-xs text-muted-foreground">
        Finalization eligibility is validated by the backend: resolved blocking findings,
        authoritative coverage and explicit confirmation of an unambiguous potential absence where
        applicable. No force-finalize or bulk period finalization is supported. New manual
        observations may make a historical revision stale; they never rewrite it.
      </p>
      <Tabs
        label="Attendance review sections"
        tabs={[
          {
            value: 'calculation',
            label: 'Reviewed calculation',
            content: <Calculation value={value.calculation} />,
          },
          {
            value: 'raw',
            label: 'Raw calculation & events',
            content: <Calculation value={value.rawCalculation} />,
          },
          {
            value: 'evidence',
            label: 'Evidence detail',
            content: <Evidence employeeId={employeeId} date={date} />,
          },
          {
            value: 'decisions',
            label: 'Review audit',
            content: (
              <div className="space-y-4">
                {value.reviewCase && (
                  <details>
                    <summary>Original review calculation</summary>
                    <Calculation value={value.reviewCase.originalCalculation} />
                  </details>
                )}
                {value.history.length ? (
                  value.history.map((action) => (
                    <div key={action.id} className="border-b border-border pb-3">
                      <Facts
                        items={[
                          ['Sequence / action', `${action.sequence} · ${action.action}`],
                          ['Reason', action.reason],
                          ['Occurred at UTC', action.occurredAtUtc],
                          ['Authenticated actor', action.actorUserId],
                          ['Origin', action.origin],
                          ['Event', action.attendanceEventId],
                        ]}
                      />
                    </div>
                  ))
                ) : (
                  <p className="text-sm">No review actions recorded.</p>
                )}
              </div>
            ),
          },
          {
            value: 'revisions',
            label: 'Finalized revisions',
            content: <RevisionHistory employeeId={employeeId} date={date} />,
          },
        ]}
      />
      {command && (
        <AttendanceCommand
          key={command}
          action={command}
          review={value}
          onClose={() => setCommand(null)}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            invoker.current?.focus({ preventScroll: true });
          }}
        />
      )}
    </div>
  );
}
function Calculation({ value }: { value: Day }) {
  return (
    <div className="attendance-workspace">
      <Facts
        items={[
          ['Expected work readiness', value.expectedWork.readiness],
          ['Work calendar', value.expectedWork.calendarName],
          ['Schedule kind', value.expectedWork.scheduleKind],
          ['Schedule finding', value.expectedWork.finding],
          [
            'Late under current policy',
            value.isLateUnderCurrentPolicy == null
              ? 'Unknown / not applicable'
              : value.isLateUnderCurrentPolicy
                ? 'Yes'
                : 'No',
          ],
          [
            'Potential absence',
            value.potentialAbsence ? 'Provisional finding only' : 'Not reported',
          ],
          ['Grace policy', `${value.currentGracePolicy} · ${value.clockInGraceMinutes} minutes`],
        ]}
      />
      <CoverageFacts value={value} />
      <h3 className="font-semibold">Calculation findings</h3>
      <Findings values={value.findings} />
      {[
        ['Expected schedule', value.scheduledIntervals],
        ['Presence within schedule', value.presenceCoveredScheduledIntervals],
        ['Approved leave coverage', value.approvedLeaveCoveredIntervals],
        ['Unexplained scheduled intervals', value.unexplainedScheduledIntervals],
        ['Presence / leave overlap — diagnostic only', value.presenceLeaveOverlapIntervals],
      ].map(([label, intervals]) => (
        <details key={String(label)}>
          <summary>{String(label)}</summary>
          {(intervals as Day['scheduledIntervals']).length ? (
            (intervals as Day['scheduledIntervals']).map((interval, i) => (
              <p className="attendance-interval" key={i}>
                {interval.startUtc} → {interval.endUtc}
              </p>
            ))
          ) : (
            <p className="text-sm">No intervals reported.</p>
          )}
        </details>
      ))}
      <h3 className="font-semibold">Approved leave facts</h3>
      {value.approvedLeaves.length ? (
        value.approvedLeaves.map((leave) => (
          <p key={leave.leaveId} className="break-all text-sm">
            {leave.leaveId} · {leave.observedStatus} · {leave.isPaid ? 'Paid' : 'Unpaid'} · frozen
            leave snapshot {leave.snapshotVersion}
          </p>
        ))
      ) : (
        <p className="text-sm">No approved leave coverage reported.</p>
      )}
      <h3 className="font-semibold">Recorded events</h3>
      <EventTable events={value.events} />
    </div>
  );
}
function EventTable({
  events,
  inspect,
}: {
  events: Event[];
  inspect?: (event: Event, button: HTMLButtonElement) => void;
}) {
  if (!events.length) return <p className="text-sm">No attendance evidence recorded.</p>;
  return (
    <TableViewport label="Attendance evidence">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Exact UTC timestamp</TableHead>
            <TableHead>Direction / source</TableHead>
            <TableHead>Reason / readiness</TableHead>
            {inspect && <TableHead>Details</TableHead>}
          </TableRow>
        </TableHeader>
        <tbody>
          {events.map((event) => (
            <TableRow key={event.attendanceEventId}>
              <TableCell className="break-all">{event.occurredAtUtc}</TableCell>
              <TableCell>
                {event.direction}
                <div>{event.source}</div>
              </TableCell>
              <TableCell>
                {event.reason ?? 'No reason supplied'}
                <div>{event.employmentReadiness}</div>
              </TableCell>
              {inspect && (
                <TableCell>
                  <Button
                    variant="outline"
                    onClick={(e) => inspect(event, e.currentTarget)}
                    aria-label={`Inspect event ${event.attendanceEventId}`}
                  >
                    Inspect event
                  </Button>
                </TableCell>
              )}
            </TableRow>
          ))}
        </tbody>
      </Table>
    </TableViewport>
  );
}
function Evidence({ employeeId, date }: { employeeId: string; date: string }) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
  const invoker = useRef<HTMLButtonElement | null>(null);
  const events = useQuery({
    queryKey: ['attendance', 'events', employeeId, date, page],
    queryFn: ({ signal }) =>
      api<Page<Event>>(
        `/api/employees/${employeeId}/attendance-events?fromDate=${date}&toDate=${date}&page=${page}&pageSize=20`,
        { signal },
      ),
  });
  const detail = useQuery({
    queryKey: ['attendance', 'event', employeeId, selected],
    enabled: !!selected,
    queryFn: ({ signal }) =>
      api<Event>(`/api/employees/${employeeId}/attendance-events/${selected}`, { signal }),
  });
  return (
    <div className="attendance-workspace">
      <QueryState
        loading={events.isPending}
        error={events.error}
        retry={() => void events.refetch()}
      />
      {events.data && !events.isError && (
        <>
          <EventTable
            events={events.data.items}
            inspect={(event, button) => {
              invoker.current = button;
              setSelected(event.attendanceEventId);
            }}
          />
          <Pagination
            page={page}
            pages={Math.max(1, Math.ceil(events.data.totalCount / 20))}
            onPage={setPage}
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
        title="Immutable attendance evidence"
        description="Original evidence is retained. Review decisions never rewrite or delete this event."
      >
        <QueryState
          loading={detail.isPending}
          error={detail.error}
          retry={() => void detail.refetch()}
        />
        {detail.data && !detail.isError && (
          <Facts
            items={[
              ['Exact occurred UTC', detail.data.occurredAtUtc],
              ['Original timestamp', detail.data.originalSourceTimestamp],
              [
                'Business date / zone',
                `${detail.data.businessDate} · ${detail.data.businessTimeZone}`,
              ],
              ['Direction', detail.data.direction],
              ['Source', detail.data.source],
              ['Reason', detail.data.reason],
              ['Received UTC', detail.data.receivedAtUtc],
              ['Actor', detail.data.actorId],
              ['Intake findings', detail.data.intakeAnomalies.join(', ') || 'None'],
            ]}
          />
        )}
      </Sheet>
    </div>
  );
}
function RevisionHistory({ employeeId, date }: { employeeId: string; date: string }) {
  const revisions = useQuery({
    queryKey: ['attendance', 'revisions', employeeId, date],
    queryFn: ({ signal }) =>
      api<Revision[]>(`/api/employees/${employeeId}/attendance-days/${date}/history`, { signal }),
  });
  return (
    <div className="attendance-workspace">
      <p className="text-sm">
        Immutable historical evidence. Historical finalization alone does not establish current
        validity; use current review metadata.
      </p>
      <QueryState
        loading={revisions.isPending}
        error={revisions.error}
        retry={() => void revisions.refetch()}
      />
      {revisions.data &&
        !revisions.isError &&
        (revisions.data.length ? (
          revisions.data.map((revision) => (
            <details key={revision.id}>
              <summary>
                Revision {revision.revision} · {revision.finalizedAtUtc}
              </summary>
              <Facts
                items={[
                  ['Authenticated finalizer', revision.actorUserId],
                  [
                    'Frozen absence confirmation',
                    revision.snapshot.isConfirmedAbsent ? 'Yes' : 'No',
                  ],
                ]}
              />
              <Calculation value={revision.snapshot.calculation} />
              <h3 className="mt-4 font-semibold">Frozen raw evidence</h3>
              <EventTable events={revision.snapshot.rawEvents} />
              <h3 className="mt-4 font-semibold">Frozen review history</h3>
              {revision.snapshot.reviewHistory.map((action) => (
                <p key={action.id} className="text-sm">
                  {action.sequence} · {action.action} · {action.reason} · {action.occurredAtUtc}
                </p>
              ))}
            </details>
          ))
        ) : (
          <p className="text-sm">No finalized revisions recorded.</p>
        ))}
    </div>
  );
}
