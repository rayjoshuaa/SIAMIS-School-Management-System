import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { AuthProvider } from '../app/providers/auth-provider';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { HrDashboard } from '../features/hr/dashboard';

const all = ['Reporting.Read', 'Employee.Read', 'Leave.Read', 'Attendance.Read'];
let capabilities: string[];
let failures: Record<string, number>;
let employeeCount: number;
let empty: boolean;
let loading: boolean;
let expired: boolean;
let networkFailure: boolean;
let calls: string[];
const counts = {
  effectiveEmployees: 3,
  scheduled: 2,
  notScheduled: 1,
  late: 1,
  approvedLeave: 1,
  requiresReview: 1,
  stale: 0,
  configurationRequired: 0,
  readyToFinalize: 0,
};
beforeEach(() => {
  capabilities = [...all];
  failures = {};
  employeeCount = 3;
  empty = false;
  loading = false;
  expired = false;
  networkFailure = false;
  calls = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      const path = new URL(url, 'http://localhost').pathname;
      calls.push(url);
      if (networkFailure && path === '/api/employees')
        throw new TypeError('private network detail');
      if (loading && path !== '/api/auth/me') return new Promise<Response>(() => {});
      const status = expired ? 401 : (failures[path] ?? 200);
      let body: unknown = {};
      if (path === '/api/auth/me')
        body = {
          userId: 'fixture',
          userName: 'HR test',
          employeeId: null,
          isActive: true,
          requiresPasswordChange: false,
          roles: ['HRAdmin'],
          capabilities,
        };
      if (path === '/api/employees')
        body = { items: [], page: 1, pageSize: 1, totalCount: empty ? 0 : employeeCount };
      if (path === '/api/attendance/today')
        body = {
          businessDate: '2026-10-07',
          businessTimeZone: 'Asia/Bangkok',
          populationCount: empty ? 0 : 3,
          generatedAtUtc: '2026-10-07T01:00:00Z',
          counts: empty ? Object.fromEntries(Object.keys(counts).map((key) => [key, 0])) : counts,
          rows: { items: [], totalCount: empty ? 0 : 3, page: 1, pageSize: 1 },
        };
      if (path === '/api/leave-requests/pending')
        body = {
          items: empty
            ? []
            : [
                {
                  leaveId: 'leave-1',
                  employeeId: 'employee-1',
                  employeeName: 'Test Employee',
                  employeeNumber: 'TEST-1',
                  leaveTypeName: 'Annual',
                  startDate: '2026-10-09',
                  endDate: '2026-10-09',
                  status: 'Pending',
                },
              ],
          totalCount: empty ? 0 : 7,
          page: 1,
          pageSize: 5,
        };
      return new Response(JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/json' },
      });
    }),
  );
});
afterEach(() => vi.unstubAllGlobals());
function setup() {
  const cache = createQueryClient();
  cache.setDefaultOptions({ queries: { retry: false } });
  render(
    <QueryClientProvider client={cache}>
      <AuthProvider>
        <MemoryRouter initialEntries={['/hr']}>
          <Routes>
            <Route element={<SessionBoundary />}>
              <Route path="/login" element={<h1>Login entry</h1>} />
              <Route element={<ProtectedRoutes />}>
                <Route element={<CapabilityRoute />}>
                  <Route path="/hr" element={<HrDashboard />} />
                  <Route path="/hr/employees" element={<h1>Employee placeholder</h1>} />
                </Route>
              </Route>
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
  return cache;
}
describe('HR dashboard', () => {
  it('renders server totals, bounded Pending rows and server business date', async () => {
    setup();
    expect(await screen.findByText('Test Employee')).toBeInTheDocument();
    expect(screen.getByText('2026-10-07 · Asia/Bangkok')).toBeInTheDocument();
    expect(screen.getByText('Showing the latest 1 of 7 requests.')).toBeInTheDocument();
    expect(screen.getAllByText('3')).toHaveLength(2);
    expect(calls.filter((x) => x.startsWith('/api/') && !x.includes('/auth/'))).toEqual(
      expect.arrayContaining([
        '/api/employees?page=1&pageSize=1',
        '/api/attendance/today?page=1&pageSize=1',
        '/api/leave-requests/pending?page=1&pageSize=5',
      ]),
    );
    expect(calls.filter((x) => x.includes('/attendance/today'))).toHaveLength(1);
  });
  it('renders true zero and scope-specific empty states', async () => {
    empty = true;
    setup();
    expect(await screen.findByText('No Pending leave requests.')).toBeInTheDocument();
    expect(screen.getByText('No employee records.')).toBeInTheDocument();
    expect(
      screen.getByText('No attendance attention items reported for today.'),
    ).toBeInTheDocument();
  });
  it('shows independent loading regions', async () => {
    loading = true;
    setup();
    expect(await screen.findByText('Loading employee overview')).toBeInTheDocument();
    expect(screen.getByText('Loading attendance overview')).toBeInTheDocument();
    expect(screen.getByText('Loading pending leave')).toBeInTheDocument();
  });
  it('does not fetch or expose sections without their capabilities or infer grants from roles', async () => {
    capabilities = ['Reporting.Read'];
    setup();
    await screen.findByText('2026-10-07 · Asia/Bangkok');
    expect(
      calls.some(
        (x) =>
          x.includes('/api/employees') ||
          x.includes('/leave-requests') ||
          x.includes('/payroll') ||
          x.includes('/documents'),
      ),
    ).toBe(false);
    expect(screen.queryByText('Employee records')).not.toBeInTheDocument();
    expect(screen.queryByText('Pending leave requests')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Payroll' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Documents' })).not.toBeInTheDocument();
  });
  it('keeps healthy sections usable on server failure and supports targeted retry', async () => {
    failures['/api/employees'] = 500;
    setup();
    await screen.findByText('Test Employee');
    expect(screen.getByText('Overview unavailable')).toBeInTheDocument();
    failures = {};
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('Employee records')).toBeInTheDocument();
    expect(calls.filter((x) => x.includes('/leave-requests'))).toHaveLength(1);
  });
  it('does not translate forbidden data to empty or offer repeated unauthorized retries', async () => {
    failures['/api/leave-requests/pending'] = 403;
    setup();
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(screen.queryByText('No Pending leave requests.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument();
  });
  it('keeps a network failure separate from zero data and healthy sections', async () => {
    networkFailure = true;
    setup();
    expect(await screen.findByText('Overview unavailable')).toBeInTheDocument();
    expect(await screen.findByText('Test Employee')).toBeInTheDocument();
    expect(screen.queryByText('No employee records.')).not.toBeInTheDocument();
    expect(screen.queryByText('private network detail')).not.toBeInTheDocument();
  });
  it('reports bounded attendance conflict without fabricated counts', async () => {
    failures['/api/attendance/today'] = 409;
    setup();
    expect(await screen.findByText(/attendance scope could not be resolved/)).toBeInTheDocument();
    expect(
      screen.queryByText('No attendance attention items reported for today.'),
    ).not.toBeInTheDocument();
    expect(await screen.findByText('Test Employee')).toBeInTheDocument();
  });
  it('shows only permitted links, leaves placeholders honest and never requests payroll/document data', async () => {
    capabilities.push('Payroll.Read', 'HRDocuments.Read');
    setup();
    await screen.findByText('Test Employee');
    expect(screen.getByRole('link', { name: 'Payroll' })).toHaveAttribute('href', '/hr/payroll');
    expect(screen.getByRole('link', { name: 'Documents' })).toHaveAttribute(
      'href',
      '/hr/documents',
    );
    expect(calls.some((x) => x.includes('/payroll') || x.includes('/documents'))).toBe(false);
    await userEvent.click(screen.getByRole('link', { name: 'Employees' }));
    expect(screen.getByRole('heading', { name: 'Employee placeholder' })).toBeInTheDocument();
  });
  it('uses F3 session-loss handling and clears dashboard cache', async () => {
    const cache = setup();
    await screen.findByText('Test Employee');
    expired = true;
    await cache.invalidateQueries({ queryKey: ['hr-dashboard'] });
    expect(await screen.findByRole('heading', { name: 'Login entry' })).toBeInTheDocument();
    await waitFor(() => expect(cache.getQueryCache().getAll()).toHaveLength(0));
  });
  it('denies /hr without Reporting.Read before making dashboard requests', async () => {
    capabilities = ['Employee.Read'];
    setup();
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(calls).toEqual(['/api/auth/me']);
    expect(
      within(screen.getByText('Access denied').parentElement!).queryByText('Employee records'),
    ).not.toBeInTheDocument();
  });
});

describe('V2.4 premium HR overview', () => {
  it.each([
    [
      'SystemAdmin',
      [
        'Reporting.Read',
        'Employee.Read',
        'Attendance.Read',
        'Leave.Read',
        'Payroll.Read',
        'Security.Manage',
      ],
      true,
      false,
    ],
    [
      'HRAdmin',
      ['Reporting.Read', 'Employee.Read', 'Attendance.Read', 'Leave.Read', 'HRDocuments.Read'],
      false,
      true,
    ],
    [
      'Combined',
      [
        'Reporting.Read',
        'Employee.Read',
        'Attendance.Read',
        'Leave.Read',
        'Payroll.Read',
        'HRDocuments.Read',
        'Security.Manage',
      ],
      true,
      true,
    ],
  ] as const)(
    'keeps %s shortcuts capability-based without fetching sensitive areas',
    async (_, grants, payroll, documents) => {
      capabilities = [...grants];
      setup();
      await screen.findByText('Test Employee');
      expect(Boolean(screen.queryByRole('link', { name: 'Payroll' }))).toBe(payroll);
      expect(Boolean(screen.queryByRole('link', { name: 'Documents' }))).toBe(documents);
      expect(
        calls.some(
          (x) => x.includes('/payroll') || x.includes('/documents') || x.includes('/admin/'),
        ),
      ).toBe(false);
    },
  );
  it.each([
    ['PayrollAdmin', ['Payroll.Read', 'Payroll.Manage']],
    ['Employee', ['SelfService', 'MasterData.Read']],
  ] as const)('denies organization-wide dashboard to %s', async (_, grants) => {
    capabilities = [...grants];
    setup();
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(calls).toEqual(['/api/auth/me']);
  });
  it('keeps Management reporting separate from HR administration', async () => {
    capabilities = ['Reporting.Read', 'MasterData.Read'];
    setup();
    await screen.findByText('2026-10-07 · Asia/Bangkok');
    expect(screen.getByRole('region', { name: "Today's attendance" })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Workforce overview' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('region', { name: 'Pending leave requests' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Open Attendance' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'User Accounts' })).not.toBeInTheDocument();
  });
  it('labels metrics without duplicating configuration counts or summing attention categories', async () => {
    setup();
    await screen.findByText('Test Employee');
    expect(screen.getAllByText('Configuration required')).toHaveLength(1);
    expect(screen.getByRole('region', { name: 'Workforce overview' })).toHaveTextContent(
      'Includes inactive employees',
    );
    expect(screen.getByRole('region', { name: 'Attendance attention · today' })).toHaveTextContent(
      'not a combined backlog',
    );
    expect(screen.getByRole('list', { name: 'Recent pending leave requests' })).toHaveTextContent(
      'Annual',
    );
  });
  it('makes permitted area navigation keyboard reachable', async () => {
    setup();
    await screen.findByText('Test Employee');
    const link = screen.getByRole('link', { name: 'Employees' });
    for (let step = 0; step < 12 && document.activeElement !== link; step++) await userEvent.tab();
    expect(link).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(
      await screen.findByRole('heading', { name: 'Employee placeholder' }),
    ).toBeInTheDocument();
  });
});

describe('V3.4 HR dashboard presentation', () => {
  it('uses compact supported metrics and puts attendance review before leave in reading order', async () => {
    setup();
    await screen.findByText('Test Employee');
    expect(screen.getByRole('region', { name: 'HR areas' })).toBeInTheDocument();
    const attendance = screen.getByRole('region', { name: 'Attendance attention · today' });
    const leave = screen.getByRole('region', { name: 'Pending leave requests' });
    expect(
      attendance.compareDocumentPosition(leave) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    for (const name of ['Workforce overview', "Today's attendance", 'Pending leave requests']) {
      expect(screen.getByRole('region', { name }).querySelector('.ui-metrics')).toHaveAttribute(
        'data-density',
        'compact',
      );
    }
    expect(attendance).toHaveTextContent('Staleness is a source change, not an HR violation.');
    expect(screen.getByRole('region', { name: "Today's attendance" })).toHaveTextContent(
      'not current-presence or payroll-deduction totals',
    );
  });
  it('links the directory through its existing read capability without introducing commands', async () => {
    setup();
    await screen.findByText('Test Employee');
    const directory = screen.getByRole('link', { name: 'Employees' });
    expect(directory).toHaveAttribute('href', '/hr/employees');
    expect(
      screen.queryByRole('button', { name: /approve|finalize|create/i }),
    ).not.toBeInTheDocument();
    await userEvent.click(directory);
    expect(
      await screen.findByRole('heading', { name: 'Employee placeholder' }),
    ).toBeInTheDocument();
  });
  it('does not expose directory actions to reporting-only users', async () => {
    capabilities = ['Reporting.Read'];
    setup();
    await screen.findByText('2026-10-07 · Asia/Bangkok');
    expect(screen.queryByRole('link', { name: 'Employees' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Open Attendance' })).not.toBeInTheDocument();
    expect(calls.filter((path) => !path.includes('/auth/'))).toEqual([
      '/api/attendance/today?page=1&pageSize=1',
    ]);
  });
});
