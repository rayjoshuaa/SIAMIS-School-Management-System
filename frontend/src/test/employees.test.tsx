import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider, MemoryRouter } from 'react-router-dom';
import { ShellNavigation } from '../components/layout/shell-navigation';
import { NavigationSessionProvider } from '../lib/auth/navigation-session';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { AuthProvider } from '../app/providers/auth-provider';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../features/auth/auth-boundaries';
import { EmployeeDirectory } from '../features/employees/directory';
import { EmployeeProfile } from '../features/employees/profile';
import { EmployeeForm } from '../features/employees/form';
import { activeRoute, currentModule } from '../app/router/navigation';
import { resolveMasterId } from '../features/employees/data';
import { employmentContext } from '../features/employees/contracts';

const id = '11111111-1111-4111-8111-111111111111';
const record = {
  employmentRecordId: 'record-1',
  employeeId: id,
  departmentId: 'dept',
  designationId: 'designation',
  employmentTypeId: 'type',
  employmentStatusId: 'status',
  locationId: null,
  hiringSourceId: null,
  reportingToEmployeeId: null,
  hireDate: '2026-01-01',
  startDate: null,
  endDate: null as string | null,
  isCurrent: true,
  department: 'Teaching',
  designation: 'Teacher',
  employmentType: 'Full Time',
  employmentStatus: 'Active',
};
const employee = {
  employeeId: id,
  employeeNumber: 'FIXTURE-001',
  firstName: 'Fixture',
  middleName: null,
  lastName: 'Employee',
  preferredName: null,
  isActive: true,
  dateOfBirth: '2000-01-01',
  gender: 'Female',
  maritalStatus: null,
  nationality: null,
  profilePhoto: 'existing-photo',
  currentEmployment: record,
  contacts: [],
  addresses: [],
  emergencyContacts: [],
  teacherProfile: null,
};
const masters: Record<string, unknown[]> = {
  departments: [{ id: 'dept', name: 'Teaching', isActive: true }],
  designations: [{ id: 'designation', name: 'Teacher', isActive: true }],
  'employment-types': [{ id: 'type', name: 'Full Time', isActive: true }],
  'employment-statuses': [
    { id: 'status', name: 'Active', isActive: true, isTerminal: false },
    { id: 'terminal', name: 'Resigned', isActive: true, isTerminal: true },
  ],
  genders: [{ id: 'gender', name: 'Female', isActive: true }],
  'marital-statuses': [],
  nationalities: [],
  locations: [],
  'hiring-sources': [],
};
let capabilities: string[];
let failures: Record<string, number>;
let failureCodes: Record<string, string>;
let empty: boolean;
let paused: boolean;
let expired: boolean;
let accountDecision: boolean;
let history: (typeof record)[];
let calls: {
  path: string;
  method: string;
  body: Record<string, unknown> | null;
  key: string | null;
}[];
beforeEach(() => {
  capabilities = ['Employee.Read', 'Employee.Manage'];
  failures = {};
  failureCodes = {};
  empty = false;
  paused = false;
  expired = false;
  accountDecision = false;
  history = [record];
  calls = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
      const path = String(input);
      const method = init?.method ?? 'GET';
      calls.push({
        path,
        method,
        key: new Headers(init?.headers).get('Idempotency-Key'),
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : null,
      });
      if (paused && path.startsWith('/api/employees?')) return new Promise<Response>(() => {});
      const status = failures[path] ?? (expired && path.startsWith('/api/employees') ? 401 : 200);
      let body: unknown =
        status === 400
          ? {
              errors: {
                FirstName: ['First name rejected by server.'],
                request: ['Check the employment dates.'],
              },
            }
          : {};
      if (status === 200) {
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
        else if (path === '/api/auth/csrf') body = { token: 'isolated-test-token' };
        else if (path.includes('/api/master-data/'))
          body = masters[path.split('/').pop()!.split('?')[0]] ?? [];
        else if (path.endsWith('/employment-history')) body = history;
        else if (path.endsWith('/account-lifecycle'))
          body = {
            accountLinked: accountDecision,
            requiresOffboardingDecision: accountDecision,
            linkedAccountVersion: 'account-version',
            currentEmploymentRecordId: record.employmentRecordId,
            accountStatus: 'Active',
          };
        else if (path.startsWith('/api/employees?'))
          body = {
            items: empty ? [] : [employee],
            page: 1,
            pageSize: 20,
            totalCount: empty ? 0 : 21,
          };
        else body = employee;
      }
      if (failureCodes[path]) body = { code: failureCodes[path] };
      return new Response(JSON.stringify(body), {
        status: status === 200 && method === 'POST' ? 201 : status,
        headers: { 'Content-Type': 'application/json' },
      });
    }),
  );
});

describe('V3.5 employee workspace presentation', () => {
  it('explains the existing effective-date contract beside the current employment without mutation', async () => {
    setup(`/hr/employees/${id}/employment-change`);
    const context = await screen.findByRole('region', { name: 'Employment being reviewed' });
    expect(context).toHaveTextContent('FIXTURE-001');
    expect(context).toHaveTextContent('The current period ends on the preceding day');
    expect(context).toHaveTextContent('Teaching');
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
  });
  it('keeps labelled filters and primary creation navigation without inventing sorting', async () => {
    setup();
    await screen.findByText('FIXTURE-001');
    const filters = screen.getByRole('form', { name: 'Employee directory filters' });
    expect(within(filters).getByLabelText('Search employees')).toBeInTheDocument();
    const create = screen.getByRole('link', { name: 'Create employee' });
    expect(create).toHaveAttribute('href', '/hr/employees/new');
    expect(create).toHaveAttribute('data-variant', 'primary');
    expect(screen.queryByRole('button', { name: /sort/i })).not.toBeInTheDocument();
    expect(calls.some((call) => call.path.includes('sort'))).toBe(false);
  });
  it('uses the approved drawer width and preserves inspection-only behavior', async () => {
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'Quick view FIXTURE-001' }));
    const drawer = await screen.findByRole('dialog', { name: 'Employee quick view' });
    expect(drawer).toHaveAttribute('data-size', 'md');
    expect(
      await within(drawer).findByRole('region', { name: 'Dates and reporting' }),
    ).toBeVisible();
    expect(calls.every((call) => call.method === 'GET')).toBe(true);
    expect(within(drawer).queryByRole('button', { name: /save|approve/i })).not.toBeInTheDocument();
  });
  it('separates record and employment identity and keeps supported profile tabs keyboard accessible', async () => {
    capabilities = ['Employee.Read'];
    setup(`/hr/employees/${id}`);
    const overview = await screen.findByRole('tab', { name: 'Overview' });
    expect(screen.getByText('Record active')).toBeVisible();
    expect(screen.getByText('Employment: Active')).toBeVisible();
    expect(screen.getByRole('region', { name: 'Employment information' })).toHaveTextContent(
      'Teaching',
    );
    expect(screen.getByRole('region', { name: 'Dates and reporting' })).toHaveTextContent(
      '2026-01-01',
    );
    expect(screen.queryByRole('link', { name: 'Edit profile' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Manage photo' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'End employment' })).not.toBeInTheDocument();
    overview.focus();
    await userEvent.keyboard('{ArrowRight}');
    const account = screen.getByRole('tab', { name: 'Account access' });
    expect(account).toHaveFocus();
    expect(account).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByRole('tab', { name: 'Personal & contacts' }));
    expect(await screen.findByText('No contact information recorded.')).toBeVisible();
    expect(calls.some((call) => call.path.startsWith('/api/admin/'))).toBe(false);
  });
});
it('uses one shared compact sizing contract for all four profile actions', async () => {
  setup(`/hr/employees/${id}`);
  const photo = await screen.findByRole('button', { name: 'Manage photo' });
  const actions = [
    photo,
    screen.getByRole('link', { name: 'Edit profile' }),
    screen.getByRole('link', { name: 'Record employment change' }),
    screen.getByRole('link', { name: 'End employment' }),
  ];
  for (const action of actions) {
    expect(action).toHaveAttribute('data-density', 'compact');
    expect(action).toHaveClass('ui-button');
    expect(action).not.toHaveAttribute('style');
  }
  photo.focus();
  await userEvent.keyboard('{Tab}');
  expect(actions[1]).toHaveFocus();
  await userEvent.keyboard('{Tab}');
  expect(actions[2]).toHaveFocus();
  await userEvent.keyboard('{Tab}');
  expect(actions[3]).toHaveFocus();
  expect(actions[1]).toHaveAttribute('href', `/hr/employees/${id}/edit`);
  expect(actions[2]).toHaveAttribute('href', `/hr/employees/${id}/employment-change`);
  expect(actions[3]).toHaveAttribute('href', `/hr/employees/${id}/end-employment`);
});
afterEach(() => vi.unstubAllGlobals());
function setup(path = '/hr/employees') {
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
                  { path: '/hr/employees', element: <EmployeeDirectory /> },
                  { path: '/hr/employees/new', element: <EmployeeForm mode="create" /> },
                  { path: '/hr/employees/:employeeId', element: <EmployeeProfile /> },
                  { path: '/hr/employees/:employeeId/edit', element: <EmployeeForm mode="edit" /> },
                  {
                    path: '/hr/employees/:employeeId/employment-change',
                    element: <EmployeeForm mode="employment-change" />,
                  },
                  {
                    path: '/hr/employees/:employeeId/end-employment',
                    element: <EmployeeForm mode="end-employment" />,
                  },
                  {
                    path: '/hr/employees/:employeeId/rehire',
                    element: <EmployeeForm mode="rehire" />,
                  },
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
async function fillCreate() {
  const user = userEvent.setup();
  await screen.findByRole('form', { name: 'Create employee' });
  await user.type(screen.getByLabelText(/^First name/), 'New');
  await user.type(screen.getByLabelText(/^Last name/), 'Fixture');
  for (const [label, value] of [
    ['Department', 'dept'],
    ['Designation', 'designation'],
    ['Employment type', 'type'],
  ])
    await user.selectOptions(screen.getByLabelText(new RegExp(`^${label}`)), value);
  await user.type(screen.getByLabelText(/^Hire date/), '2026-01-01');
  return user;
}

describe('F5.1B registration', () => {
  it('distinguishes scheduled employment from current work using the effective start date', () => {
    const future = new Date();
    future.setUTCDate(future.getUTCDate() + 30);
    const futureDate = future.toISOString().slice(0, 10);
    expect(
      employmentContext({ ...employee, currentEmployment: { ...record, hireDate: futureDate } }),
    ).toBe(`Scheduled to start ${futureDate}`);
    expect(
      employmentContext({ ...employee, currentEmployment: { ...record, startDate: futureDate } }),
    ).toBe(`Scheduled to start ${futureDate}`);
    expect(employmentContext(employee)).toBe('Active');
  });
  it('saves optional initial contact in the same registration request without provisioning an account', async () => {
    setup('/hr/employees/new');
    await screen.findByRole('form', { name: 'Create employee' });
    expect(screen.queryByLabelText(/^Employment status/)).not.toBeInTheDocument();
    const user = await fillCreate();
    await user.type(screen.getByLabelText('Initial work email'), 'synthetic@example.invalid');
    await user.type(screen.getByLabelText('Initial mobile'), '0100000000');
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(await screen.findByText(/Employee registered successfully/)).toBeVisible();
    const post = calls.find((call) => call.path === '/api/employees' && call.method === 'POST');
    expect(post?.body?.contacts).toEqual([
      { workEmail: 'synthetic@example.invalid', mobile: '0100000000', isPrimary: true },
    ]);
    expect(post?.body).not.toHaveProperty('employeeNumber');
    expect(post?.body).not.toHaveProperty('employmentStatusId');
    expect(post?.body).not.toHaveProperty('initialWorkEmail');
    expect(calls.some((call) => call.path.startsWith('/api/admin/users'))).toBe(false);
  });
});
describe('F5 employee workspace', () => {
  it('loads real-shaped directory data and server pagination', async () => {
    setup();
    expect(await screen.findByText('FIXTURE-001')).toBeInTheDocument();
    expect(screen.getByText('21 employee records')).toBeInTheDocument();
    expect(calls.some((call) => call.path === '/api/employees?page=1&pageSize=20')).toBe(true);
  });
  it('announces loading without inventing employee rows', async () => {
    paused = true;
    setup();
    expect(await screen.findByText('Loading employee information…')).toHaveAttribute(
      'role',
      'status',
    );
    expect(screen.queryByText('FIXTURE-001')).not.toBeInTheDocument();
  });
  it('sends search, filters and page to the server', async () => {
    const router = setup();
    await screen.findByText('FIXTURE-001');
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/^Search employees/), 'Fixture');
    await user.click(screen.getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(router.state.location.search).toContain('search=Fixture'));
    await user.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() =>
      expect(
        calls.some((call) => call.path.includes('page=2') && call.path.includes('search=Fixture')),
      ).toBe(true),
    );
    await user.selectOptions(screen.getByLabelText(/^Employee record/), 'false');
    await waitFor(() =>
      expect(
        calls.some((call) => call.path.includes('isActive=false') && call.path.includes('page=1')),
      ).toBe(true),
    );
  });
  it('shows genuine empty and reset-filter states', async () => {
    empty = true;
    setup('/hr/employees?search=missing');
    expect(await screen.findByText('No employees found')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(screen.getByLabelText(/^Search employees/)).toHaveValue('');
  });
  it('retains filters in a keyboard-accessible drawer and returns focus', async () => {
    const router = setup('/hr/employees?search=Fixture&page=2');
    const button = await screen.findByRole('button', { name: 'Quick view FIXTURE-001' });
    await userEvent.click(button);
    expect(await screen.findByRole('dialog', { name: 'Employee quick view' })).toBeInTheDocument();
    expect(await screen.findByText('No contact information recorded.')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(button).toHaveFocus());
    expect(router.state.location.search).toBe('?search=Fixture&page=2');
  });
  it('navigates drawer to the full profile and renders historical records', async () => {
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'Quick view FIXTURE-001' }));
    await userEvent.click(await screen.findByRole('link', { name: 'Open Employee 360 →' }));
    await userEvent.click(await screen.findByRole('tab', { name: 'Employment history' }));
    expect(await screen.findByRole('region', { name: 'Employment history' })).toBeInTheDocument();
    expect(screen.getByText('Current')).toBeInTheDocument();
    expect(calls.some((call) => /documents|compensations/.test(call.path))).toBe(false);
  });
  it('renders safe personal sections without a fabricated teacher tab', async () => {
    setup(`/hr/employees/${id}`);
    await userEvent.click(await screen.findByRole('tab', { name: 'Personal & contacts' }));
    expect(screen.getByText('Female')).toBeInTheDocument();
    expect(screen.getByText('No addresses recorded.')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Teacher profile' })).not.toBeInTheDocument();
  });
  it('shows error and retries the failed directory request', async () => {
    failures['/api/employees?page=1&pageSize=20'] = 500;
    setup();
    expect(await screen.findByText('Employee information unavailable')).toBeInTheDocument();
    delete failures['/api/employees?page=1&pageSize=20'];
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('FIXTURE-001')).toBeInTheDocument();
  });
  it('handles missing employee details', async () => {
    failures[`/api/employees/${id}`] = 404;
    setup(`/hr/employees/${id}`);
    expect(await screen.findByText('This item could not be found.')).toBeInTheDocument();
  });
  it('does not give directory access to self-service or payroll-only users', async () => {
    capabilities = ['SelfService.Read', 'Payroll.Read'];
    setup();
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(calls.some((call) => call.path.startsWith('/api/employees'))).toBe(false);
  });
  it('hides write actions for Employee.Read and denies direct create navigation', async () => {
    capabilities = ['Employee.Read'];
    const router = setup();
    await screen.findByText('FIXTURE-001');
    expect(screen.queryByRole('link', { name: 'Create employee' })).not.toBeInTheDocument();
    await router.navigate('/hr/employees/new');
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
  });
  it('protects all dynamic management paths and keeps module context', () => {
    for (const action of ['edit', 'employment-change', 'rehire', 'end-employment'])
      expect(activeRoute(`/hr/employees/${id}/${action}`)?.capability).toBe('Employee.Manage');
    expect(activeRoute(`/hr/employees/${id}`)?.capability).toBe('Employee.Read');
    expect(currentModule(`/hr/employees/${id}`)?.id).toBe('hr');
  });
  it('does not submit an empty or whitespace-only create form', async () => {
    setup('/hr/employees/new');
    await screen.findByRole('form');
    await userEvent.type(screen.getByLabelText(/^First name/), '   ');
    await userEvent.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(await screen.findByText('First name is required.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^First name/)).toHaveAttribute('aria-invalid', 'true');
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('creates once through the CSRF client and refreshes the directory', async () => {
    const router = setup('/hr/employees/new');
    const user = await fillCreate();
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(
      await screen.findByText(
        'Employee registered successfully. Permanent employee number: FIXTURE-001.',
      ),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/hr/employees/${id}`);
    const writes = calls.filter((call) => call.path === '/api/employees' && call.method === 'POST');
    expect(writes).toHaveLength(1);
    expect(writes[0].body).toMatchObject({ departmentId: 'dept', firstName: 'New' });
    expect(writes[0].body).not.toHaveProperty('employeeNumber');
    expect(screen.queryByLabelText(/^Employee number/)).not.toBeInTheDocument();
    expect(calls.some((call) => call.path === '/api/auth/csrf')).toBe(true);
    expect(calls.some((call) => /security|accounts/.test(call.path))).toBe(false);
  });
  it('keeps field and service validation visible on a failed save', async () => {
    failures['/api/employees'] = 400;
    setup('/hr/employees/new');
    const user = await fillCreate();
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(await screen.findByText('First name rejected by server.')).toBeInTheDocument();
    expect(screen.getByText('Check the employment dates.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^First name/)).toHaveValue('New');
  });
  it('keeps the create form after a connection failure', async () => {
    setup('/hr/employees/new');
    const user = await fillCreate();
    vi.mocked(fetch).mockRejectedValueOnce(new TypeError('offline'));
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(
      await screen.findByText(/The registration outcome is not confirmed/),
    ).toBeInTheDocument();
    expect(screen.getByRole('form')).toBeInTheDocument();
  });
  it('retries a lost response with the same key and frozen payload, then gives a new registration a new key', async () => {
    const router = setup('/hr/employees/new');
    const user = await fillCreate();
    const normal = vi.mocked(fetch).getMockImplementation()!;
    let loseResponse = true;
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      const response = await normal(input, init);
      if (String(input) === '/api/employees' && init?.method === 'POST' && loseResponse) {
        loseResponse = false;
        throw new TypeError('response lost');
      }
      return response;
    });
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText(/The registration outcome is not confirmed/);
    expect(screen.getByLabelText(/^First name/)).toBeDisabled();
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByLabelText(/^First name/)).toHaveValue('New');
    await user.click(screen.getByRole('button', { name: 'Retry original submission' }));
    await screen.findByText(/Employee registered successfully/);
    const firstAttempts = calls.filter(
      (call) => call.path === '/api/employees' && call.method === 'POST',
    );
    expect(firstAttempts).toHaveLength(2);
    expect(firstAttempts[0].key).toMatch(/^[a-f0-9-]{36}$/);
    expect(firstAttempts[1].key).toBe(firstAttempts[0].key);
    expect(firstAttempts[1].body).toEqual(firstAttempts[0].body);
    await router.navigate('/hr/employees/new');
    await fillCreate();
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText(/Employee registered successfully/);
    const finalAttempts = calls.filter(
      (call) => call.path === '/api/employees' && call.method === 'POST',
    );
    expect(finalAttempts[2].key).not.toBe(firstAttempts[0].key);
  });
  it('keeps one pending request on a double click', async () => {
    setup('/hr/employees/new');
    const user = await fillCreate();
    const normal = vi.mocked(fetch).getMockImplementation()!;
    let complete!: () => void;
    const pending = new Promise<void>((resolve) => {
      complete = resolve;
    });
    let submissions = 0;
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      if (String(input) === '/api/employees' && init?.method === 'POST') {
        submissions++;
        await pending;
      }
      return normal(input, init);
    });
    await user.dblClick(screen.getByRole('button', { name: 'Save employee' }));
    await waitFor(() => expect(submissions).toBe(1));
    expect(screen.getByLabelText(/^First name/)).toBeDisabled();
    complete();
    await screen.findByText(/Employee registered successfully/);
    expect(submissions).toBe(1);
  });
  it.each([
    [409, 'idempotency_conflict', /used with different details/],
    [410, 'registration_key_expired', /key has expired/],
  ])(
    'announces terminal registration error %s without silently starting again',
    async (status, code, message) => {
      failures['/api/employees'] = status as number;
      failureCodes['/api/employees'] = code as string;
      setup('/hr/employees/new');
      const user = await fillCreate();
      await user.click(screen.getByRole('button', { name: 'Save employee' }));
      expect(await screen.findByText(message as RegExp)).toBeVisible();
      expect(screen.getByRole('button', { name: 'Save employee' })).toBeDisabled();
      expect(screen.queryByRole('button', { name: 'Reload record' })).not.toBeInTheDocument();
      expect(
        calls.filter((call) => call.path === '/api/employees' && call.method === 'POST'),
      ).toHaveLength(1);
    },
  );
  it('retains the key for an in-progress response', async () => {
    failures['/api/employees'] = 409;
    failureCodes['/api/employees'] = 'registration_in_progress';
    setup('/hr/employees/new');
    const user = await fillCreate();
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText(/The registration outcome is not confirmed/);
    delete failures['/api/employees'];
    delete failureCodes['/api/employees'];
    await user.click(screen.getByRole('button', { name: 'Retry original submission' }));
    await screen.findByText(/Employee registered successfully/);
    const attempts = calls.filter(
      (call) => call.path === '/api/employees' && call.method === 'POST',
    );
    expect(attempts[0].key).toBe(attempts[1].key);
  });
  it('preserves demographic IDs, photo, employment and omitted children during profile PUT', async () => {
    setup(`/hr/employees/${id}/edit`);
    const first = await screen.findByLabelText(/^First name/);
    await userEvent.clear(first);
    await userEvent.type(first, 'Corrected');
    await userEvent.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText('Employee saved successfully.');
    const body = calls.find((call) => call.method === 'PUT')!.body!;
    expect(body).toMatchObject({
      firstName: 'Corrected',
      genderId: 'gender',
      hireDate: record.hireDate,
      departmentId: record.departmentId,
    });
    expect(body).not.toHaveProperty('contacts');
    expect(body).not.toHaveProperty('teacherProfile');
    expect(body).not.toHaveProperty('employeeNumber');
    expect(body).not.toHaveProperty('profilePhoto');
  });
  it('uses the latest ended record for inactive profile corrections', async () => {
    history = [
      {
        ...record,
        employmentRecordId: 'old',
        hireDate: '2025-01-01',
        isCurrent: false,
        endDate: '2025-12-31',
      },
      { ...record, isCurrent: false, endDate: '2026-08-31' },
    ];
    const original = employee.currentEmployment;
    employee.currentEmployment = null as unknown as typeof record;
    employee.isActive = false;
    try {
      setup(`/hr/employees/${id}/edit`);
      await screen.findByLabelText(/^First name/);
      await userEvent.click(screen.getByRole('button', { name: 'Save employee' }));
      await screen.findByText('Employee saved successfully.');
      expect(calls.find((call) => call.method === 'PUT')?.body).toMatchObject({
        hireDate: '2026-01-01',
        endDate: '2026-08-31',
      });
    } finally {
      employee.currentEmployment = original;
      employee.isActive = true;
    }
  });
  it('fails closed when demographic name-to-ID mapping is ambiguous', () => {
    expect(() =>
      resolveMasterId(
        [
          { id: 'a', name: 'Same', isActive: true },
          { id: 'b', name: 'Same', isActive: true },
        ],
        'Same',
      ),
    ).toThrow('cannot be safely resolved');
  });
  it('warns on unsaved navigation and can stay on the form', async () => {
    setup('/hr/employees/new');
    await screen.findByRole('form');
    await userEvent.type(screen.getByLabelText(/^First name/), 'Unsaved');
    await userEvent.click(screen.getByRole('link', { name: 'Cancel' }));
    expect(
      await screen.findByRole('alertdialog', { name: 'Leave without saving?' }),
    ).toBeInTheDocument();
    await userEvent.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Cancel' }),
    );
    expect(screen.getByLabelText(/^First name/)).toHaveValue('Unsaved');
  });
  it('sends only supplied context fields in a historical employment change', async () => {
    setup(`/hr/employees/${id}/employment-change`);
    await screen.findByLabelText(/^Effective date/);
    await userEvent.type(screen.getByLabelText(/^Effective date/), '2026-02-01');
    await userEvent.selectOptions(screen.getByLabelText(/^Department/), 'dept');
    await userEvent.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText('Employee saved successfully.');
    expect(
      calls.find((call) => call.path.endsWith('/employment-changes') && call.method === 'POST')
        ?.body,
    ).toEqual({ effectiveDate: '2026-02-01', departmentId: 'dept' });
  });
  it('requires Security.Manage for linked-account offboarding decisions', async () => {
    accountDecision = true;
    setup(`/hr/employees/${id}/end-employment`);
    expect(await screen.findByText('Employment cannot be ended here')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Review end of employment' }),
    ).not.toBeInTheDocument();
  });
  it('confirms end employment with record and linked-account version protection', async () => {
    accountDecision = true;
    capabilities.push('Security.Manage');
    setup(`/hr/employees/${id}/end-employment`);
    await screen.findByLabelText(/^End date/);
    await userEvent.type(screen.getByLabelText(/^End date/), '2026-09-01');
    await userEvent.selectOptions(screen.getByLabelText(/^End-of-employment status/), 'terminal');
    await userEvent.selectOptions(screen.getByLabelText(/^Linked account decision/), 'false');
    await userEvent.click(screen.getByRole('button', { name: 'Review end of employment' }));
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
    await userEvent.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: 'End employment' }),
    );
    await screen.findByText('Employee saved successfully.');
    expect(
      calls.find((call) => call.path.endsWith('/end-employment') && call.method === 'POST')?.body,
    ).toMatchObject({
      expectedEmploymentRecordId: 'record-1',
      disableLinkedAccount: false,
      expectedLinkedAccountVersion: 'account-version',
    });
  });
  it('clears employee information on session expiry and returns to login', async () => {
    expired = true;
    setup();
    expect(await screen.findByRole('heading', { name: 'Login entry' })).toBeInTheDocument();
    expect(screen.queryByText('FIXTURE-001')).not.toBeInTheDocument();
  });
  it('prevents repeated submission while the server command is pending', async () => {
    setup('/hr/employees/new');
    const user = await fillCreate();
    let release!: () => void;
    const pending = new Promise<void>((resolve) => {
      release = resolve;
    });
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      const response = await original(input, init);
      if (String(input) === '/api/employees' && init?.method === 'POST') await pending;
      return response;
    });
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Save employee' })).toBeDisabled(),
    );
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(
      calls.filter((call) => call.path === '/api/employees' && call.method === 'POST'),
    ).toHaveLength(1);
    release();
    expect(await screen.findByText(/Employee registered/)).toBeInTheDocument();
  });
  it('shows a conflict without assuming success or clearing entered data', async () => {
    failures['/api/employees'] = 409;
    setup('/hr/employees/new');
    const user = await fillCreate();
    await user.click(screen.getByRole('button', { name: 'Save employee' }));
    expect(
      await screen.findByRole('button', { name: 'Retry original submission' }),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Employee number/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/^First name/)).toHaveValue('New');
    expect(screen.queryByText('Employee created successfully.')).not.toBeInTheDocument();
  });
  it('rehire uses the dedicated endpoint without an account or status PATCH', async () => {
    setup(`/hr/employees/${id}/rehire`);
    await screen.findByRole('form', { name: 'Rehire employee' });
    for (const [label, value] of [
      ['Department', 'dept'],
      ['Designation', 'designation'],
      ['Employment type', 'type'],
      ['Employment status', 'status'],
    ])
      await userEvent.selectOptions(screen.getByLabelText(new RegExp(`^${label}`)), value);
    await userEvent.type(screen.getByLabelText(/^Hire date/), '2026-10-01');
    await userEvent.click(screen.getByRole('button', { name: 'Save employee' }));
    await screen.findByText('Employee saved successfully.');
    expect(calls.find((call) => call.method === 'POST')?.path).toBe(`/api/employees/${id}/rehire`);
    expect(
      calls.some((call) => call.method === 'PATCH' || call.path.includes('/api/security')),
    ).toBe(false);
  });
  it('blocks employee creation when required lookup data is unavailable', async () => {
    failures['/api/master-data/departments?includeInactive=true'] = 500;
    setup('/hr/employees/new');
    expect(await screen.findByText('Employee information unavailable')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save employee' })).not.toBeInTheDocument();
  });
  it('uses bounded active employee search for manager selection', async () => {
    setup('/hr/employees/new');
    await screen.findByRole('form');
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Find reporting employee' }),
      'Fixture',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Find' }));
    const manager = await screen.findByRole('option', { name: 'Fixture Employee · FIXTURE-001' });
    expect(manager).toHaveValue(id);
    expect(
      calls.some(
        (call) =>
          call.path.includes('search=Fixture') &&
          call.path.includes('isActive=true') &&
          call.path.includes('pageSize=20'),
      ),
    ).toBe(true);
  });
  it('keeps Employees active in the shell on nested profile routes', () => {
    render(
      <MemoryRouter initialEntries={[`/hr/employees/${id}`]}>
        <NavigationSessionProvider
          value={{
            userName: 'Fixture',
            mode: 'authenticated',
            capabilities: ['Employee.Read', 'Reporting.Read'],
          }}
        >
          <ShellNavigation />
        </NavigationSessionProvider>
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Employees' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Overview' })).not.toHaveAttribute('aria-current');
  });
  it('does not warn about unsaved changes on a pristine create form', async () => {
    setup('/hr/employees/new');
    await screen.findByRole('form');
    await userEvent.click(screen.getByRole('link', { name: 'Cancel' }));
    expect(await screen.findByText('FIXTURE-001')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });
  it('expires the real session boundary when Account access returns 401', async () => {
    failures[`/api/employees/${id}/account-lifecycle`] = 401;
    setup(`/hr/employees/${id}`);
    await userEvent.click(await screen.findByRole('tab', { name: 'Account access' }));
    expect(await screen.findByText('Login entry')).toBeVisible();
    expect(screen.queryByRole('tabpanel', { name: 'Account access' })).not.toBeInTheDocument();
  });
  it('keeps the profile available while account readiness is forbidden', async () => {
    failures[`/api/employees/${id}/account-lifecycle`] = 403;
    setup(`/hr/employees/${id}`);
    await userEvent.click(await screen.findByRole('tab', { name: 'Account access' }));
    expect(await screen.findByText('Employee information unavailable')).toBeVisible();
    expect(screen.getByRole('heading', { name: 'Fixture Employee' })).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Open User Accounts' })).not.toBeInTheDocument();
  });
});
describe('Stage 1 same-session page-state preservation', () => {
  it('preserves unsaved registration input during window-focus revalidation', async () => {
    setup('/hr/employees/new');
    const user = await fillCreate();
    const first = screen.getByLabelText(/^First name/);
    await user.type(first, ' unsaved');
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByLabelText(/^First name/)).toBe(first);
    expect(first).toHaveValue('New unsaved');
    expect(calls.filter((call) => call.path === '/api/auth/me')).toHaveLength(2);
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('preserves the Employee 360 profile editor draft during focus revalidation', async () => {
    setup(`/hr/employees/${id}/edit`);
    const first = await screen.findByLabelText(/^First name/);
    await userEvent.clear(first);
    await userEvent.type(first, 'Unsaved profile edit');
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByLabelText(/^First name/)).toBe(first);
    expect(first).toHaveValue('Unsaved profile edit');
    expect(calls.some((call) => call.method === 'PUT')).toBe(false);
  });
  it('preserves unapplied directory search input without silently applying it', async () => {
    const router = setup();
    const search = await screen.findByLabelText(/^Search employees/);
    await userEvent.type(search, 'Unapplied directory draft');
    await act(async () => {
      fireEvent(window, new Event('focus'));
    });
    expect(screen.getByLabelText(/^Search employees/)).toBe(search);
    expect(search).toHaveValue('Unapplied directory draft');
    expect(router.state.location.search).not.toContain('Unapplied');
  });
});
