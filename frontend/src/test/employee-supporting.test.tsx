import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../app/providers/query-client';
import { SupportingInformation } from '../features/employees/supporting-information';
import type { Employee } from '../features/employees/contracts';

const employee: Employee = {
  employeeId: 'employee-one',
  employeeNumber: '100000',
  firstName: 'Synthetic',
  lastName: 'Fixture',
  isActive: true,
  dateOfBirth: null,
  gender: null,
  maritalStatus: null,
  nationality: null,
  profilePhoto: null,
  currentEmployment: null,
  createdAt: '',
  updatedAt: '',
  contacts: [],
  addresses: [],
  emergencyContacts: [],
  teacherProfile: null,
};
let calls: { path: string; method: string; body: Record<string, unknown> | null }[];
let fail: boolean;
beforeEach(() => {
  calls = [];
  fail = false;
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string, init?: RequestInit) => {
      const path = String(input);
      const method = init?.method ?? 'GET';
      calls.push({
        path,
        method,
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : null,
      });
      const body = path.includes('csrf')
        ? { token: 'isolated-token' }
        : path.includes('master-data')
          ? [{ id: 'master-one', name: 'Home', isActive: true }]
          : fail
            ? { errors: { Phone: ['Phone rejected by server.'] } }
            : {};
      return new Response(JSON.stringify(body), {
        status: fail && method !== 'GET' ? 400 : 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }),
  );
});
afterEach(() => vi.unstubAllGlobals());
function setup(value = employee, manage = true) {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <SupportingInformation employee={value} manage={manage} />
    </QueryClientProvider>,
  );
}
describe('F5.1B supporting information', () => {
  it('shows permanent number and read-only supporting information without management controls', () => {
    setup(employee, false);
    expect(screen.getByText('100000')).toBeVisible();
    expect(screen.queryByRole('button', { name: /add|edit/i })).not.toBeInTheDocument();
    expect(calls).toHaveLength(0);
  });
  it('validates empty contact and malformed email before submission', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Add contact' }));
    await user.click(screen.getByRole('button', { name: 'Save contact' }));
    expect(await screen.findByText('Enter at least one contact value.')).toBeVisible();
    await user.type(screen.getByLabelText('Work email'), 'invalid');
    await user.click(screen.getByRole('button', { name: 'Save contact' }));
    expect(await screen.findByText('Enter a valid email address.')).toBeVisible();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });
  it('creates a contact using scoped API and CSRF, then reports success', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Add contact' }));
    await user.type(screen.getByLabelText('Mobile'), '0100000000');
    await user.click(screen.getByRole('button', { name: 'Save contact' }));
    expect(await screen.findByText('Contact saved successfully.')).toBeVisible();
    expect(calls.find((call) => call.method === 'POST')).toMatchObject({
      path: '/api/employees/employee-one/contacts',
      body: { mobile: '0100000000', isPrimary: false },
    });
    expect(calls.some((call) => call.path === '/api/auth/csrf')).toBe(true);
    expect(screen.getByRole('button', { name: 'Add contact' })).toHaveFocus();
  });
  it('opens address masters only for address editing and validates required fields', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Add address' }));
    await screen.findAllByRole('option', { name: 'Home' });
    await user.click(screen.getByRole('button', { name: 'Save address' }));
    expect(await screen.findByText('Address type is required.')).toBeVisible();
    expect(screen.getByText('Address line 1 is required.')).toBeVisible();
    await user.selectOptions(screen.getByLabelText(/Address type/), 'master-one');
    await user.type(screen.getByLabelText(/Address line 1/), 'Synthetic address');
    await user.click(screen.getByRole('button', { name: 'Save address' }));
    expect(await screen.findByText('Address saved successfully.')).toBeVisible();
    expect(calls.find((call) => call.method === 'POST')).toMatchObject({
      body: { addressTypeId: 'master-one', addressLine1: 'Synthetic address' },
    });
  });
  it('edits emergency fields using supported alternative phone and address, retaining legacy fields', async () => {
    setup({
      ...employee,
      emergencyContacts: [
        {
          emergencyContactId: 'contact-one',
          name: 'Synthetic guardian',
          relationship: 'Relative',
          phone: '01',
          alternativePhone: '02',
          address: 'Original',
          mobile: 'legacy-mobile',
          email: 'legacy@example.invalid',
          isPrimary: true,
        },
      ],
    });
    const user = userEvent.setup();
    expect(screen.getByText('02')).toBeVisible();
    expect(screen.getByText('Original')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Edit emergency contact' }));
    await user.clear(screen.getByLabelText('Alternative phone'));
    await user.click(screen.getByRole('button', { name: 'Save emergency contact' }));
    expect(await screen.findByText('Emergency contact saved successfully.')).toBeVisible();
    const put = calls.find((call) => call.method === 'PUT');
    expect(put).toMatchObject({
      path: '/api/employees/employee-one/emergency-contacts/contact-one',
      body: { alternativePhone: '', address: 'Original' },
    });
    expect(put?.body).not.toHaveProperty('mobile');
    expect(put?.body).not.toHaveProperty('email');
  });
  it('does not silently clear fields that existing contact update preserves', async () => {
    setup({
      ...employee,
      contacts: [
        {
          employeeContactId: 'contact-one',
          workEmail: null,
          personalEmail: null,
          mobile: '01',
          phone: '02',
          workPhone: null,
          isPrimary: true,
        },
      ],
    });
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Edit contact' }));
    await user.clear(screen.getByLabelText('Phone'));
    await user.click(screen.getByRole('button', { name: 'Save contact' }));
    expect(await screen.findByText(/This API preserves blank values/)).toBeVisible();
    expect(calls.some((call) => call.method === 'PUT')).toBe(false);
  });
  it('protects dirty cancel and Escape without nested overlays', async () => {
    setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Add contact' }));
    await user.type(screen.getByLabelText('Mobile'), '01');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(await screen.findByText('Discard unsaved changes?')).toBeVisible();
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Keep editing' }));
    await user.keyboard('{Escape}');
    expect(await screen.findByText('Discard unsaved changes?')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Discard changes' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
  it('maps server field validation and leaves the form visible', async () => {
    fail = true;
    setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Add emergency contact' }));
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Contact name/), 'Synthetic');
    await user.type(within(dialog).getByLabelText(/Relationship/), 'Relative');
    await user.type(within(dialog).getByLabelText(/^Phone/), '01');
    await user.click(screen.getByRole('button', { name: 'Save emergency contact' }));
    expect(await screen.findByText('Phone rejected by server.')).toBeVisible();
    expect(screen.getByRole('dialog')).toBeVisible();
  });
});
