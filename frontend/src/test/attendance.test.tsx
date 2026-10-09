import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { AuthProvider } from '../app/providers/auth-provider';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { AttendanceWorkspace } from '../features/attendance/workspace';
import { AttendanceReview } from '../features/attendance/review';
import { activeRoute, currentModule } from '../app/router/navigation';
import { duration } from '../features/attendance/format';
import type { Day, Review, Row } from '../features/attendance/contracts';

const id = '11111111-1111-4111-8111-111111111111';
const date = '2026-10-08';
const base = `/api/employees/${id}/attendance-days/${date}`;
const evidence = {
  attendanceEventId: '22222222-2222-4222-8222-222222222222',
  employeeId: id,
  occurredAtUtc: '2026-10-08T00:35:00.0000001Z',
  businessDate: date,
  businessTimeZone: 'Asia/Bangkok',
  direction: 'In',
  source: 'ManualAuthorized',
  reason: 'Synthetic observation',
  receivedAtUtc: '2026-10-08T01:00:00Z',
  actorId: 'fixture-actor',
  originalSourceTimestamp: '2026-10-08T07:35:00.0000001+07:00',
  employeeWasInactive: false,
  employmentReadiness: 'Ready',
  intakeAnomalies: [],
};
const calculation: Day = {
  employeeId: id,
  businessDate: date,
  businessTimeZone: 'Asia/Bangkok',
  readiness: 'Ready',
  coveragePartitionAvailable: true,
  potentialAbsence: false,
  expectedWork: {
    readiness: 'Ready',
    finding: null,
    calendarName: 'Fixture calendar',
    scheduleKind: 'Weekly',
  },
  events: [evidence],
  findings: [],
  approvedLeaves: [],
  scheduledIntervals: [],
  presenceCoveredScheduledIntervals: [],
  approvedLeaveCoveredIntervals: [],
  unexplainedScheduledIntervals: [],
  presenceLeaveOverlapIntervals: [],
  observedPresenceMilliseconds: 1000,
  scheduledMilliseconds: 3001,
  presenceCoveredScheduledMilliseconds: 1000,
  approvedLeaveCoveredScheduledMilliseconds: 1000,
  paidLeaveCoveredMilliseconds: 1000,
  unpaidLeaveCoveredMilliseconds: 0,
  unexplainedScheduledMilliseconds: 1000,
  coverageTruncationResidualMilliseconds: 1,
  rawStartVarianceTicks: 3000000001,
  rawStartVarianceMilliseconds: 300000,
  calculationContractVersion: 'D9C-v1',
  isLateUnderCurrentPolicy: true,
  clockInGraceMinutes: 5,
  currentGracePolicy: 'FiveMinuteClockInGrace-v1',
};
const row: Row = {
  employeeId: id,
  employeeNumber: 'FIXTURE-EMP',
  displayName: 'Synthetic Employee',
  departmentName: 'Teaching',
  designationName: 'Teacher',
  businessDate: date,
  workState: 'Scheduled',
  timingState: 'Late',
  recordState: 'UnfinalizedPastDay',
  requiresReview: true,
  readyToFinalize: false,
  latestHistoricalRevision: null,
  finalizedAtUtc: null,
  isStale: false,
  requiresReopen: false,
  isReopened: false,
  isCurrentlyValidated: false,
  findings: [
    { code: 'MissingClockOut', message: 'Review the unmatched observation.', sourceIds: [] },
  ],
  changedSources: [],
  attentionCategories: ['RequiresReview'],
  live: {
    ...calculation,
    events: [evidence],
    approvedLeaves: [],
    isConfirmedAbsent: false,
    isLate: true,
    leaveState: 'None',
    leaveExtent: 'None',
  },
  official: null,
};
let review: Review;
let capabilities: string[];
let failure: number;
let paused: boolean;
let commandPaused: boolean;
let empty: boolean;
let calls: { path: string; method: string; body: Record<string, unknown> | null }[];
beforeEach(() => {
  review = {
    employeeId: id,
    businessDate: date,
    version: 7,
    sourceFingerprint: 'A'.repeat(64),
    rawCalculation: calculation,
    calculation,
    reviewCase: null,
    history: [],
    latestHistoricalFinalizedRevision: null,
    isStale: false,
    requiresReopen: false,
    isReopened: false,
    isCurrentlyValidated: false,
    isConfirmedAbsent: false,
    changedSources: [],
  };
  capabilities = [
    'Attendance.Read',
    'Attendance.Manage',
    'Attendance.Finalize',
    'Reporting.Read',
    'Employee.Read',
    'MasterData.Read',
  ];
  failure = 0;
  paused = false;
  commandPaused = false;
  empty = false;
  calls = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      const path = String(url);
      const method = options?.method ?? 'GET';
      calls.push({ path, method, body: options?.body ? JSON.parse(String(options.body)) : null });
      if (commandPaused && method === 'POST') return new Promise<Response>(() => {});
      if (paused && path.startsWith('/api/attendance')) return new Promise<Response>(() => {});
      if (
        failure &&
        (method === 'POST' || path.startsWith('/api/attendance') || path.endsWith('/review'))
      )
        return new Response(
          JSON.stringify({ errors: { request: ['Synthetic backend validation.'] } }),
          { status: failure, headers: { 'Content-Type': 'application/json' } },
        );
      const counts = {
        effectiveEmployees: 1,
        employeeDates: 1,
        scheduled: 9,
        notScheduled: 0,
        onTime: 0,
        late: 3,
        approvedLeave: 2,
        confirmedAbsence: 0,
        potentialAbsence: 0,
        requiresReview: 4,
        finalized: 0,
        stale: 0,
        configurationRequired: 0,
        reopened: 0,
        unfinalizedPastDays: 1,
        readyToFinalize: 0,
      };
      let body: unknown;
      if (path === '/api/auth/me')
        body = {
          userId: 'fixture-user',
          userName: 'fixture',
          employeeId: null,
          isActive: true,
          requiresPasswordChange: false,
          roles: [],
          capabilities,
        };
      else if (path === '/api/auth/csrf') body = { token: 'isolated-csrf' };
      else if (path.startsWith('/api/master-data/'))
        body = [{ id: 'dept', name: 'Teaching', isActive: true }];
      else if (path.startsWith('/api/attendance'))
        body = {
          businessDate: date,
          from: date,
          to: date,
          businessTimeZone: 'Asia/Bangkok',
          counts,
          rows: { items: empty ? [] : [row], page: 1, pageSize: 20, totalCount: empty ? 0 : 21 },
        };
      else if (path.startsWith('/api/employees?'))
        body = {
          items: [
            {
              employeeId: id,
              employeeNumber: 'FIXTURE-EMP',
              firstName: 'Synthetic',
              lastName: 'Employee',
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
        };
      else if (path.includes('attendance-history'))
        body = {
          employeeId: id,
          from: date,
          to: date,
          businessTimeZone: 'Asia/Bangkok',
          dates: [row],
        };
      else if (path.includes('attendance-summary'))
        body = {
          isComplete: false,
          effectiveEmploymentDates: 1,
          finalizedWorkingDays: 0,
          unfinalizedWorkingDays: 1,
          staleFinalizedDays: 0,
          reopenedDays: 0,
          configurationRequiredDays: 1,
          requiresReviewDays: 1,
          scheduledMilliseconds: 0,
          presenceCoveredScheduledMilliseconds: 0,
          approvedLeaveCoveredScheduledMilliseconds: 0,
          unexplainedScheduledMilliseconds: 0,
          coverageTruncationResidualMilliseconds: 0,
        };
      else if (path.includes('/attendance-events?'))
        body = { items: [evidence], page: 1, pageSize: 20, totalCount: 1 };
      else if (path.includes('/attendance-events/') && method === 'GET') body = evidence;
      else if (path.includes('/attendance?')) body = [];
      else if (path.endsWith('/history'))
        body = review.latestHistoricalFinalizedRevision
          ? [review.latestHistoricalFinalizedRevision]
          : [];
      else body = review;
      return new Response(JSON.stringify(body), {
        status: method === 'POST' ? 201 : 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }),
  );
});
afterEach(() => vi.unstubAllGlobals());
function setup(path = '/hr/attendance') {
  const cache = createQueryClient();
  cache.setDefaultOptions({ queries: { retry: false }, mutations: { retry: false } });
  const router = createMemoryRouter(
    [
      {
        element: (
          <AuthProvider>
            <SessionBoundary />
          </AuthProvider>
        ),
        children: [
          { path: '/login', element: <h1>Login entry</h1> },
          {
            element: <ProtectedRoutes />,
            children: [
              {
                element: <CapabilityRoute />,
                children: [
                  { path: '/hr/attendance', element: <AttendanceWorkspace /> },
                  { path: '/hr/attendance/:employeeId/:date', element: <AttendanceReview /> },
                ],
              },
            ],
          },
        ],
      },
    ],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider client={cache}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}
function freeze() {
  review.latestHistoricalFinalizedRevision = {
    id: 'revision-1',
    revision: 1,
    finalizedAtUtc: '2026-10-09T00:00:00Z',
    actorUserId: 'fixture-actor',
    snapshot: { calculation, rawEvents: [evidence], reviewHistory: [], isConfirmedAbsent: false },
  };
  review.isCurrentlyValidated = true;
}
describe('F7 attendance administration', () => {
  it('uses server totals rather than counting visible rows', async () => {
    setup();
    await screen.findByText('Synthetic Employee');
    expect(screen.getByText('9')).toBeVisible();
    expect(screen.getByText('3')).toBeVisible();
    expect(
      calls.some(
        (call) => call.path.includes('/api/attendance/today?') && call.path.includes('pageSize=20'),
      ),
    ).toBe(true);
  });
  it('sends date, department and typed status filters to the server', async () => {
    setup();
    await screen.findByText('Synthetic Employee');
    await userEvent.type(screen.getByLabelText('Business date'), date);
    await userEvent.click(screen.getByLabelText('Department'));
    await userEvent.click(await screen.findByRole('option', { name: 'Teaching' }));
    await userEvent.click(screen.getByText('Factual status filters'));
    await userEvent.selectOptions(screen.getByLabelText('Stale sources'), 'true');
    await waitFor(() =>
      expect(
        calls.some(
          (call) =>
            call.path.includes(`/api/attendance/days/${date}?`) &&
            call.path.includes('departmentId=dept') &&
            call.path.includes('isStale=true'),
        ),
      ).toBe(true),
    );
  });
  it('pages server results', async () => {
    setup();
    await screen.findByText('Synthetic Employee');
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(calls.some((call) => call.path.includes('page=2'))).toBe(true));
  });
  it('does not query an incomplete queue range', async () => {
    setup();
    await userEvent.click(await screen.findByRole('tab', { name: 'Review queue' }));
    expect(await screen.findByText(/Select an inclusive date range/)).toBeVisible();
    expect(calls.some((call) => call.path.includes('review-queue'))).toBe(false);
    await userEvent.type(screen.getByLabelText(/^From/), date);
    await userEvent.type(screen.getByLabelText(/^To/), date);
    await waitFor(() =>
      expect(
        calls.some(
          (call) =>
            call.path.includes(`review-queue?`) &&
            call.path.includes(`from=${date}`) &&
            call.path.includes(`to=${date}`),
        ),
      ).toBe(true),
    );
  });
  it('inspects live facts without inventing official coverage and restores focus', async () => {
    setup();
    const button = await screen.findByRole('button', { name: `Inspect FIXTURE-EMP ${date}` });
    await userEvent.click(button);
    expect(await screen.findByText(/No currently validated official snapshot/)).toBeVisible();
    expect(screen.getByText('Missing clock-out')).toBeVisible();
    await userEvent.click(screen.getByText('Technical reference'));
    expect(screen.getByText('MissingClockOut')).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(button).toHaveFocus());
  });
  it('shows honest empty results', async () => {
    empty = true;
    setup();
    expect(await screen.findByText('No attendance dates match these filters.')).toBeVisible();
  });
  it('shows loading without fake counts', async () => {
    paused = true;
    setup();
    expect(await screen.findByText('Loading attendance information…')).toBeVisible();
    expect(screen.queryByText('9')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Refresh attendance' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Refresh attendance' })).toHaveAttribute(
      'aria-busy',
      'true',
    );
  });
  it.each([400, 403, 409, 503])('shows HTTP %s read errors', async (status) => {
    failure = status;
    setup();
    expect(await screen.findByText('Attendance information unavailable')).toBeVisible();
    expect(screen.queryByText('Synthetic Employee')).not.toBeInTheDocument();
  });
  it('denies organization-wide access to SelfService-only employee', async () => {
    capabilities = ['SelfService'];
    setup();
    expect(await screen.findByText("You don't have permission to access this area.")).toBeVisible();
    expect(calls.some((call) => call.path.includes('/api/attendance'))).toBe(false);
  });
  it('denies PayrollAdmin capabilities without attendance grants', async () => {
    capabilities = ['Payroll.Read', 'Payroll.Manage'];
    setup();
    expect(await screen.findByText("You don't have permission to access this area.")).toBeVisible();
  });
  it('does not query reporting APIs without Reporting.Read', async () => {
    capabilities = ['Attendance.Read', 'MasterData.Read'];
    setup();
    expect(await screen.findByRole('tab', { name: 'Review queue' })).toBeVisible();
    expect(screen.queryByRole('tab', { name: 'Daily overview' })).not.toBeInTheDocument();
    expect(calls.some((call) => call.path.includes('/attendance/today'))).toBe(false);
  });
  it('hides all write actions from read-only attendance users', async () => {
    capabilities = ['Attendance.Read'];
    setup(`/hr/attendance/${id}/${date}`);
    await screen.findByText('Reviewed calculation', { exact: true });
    expect(screen.queryByRole('button', { name: 'Finalize day' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Add correction evidence' }),
    ).not.toBeInTheDocument();
  });
  it('separates Manage from Finalize permissions', async () => {
    capabilities = ['Attendance.Read', 'Attendance.Manage'];
    setup(`/hr/attendance/${id}/${date}`);
    expect(await screen.findByRole('button', { name: 'Add correction evidence' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Finalize day' })).not.toBeInTheDocument();
  });
  it('keeps validation, evidence summary and decision consequences visible without disclosures', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    expect(await screen.findByText('Available evidence events')).toBeVisible();
    expect(screen.getByText('Currently validated')).toBeVisible();
    expect(screen.getByText('Explicit reopening required')).toBeVisible();
    expect(screen.getByText('Finalization safeguards')).toBeVisible();
    expect(
      screen.getByText(/New manual observations may make a historical revision stale/),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Finalize day' })).toBeVisible();
  });
  it('keeps exact event timestamps and residual separate from unexplained coverage', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    expect(await screen.findByText(evidence.occurredAtUtc)).toBeVisible();
    await userEvent.click(screen.getByText('Precision and calculation details'));
    expect(screen.getByText('1 ms')).toBeVisible();
    expect(screen.getAllByText('1,000 ms').length).toBeGreaterThan(1);
  });
  it('requires a reason before sending a correction', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Add correction evidence' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm add correction evidence' }));
    expect(await screen.findByText('A reason is required.')).toBeVisible();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('sends explicit-offset correction, frozen tokens and a request key using CSRF', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Add correction evidence' }));
    await user.type(screen.getByLabelText(/Occurred at/), '2026-10-08T16:00:00.0000001+07:00');
    await user.type(screen.getByLabelText(/^Reason/), 'Synthetic correction');
    await user.selectOptions(screen.getByLabelText(/^Direction/), 'Out');
    await user.click(screen.getByRole('button', { name: 'Confirm add correction evidence' }));
    await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true));
    const call = calls.find((call) => call.method === 'POST')!;
    expect(call.path).toBe(`${base}/corrections`);
    expect(call.body).toMatchObject({
      expectedVersion: 7,
      expectedSourceFingerprint: 'A'.repeat(64),
      occurredAt: '2026-10-08T16:00:00.0000001+07:00',
      direction: 'Out',
      reason: 'Synthetic correction',
    });
    expect(call.body?.manualRequestKey).toMatch(/^[0-9a-f-]{36}$/);
    expect(calls.some((call) => call.path === '/api/auth/csrf')).toBe(true);
  });
  it('rejects timestamps without an explicit offset without sending a mutation', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Add correction evidence' }));
    await userEvent.type(screen.getByLabelText(/Occurred at/), '2026-10-08T16:00:00');
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Fixture reason');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm add correction evidence' }));
    expect(await screen.findByText(/Enter an ISO timestamp/)).toBeVisible();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('sends explicit exclusion without deleting source evidence', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(
      await screen.findByRole('button', { name: 'Include / exclude evidence' }),
    );
    await userEvent.selectOptions(
      screen.getByLabelText(/^Recorded evidence/),
      evidence.attendanceEventId,
    );
    await userEvent.selectOptions(screen.getByLabelText(/^Decision/), 'false');
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Exclude duplicate');
    await userEvent.click(
      screen.getByRole('button', { name: 'Confirm include / exclude evidence' }),
    );
    await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true));
    expect(calls.find((call) => call.method === 'POST')?.body).toMatchObject({
      included: false,
      attendanceEventId: evidence.attendanceEventId,
    });
    expect(calls.some((call) => ['DELETE', 'PUT', 'PATCH'].includes(call.method))).toBe(false);
  });
  it.each([
    ['Finalize day', 'Confirm finalize attendance day', 'finalize'],
    ['Confirm potential absence', 'Confirm confirm potential absence', 'confirm-absence'],
  ])('confirms %s with backend-owned prerequisites', async (button, confirm, endpoint) => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: button }));
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Fixture decision');
    await userEvent.click(screen.getByRole('button', { name: confirm }));
    await waitFor(() =>
      expect(calls.some((call) => call.path === `${base}/${endpoint}`)).toBe(true),
    );
  });
  it('preserves frozen revisions and requires explicit reopening', async () => {
    freeze();
    review.isStale = true;
    review.requiresReopen = true;
    review.isCurrentlyValidated = false;
    review.changedSources = [
      {
        code: 'LeaveCancelled',
        message: 'Approved leave changed after finalization.',
        sourceIds: [],
      },
    ];
    setup(`/hr/attendance/${id}/${date}`);
    expect(await screen.findByText('Leave Cancelled')).toBeVisible();
    await userEvent.click(screen.getByText('Technical reference'));
    expect(screen.getByText('LeaveCancelled')).toBeVisible();
    expect(
      screen.queryByRole('button', { name: 'Add correction evidence' }),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Reopen finalized day' }));
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Review changed leave');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm reopen finalized day' }));
    await waitFor(() => expect(calls.some((call) => call.path === `${base}/reopen`)).toBe(true));
  });
  it('retains immutable historical snapshots in revision view', async () => {
    freeze();
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('tab', { name: 'Finalized revisions' }));
    const revision = await screen.findByText(/Revision 1/);
    await userEvent.click(revision);
    expect(await screen.findByText('Frozen absence confirmation')).toBeVisible();
    expect(screen.getAllByText(evidence.occurredAtUtc).length).toBe(2);
  });
  it('supports manual intake independently, including Unknown, without impersonating employee clocking', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Record manual evidence' }));
    expect(screen.getByText(/not employee self-service clocking/)).toBeVisible();
    await userEvent.type(screen.getByLabelText(/Occurred at/), '2026-10-08T08:00:00Z');
    await userEvent.selectOptions(screen.getByLabelText(/^Direction/), 'Unknown');
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Review source');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm record manual evidence' }));
    await waitFor(() =>
      expect(calls.some((call) => call.path.endsWith('/attendance-events/manual'))).toBe(true),
    );
    expect(calls.find((call) => call.method === 'POST')?.body).not.toHaveProperty(
      'expectedVersion',
    );
  });
  it('retains validation errors and reason after a failed command', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Finalize day' }));
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Keep this explanation');
    failure = 400;
    await userEvent.click(screen.getByRole('button', { name: 'Confirm finalize attendance day' }));
    expect(await screen.findByText('Synthetic backend validation.')).toBeVisible();
    expect(screen.getByLabelText(/^Reason/)).toHaveValue('Keep this explanation');
  });
  it('requires reload after a conflict and prevents repeated submission', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Finalize day' }));
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Fixture reason');
    failure = 409;
    const button = screen.getByRole('button', { name: 'Confirm finalize attendance day' });
    await userEvent.click(button);
    await screen.findByText(/Close this confirmation and refresh/);
    await userEvent.click(button);
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
  });
  it('cancels a consequential command without mutation and restores focus', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    const button = await screen.findByRole('button', { name: 'Finalize day' });
    await userEvent.click(button);
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(button).toHaveFocus());
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('handles session expiry through the existing authentication boundary', async () => {
    failure = 401;
    setup();
    expect(await screen.findByText('Login entry')).toBeVisible();
  });
  it('inspects immutable event details', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('tab', { name: 'Evidence detail' }));
    await userEvent.click(
      await screen.findByRole('button', { name: `Inspect event ${evidence.attendanceEventId}` }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(evidence.originalSourceTimestamp)).toBeVisible();
  });
  it('loads employee history and official summary using a bounded employee search', async () => {
    setup();
    await userEvent.click(await screen.findByRole('tab', { name: 'Employee history & evidence' }));
    await userEvent.type(screen.getByLabelText('Find employee'), 'Synthetic');
    await userEvent.click(screen.getByRole('button', { name: 'Find employee' }));
    await userEvent.click(screen.getByLabelText('Employee'));
    await userEvent.click(
      await screen.findByRole('option', { name: 'Synthetic Employee · FIXTURE-EMP' }),
    );
    await userEvent.type(screen.getByLabelText(/^From/), date);
    await userEvent.type(screen.getByLabelText(/^To/), date);
    await userEvent.click(screen.getByRole('button', { name: 'Load employee history' }));
    expect(await screen.findByText('Official period summary')).toBeVisible();
    expect(await screen.findByText('No legacy attendance records.')).toBeVisible();
    expect(calls.some((call) => call.path.includes('attendance-summary?from='))).toBe(true);
    expect(
      calls.some(
        (call) => call.path.includes('search=Synthetic') && call.path.includes('pageSize=20'),
      ),
    ).toBe(true);
  });
  it('keeps nested review routes within HR with Attendance.Read protection', () => {
    expect(activeRoute(`/hr/attendance/${id}/${date}`)?.capability).toBe('Attendance.Read');
    expect(currentModule(`/hr/attendance/${id}/${date}`)?.id).toBe('hr');
  });
  it('never substitutes unavailable durations with zero or unsafe numeric precision', () => {
    expect(duration(null)).toBe('Unavailable');
    expect(duration(0)).toBe('0 ms');
    expect(duration(Number.MAX_SAFE_INTEGER + 1)).toContain('precision');
  });
  it('displays readable record states while preserving filter values', async () => {
    setup();
    await screen.findByText('Synthetic Employee');
    expect(
      within(screen.getByRole('region', { name: 'Attendance records' })).getByText(
        'Awaiting finalization',
      ),
    ).toBeVisible();
    await userEvent.click(screen.getByText('Factual status filters'));
    await userEvent.selectOptions(screen.getByLabelText('Record state'), 'UnfinalizedPastDay');
    await waitFor(() =>
      expect(calls.some((call) => call.path.includes('recordState=UnfinalizedPastDay'))).toBe(true),
    );
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('protects an unsaved decision and restores editing or invoker focus without submission', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    const button = await screen.findByRole('button', { name: 'Add correction evidence' });
    await userEvent.click(button);
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Unsaved explanation');
    await userEvent.keyboard('{Escape}');
    expect(await screen.findByText('Discard unsaved attendance decision?')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Keep editing' })).toHaveFocus();
    await userEvent.click(screen.getByRole('button', { name: 'Keep editing' }));
    expect(screen.getByLabelText(/^Reason/)).toHaveValue('Unsaved explanation');
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
    await waitFor(() => expect(button).toHaveFocus());
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('names the review action groups without granting extra actions', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    expect(await screen.findByRole('heading', { name: 'Evidence & decisions' })).toBeVisible();
    expect(screen.getByRole('heading', { name: 'Finalize or reopen' })).toBeVisible();
    expect(screen.getByText(/They do not authorize absence/)).toBeVisible();
  });
  it('keeps a pending confirmation open and prevents repeated commands', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Finalize day' }));
    await userEvent.type(screen.getByLabelText(/^Reason/), 'Fixture pending decision');
    commandPaused = true;
    const confirm = screen.getByRole('button', { name: 'Confirm finalize attendance day' });
    await userEvent.click(confirm);
    await waitFor(() => expect(confirm).toBeDisabled());
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('alertdialog')).toBeVisible();
    expect(screen.getByLabelText(/^Reason/)).toBeDisabled();
    await userEvent.click(confirm);
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
  });
});
describe('Stage 1 attendance draft preservation', () => {
  it('preserves unsaved correction evidence during session focus refresh', async () => {
    setup(`/hr/attendance/${id}/${date}`);
    await userEvent.click(await screen.findByRole('button', { name: 'Add correction evidence' }));
    const reason = screen.getByLabelText(/^Reason/);
    await userEvent.type(reason, 'Unsaved attendance correction');
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByLabelText(/^Reason/)).toBe(reason);
    expect(reason).toHaveValue('Unsaved attendance correction');
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
});
