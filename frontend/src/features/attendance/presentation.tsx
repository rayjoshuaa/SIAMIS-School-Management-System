import { Link } from 'react-router-dom';
import { Button } from '../../components/ui/button';
import { Table, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { TableViewport, SystemState } from '../../components/shared/workspace';
import { ApiError } from '../../lib/api/errors';
import { Facts } from '../employees/presentation';
import type { Coverage, Finding, Row } from './contracts';

import { duration } from './format';
export function QueryState({
  loading,
  error,
  retry,
}: {
  loading: boolean;
  error: Error | null;
  retry: () => void;
}) {
  if (loading)
    return (
      <p role="status" className="py-6 text-sm text-muted-foreground">
        Loading attendance information…
      </p>
    );
  if (!error) return null;
  return (
    <SystemState
      kind={error instanceof ApiError && error.status === 403 ? 'permission' : 'error'}
      title="Attendance information unavailable"
      action={
        <Button variant="outline" onClick={retry}>
          Retry
        </Button>
      }
    >
      <p>
        {error instanceof ApiError
          ? error.message
          : 'Unable to load attendance information. Try again.'}
      </p>
      {error instanceof ApiError &&
        Object.values(error.fieldErrors)
          .flat()
          .map((message, i) => <p key={i}>{message}</p>)}
    </SystemState>
  );
}
export function Findings({ values }: { values: Finding[] }) {
  return values.length ? (
    <ul className="space-y-2 text-sm">
      {values.map((finding, i) => (
        <li key={`${finding.code}-${i}`}>
          <strong>{finding.code}</strong> — {finding.message}
        </li>
      ))}
    </ul>
  ) : (
    <p className="text-sm text-muted-foreground">No findings reported.</p>
  );
}
export function CoverageFacts({ value }: { value: Coverage }) {
  return (
    <div className="space-y-3">
      <Facts
        items={[
          ['Readiness', value.readiness],
          [
            'Authoritative coverage partition',
            value.coveragePartitionAvailable ? 'Available' : 'Unavailable',
          ],
          ['Scheduled coverage', duration(value.scheduledMilliseconds)],
          ['Presence within schedule', duration(value.presenceCoveredScheduledMilliseconds)],
          ['Approved leave coverage', duration(value.approvedLeaveCoveredScheduledMilliseconds)],
          ['Paid leave coverage', duration(value.paidLeaveCoveredMilliseconds)],
          ['Unpaid leave coverage', duration(value.unpaidLeaveCoveredMilliseconds)],
          ['Unexplained scheduled coverage', duration(value.unexplainedScheduledMilliseconds)],
          ['Observed presence', duration(value.observedPresenceMilliseconds)],
          ['Conversion residual', duration(value.coverageTruncationResidualMilliseconds)],
          ['Raw arrival variance', duration(value.rawStartVarianceMilliseconds)],
          ['Calculation contract', value.calculationContractVersion],
        ]}
      />
      <p className="text-xs text-muted-foreground">
        Durations are independent integer milliseconds. Conversion residual is precision metadata,
        never absence, worked time, leave or payroll time. Exact intervals remain authoritative.
      </p>
    </div>
  );
}
export function Rows({
  rows,
  inspect,
}: {
  rows: Row[];
  inspect?: (row: Row, button: HTMLButtonElement) => void;
}) {
  if (!rows.length)
    return (
      <p role="status" className="py-5 text-sm">
        No attendance dates match these filters.
      </p>
    );
  return (
    <TableViewport label="Attendance records">
      <Table className="attendance-table">
        <TableHeader>
          <TableRow>
            {['Employee / date', 'Schedule / timing', 'Record status', 'Review', 'Actions'].map(
              (label) => (
                <TableHead key={label}>{label}</TableHead>
              ),
            )}
          </TableRow>
        </TableHeader>
        <tbody>
          {rows.map((row) => (
            <TableRow key={`${row.employeeId}-${row.businessDate}`}>
              <TableCell>
                {row.displayName}
                <div className="text-xs text-muted-foreground">
                  {row.employeeNumber} · {row.businessDate}
                </div>
              </TableCell>
              <TableCell>
                {row.workState}
                <div>{row.timingState}</div>
              </TableCell>
              <TableCell>
                {row.recordState}
                {row.isStale && <div>Source facts changed</div>}
              </TableCell>
              <TableCell>
                {row.readyToFinalize
                  ? 'Ready to finalize'
                  : row.requiresReview
                    ? 'Requires review'
                    : 'No review flag'}
                <div className="text-xs">{row.attentionCategories.join(', ')}</div>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-2">
                  {inspect && (
                    <Button
                      variant="outline"
                      onClick={(event) => inspect(row, event.currentTarget)}
                      aria-label={`Inspect ${row.employeeNumber} ${row.businessDate}`}
                    >
                      Inspect
                    </Button>
                  )}
                  <Link
                    className="ui-link inline-flex min-h-11 items-center"
                    to={`/hr/attendance/${row.employeeId}/${row.businessDate}`}
                  >
                    Review day
                  </Link>
                </div>
              </TableCell>
            </TableRow>
          ))}
        </tbody>
      </Table>
    </TableViewport>
  );
}
