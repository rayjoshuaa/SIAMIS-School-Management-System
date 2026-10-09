import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../app/providers/auth-provider';
import { createQueryClient } from '../app/providers/query-client';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { MyAttendance } from '../features/clocking/my-attendance';
import { clockTime, sessionDuration, validDate } from '../features/clocking/format';
import {
  activeRoute,
  currentModule,
  availableModules,
  visibleRoutes,
} from '../app/router/navigation';
import type { ClockSession } from '../features/clocking/contracts';

const session: ClockSession = {
  sessionId: '11111111-1111-4111-8111-111111111111',
  workArrangement: 'OnlineClass',
  inEventId: '22222222-2222-4222-8222-222222222222',
  outEventId: null,
  clockedInAtUtc: '2026-10-09T00:30:00.0000001Z',
  clockedOutAtUtc: null,
  inBusinessDate: '2026-10-09',
  outBusinessDate: null,
  businessTimeZone: 'Asia/Bangkok',
  isOpen: true,
};
let current: ClockSession | null;
let history: ClockSession[];
let capabilities: string[];
let employeeId: string | null;
let anonymous: boolean;
let currentFailure: number;
let historyFailure: number;
let commandFailure: number | 'network' | 'malformed';
let hold: boolean;
let replay: boolean;
let total: number;
let calls: {
  path: string;
  method: string;
  body: Record<string, string> | null;
  options?: RequestInit;
}[];
function response(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
beforeEach(() => {
  current = null;
  history = [];
  capabilities = ['SelfService'];
  employeeId = 'linked-fixture';
  anonymous = false;
  currentFailure = historyFailure = 0;
  commandFailure = 0;
  hold = replay = false;
  total = 0;
  calls = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      const path = String(url),
        method = options?.method ?? 'GET';
      const body = options?.body
        ? (JSON.parse(String(options.body)) as Record<string, string>)
        : null;
      calls.push({ path, method, body, options });
      if (path === '/api/auth/me')
        return anonymous
          ? response({}, 401)
          : response({
              userId: 'fixture-user',
              userName: 'Fixture',
              employeeId,
              isActive: true,
              requiresPasswordChange: false,
              capabilities,
              roles: [],
            });
      if (path === '/api/auth/csrf') return response({ token: 'fixture-csrf' });
      if (path === '/api/self/attendance/current') return response(current, currentFailure || 200);
      if (path.startsWith('/api/self/attendance/sessions'))
        return response(
          {
            items: history,
            page: Number(new URL(path, 'http://fixture').searchParams.get('page')),
            pageSize: 20,
            totalCount: total,
          },
          historyFailure || 200,
        );
      if (method === 'POST') {
        if (hold) return new Promise<Response>(() => {});
        if (commandFailure === 'network') throw new TypeError('Fixture network interruption');
        if (commandFailure === 'malformed') return new Response('not json', { status: 201 });
        if (commandFailure) return response({}, commandFailure);
        const result = path.endsWith('clock-in')
          ? { ...session, workArrangement: body!.workArrangement }
          : {
              ...session,
              outEventId: 'fixture-out',
              clockedOutAtUtc: '2026-10-09T01:30:00.0000002Z',
              outBusinessDate: '2026-10-09',
              isOpen: false,
            };
        current = result.isOpen ? (result as ClockSession) : null;
        history = [result as ClockSession];
        total = 1;
        return response({ session: result, isReplay: replay }, replay ? 200 : 201);
      }
      throw new Error(`Unexpected fixture endpoint ${path}`);
    }),
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});
function setup(path = '/self/attendance') {
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
                children: [{ path: '/self/attendance', element: <MyAttendance /> }],
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
const posts = () => calls.filter((call) => call.method === 'POST');
async function ready() {
  await screen.findByText('Not Clocked In');
}
describe('employee clocking presentation and ownership', () => {
  it('shows verified not-clocked-in status, bounded empty history and no commands on load', async () => {
    setup();
    await ready();
    expect(await screen.findByText('No sessions found')).toBeInTheDocument();
    expect(posts()).toHaveLength(0);
    expect(calls.some((x) => x.path.includes('pageSize=20'))).toBe(true);
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
  it('shows an owned open session, Bangkok timestamp and Clock Out without another Clock In', async () => {
    current = session;
    setup();
    expect(await screen.findByText('Clocked In')).toBeInTheDocument();
    expect(screen.getByText(/09 Oct 2026, 07:30:00/)).toBeInTheDocument();
    expect(screen.getByText('Online Class')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clock Out' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Clock In' })).not.toBeInTheDocument();
  });
  it.each(['Employee.Read', 'Payroll.Manage', 'Security.Manage'])(
    'does not infer self-service from %s',
    async (capability) => {
      capabilities = [capability];
      setup();
      await screen.findByText('Access denied');
      expect(calls.filter((x) => x.path.startsWith('/api/self/'))).toHaveLength(0);
    },
  );
  it('denies an unlinked SelfService account without sending clock reads', async () => {
    employeeId = null;
    setup();
    await screen.findByText('Linked employee access required');
    expect(calls.filter((x) => x.path.startsWith('/api/self/'))).toHaveLength(0);
  });
  it('redirects anonymous users to existing login', async () => {
    anonymous = true;
    setup();
    await screen.findByText('Login entry');
    expect(posts()).toHaveLength(0);
  });
  it('does not pretend a failed current read means not clocked in', async () => {
    currentFailure = 500;
    setup();
    await screen.findByText('Current status unavailable');
    expect(screen.queryByText('Not Clocked In')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clock In' })).not.toBeInTheDocument();
  });
  it('keeps current controls available when history fails', async () => {
    historyFailure = 500;
    setup();
    await ready();
    await screen.findByText('History unavailable');
    expect(screen.getByRole('button', { name: 'Clock In' })).toBeEnabled();
  });
  it('maps the new page to its own capability-aware module', () => {
    expect(activeRoute('/self/attendance')?.capability).toBe('SelfService');
    expect(currentModule('/self/attendance')?.id).toBe('self');
    expect(availableModules(['SelfService']).some((x) => x.id === 'self')).toBe(true);
    expect(availableModules(['Payroll.Read']).some((x) => x.id === 'self')).toBe(false);
    expect(visibleRoutes(['SelfService']).some((x) => x.path === '/hr/attendance')).toBe(false);
  });
});
describe('clock commands and retry safety', () => {
  it.each(['On Campus', 'Online Class', 'Remote Work'])(
    'submits the exact %s enum with CSRF and no employee or timestamp',
    async (label) => {
      setup();
      await ready();
      const user = userEvent.setup();
      await user.click(screen.getByRole('combobox', { name: 'Work arrangement' }));
      await user.click(screen.getByRole('option', { name: label }));
      await user.click(screen.getByRole('button', { name: 'Clock In' }));
      await screen.findByRole('status');
      await waitFor(() => expect(posts()).toHaveLength(1));
      const call = posts()[0];
      expect(call.path).toBe('/api/self/attendance/clock-in');
      expect(Object.keys(call.body!).sort()).toEqual(['requestKey', 'workArrangement']);
      expect(call.body?.workArrangement).toBe(label.replaceAll(' ', ''));
      expect(call.body?.requestKey).toMatch(/^[0-9a-f-]{36}$/);
      expect(new Headers(call.options?.headers).get('X-CSRF-TOKEN')).toBe('fixture-csrf');
      expect(call.options?.credentials).toBe('include');
      await screen.findByText('Clocked In');
      expect(screen.getByRole('button', { name: 'Clock Out' })).toBeEnabled();
    },
  );
  it('closes only the returned owned session and refreshes both reads', async () => {
    current = session;
    setup();
    await screen.findByText('Clocked In');
    await userEvent.click(screen.getByRole('button', { name: 'Clock Out' }));
    await screen.findByText('Not Clocked In');
    expect(posts()[0].body?.sessionId).toBe(session.sessionId);
    expect(Object.keys(posts()[0].body!).sort()).toEqual(['requestKey', 'sessionId']);
    expect(await screen.findByText('Closed')).toBeInTheDocument();
    expect(calls.filter((x) => x.path === '/api/self/attendance/current').length).toBeGreaterThan(
      1,
    );
  });
  it('blocks double submission and selection while pending without fake success', async () => {
    hold = true;
    setup();
    await ready();
    await userEvent.dblClick(screen.getByRole('button', { name: 'Clock In' }));
    await waitFor(() => expect(posts()).toHaveLength(1));
    expect(screen.getByRole('button', { name: 'Clock In' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Work arrangement' })).toBeDisabled();
    expect(screen.queryByText('Clocked In')).not.toBeInTheDocument();
  });
  it.each(['network', 500, 'malformed'] as const)(
    'retains the identical command after ambiguous %s failure',
    async (failure) => {
      commandFailure = failure;
      setup();
      await ready();
      await userEvent.click(screen.getByRole('button', { name: 'Clock In' }));
      const retry = await screen.findByRole('button', { name: 'Retry same clock-in request' });
      expect(screen.getByRole('button', { name: 'Clock In' })).toBeDisabled();
      expect(screen.getByRole('combobox', { name: 'Work arrangement' })).toBeDisabled();
      expect(posts()).toHaveLength(1);
      const original = posts()[0].body;
      commandFailure = 0;
      replay = true;
      await userEvent.click(retry);
      await screen.findByText(/confirmed from your previous request/);
      expect(posts()).toHaveLength(2);
      expect(posts()[1].body).toEqual(original);
      expect(
        screen.queryByRole('button', { name: 'Retry same clock-in request' }),
      ).not.toBeInTheDocument();
    },
  );
  it('retains the owned OUT session and key across an ambiguous failure', async () => {
    current = session;
    commandFailure = 'network';
    setup();
    await screen.findByText('Clocked In');
    await userEvent.click(screen.getByRole('button', { name: 'Clock Out' }));
    const retry = await screen.findByRole('button', { name: 'Retry same clock-out request' });
    commandFailure = 0;
    await userEvent.click(retry);
    await screen.findByText('Not Clocked In');
    expect(posts()[1].body).toEqual(posts()[0].body);
  });
  it.each([400, 403, 404, 409])(
    'handles deterministic HTTP %s without automatic mutation retry',
    async (status) => {
      commandFailure = status;
      setup();
      await ready();
      await userEvent.click(screen.getByRole('button', { name: 'Clock In' }));
      await screen.findByText('Clocking needs attention');
      expect(posts()).toHaveLength(1);
      expect(
        screen.queryByRole('button', { name: 'Retry same clock-in request' }),
      ).not.toBeInTheDocument();
    },
  );
  it('preserves central session-expiry handling', async () => {
    commandFailure = 401;
    setup();
    await ready();
    await userEvent.click(screen.getByRole('button', { name: 'Clock In' }));
    await screen.findByText('Login entry');
  });
  it('announces success and moves focus to feedback', async () => {
    setup();
    await ready();
    await userEvent.click(screen.getByRole('button', { name: 'Clock In' }));
    const status = await screen.findByText(/Clock-in recorded/);
    await waitFor(() => expect(status.parentElement).toHaveFocus());
  });
  it('supports keyboard activation of the primary command', async () => {
    setup();
    await ready();
    const button = screen.getByRole('button', { name: 'Clock In' });
    button.focus();
    await userEvent.keyboard('{Enter}');
    await screen.findByText('Clocked In');
    expect(posts()).toHaveLength(1);
  });
});
describe('personal history and precise presentation', () => {
  it('shows open sessions without manufacturing an end or completed duration', async () => {
    history = [session];
    total = 1;
    setup();
    await screen.findByText('Open');
    expect(screen.getByText('Not clocked out')).toBeInTheDocument();
    expect(screen.getByText('Open — no end time')).toBeInTheDocument();
  });
  it('uses Bangkok opening-date filters and resets pagination', async () => {
    setup('/self/attendance?page=3');
    await ready();
    await userEvent.type(screen.getByLabelText('From'), '2026-10-01');
    await userEvent.type(screen.getByLabelText('To'), '2026-10-09');
    await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }));
    await waitFor(() =>
      expect(
        calls.some((x) => x.path.includes('page=1&pageSize=20&from=2026-10-01&to=2026-10-09')),
      ).toBe(true),
    );
  });
  it('rejects reversed filters without querying that range', async () => {
    setup('/self/attendance?from=2026-10-10&to=2026-10-01');
    await ready();
    expect(screen.getByRole('alert')).toHaveTextContent('From on or before To');
    expect(calls.filter((x) => x.path.includes('/sessions'))).toHaveLength(0);
  });
  it('supports returning from an empty out-of-range page', async () => {
    setup('/self/attendance?page=3');
    await screen.findByText('No sessions found');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled();
    await userEvent.click(screen.getByRole('button', { name: 'Previous' }));
    await waitFor(() => expect(calls.some((x) => x.path.includes('page=2'))).toBe(true));
  });
  it('does not call payroll, Leave or HR review APIs', async () => {
    setup();
    await ready();
    await userEvent.click(screen.getByRole('button', { name: 'Clock In' }));
    await screen.findByText('Clocked In');
    expect(
      calls.every(
        (x) => x.path.startsWith('/api/self/attendance/') || x.path.startsWith('/api/auth/'),
      ),
    ).toBe(true);
  });
  it('formats UTC in Bangkok regardless of browser timezone', () => {
    expect(clockTime('2026-10-08T18:30:00Z')).toBe('09 Oct 2026, 01:30:00');
  });
  it('preserves tick precision across a second boundary', () => {
    expect(sessionDuration('2026-10-09T00:00:00.9999999Z', '2026-10-09T00:00:01.9999998Z')).toBe(
      'Less than 1 second',
    );
  });
  it('derives cross-midnight duration only from actual timestamps', () => {
    expect(sessionDuration('2026-10-08T16:30:00Z', '2026-10-08T18:30:05Z')).toBe('2h 0m 5s');
  });
  it('rejects malformed or reversed duration and date inputs', () => {
    expect(sessionDuration('invalid', 'invalid')).toBe('Unavailable');
    expect(sessionDuration('2026-10-09T02:00:00Z', '2026-10-09T01:00:00Z')).toBe('Unavailable');
    expect(validDate('2026-02-30')).toBe(false);
    expect(validDate('bad')).toBe(false);
  });
});
