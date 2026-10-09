import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { PermanentDeletion } from '../features/employees/permanent-deletion';
import { api } from '../lib/api/client';
vi.mock('../lib/api/client', () => ({ api: vi.fn() }));
let capabilities: string[];
vi.mock('../lib/auth/auth-context', () => ({
  useAuth: () => ({ state: { user: { capabilities } } }),
}));
const request = vi.mocked(api);
beforeEach(() => {
  capabilities = ['Employee.Read', 'Employee.DeletePermanent'];
  request.mockReset();
  request.mockResolvedValue({ state: 'Eligible', version: 'a'.repeat(64), blockers: [] });
});
function show() {
  return render(
    <MemoryRouter>
      <QueryClientProvider
        client={
          new QueryClient({
            defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
          })
        }
      >
        <PermanentDeletion id="fixture" number="100123" />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}
describe('restricted permanent deletion', () => {
  it('Employee.Manage does not show deletion or fetch an assessment', () => {
    capabilities = ['Employee.Read', 'Employee.Manage'];
    show();
    expect(screen.queryByText('Restricted administration')).not.toBeInTheDocument();
    expect(request).not.toHaveBeenCalled();
  });
  it('requires Employee.Read in addition to the explicit deletion capability', () => {
    capabilities = ['Employee.DeletePermanent'];
    show();
    expect(request).not.toHaveBeenCalled();
  });
  it('does not fetch automatically and preserves routine lifecycle guidance', () => {
    show();
    expect(screen.getByText(/End Employment is the standard/)).toBeVisible();
    expect(request).not.toHaveBeenCalled();
  });
  it('requires the reason and exact identity before submission', async () => {
    const user = userEvent.setup();
    show();
    await user.click(screen.getByRole('button', { name: 'Assess permanent deletion' }));
    await screen.findByText('Eligible erroneous registration');
    const submit = screen.getByRole('button', { name: 'Permanently delete employee' });
    expect(submit).toBeDisabled();
    await user.type(screen.getByLabelText(/Reason this/), 'Created in error');
    await user.type(screen.getByLabelText(/Type 100123/), '100124');
    expect(submit).toBeDisabled();
    await user.clear(screen.getByLabelText(/Type 100123/));
    await user.type(screen.getByLabelText(/Type 100123/), '100123');
    expect(submit).toBeEnabled();
    await user.click(submit);
    await waitFor(() =>
      expect(request).toHaveBeenCalledWith(
        '/api/employees/fixture/permanent-deletion',
        expect.objectContaining({
          method: 'POST',
          body: { employeeNumber: '100123', reason: 'Created in error', version: 'a'.repeat(64) },
        }),
      ),
    );
  });
  it('blocks confirmation when dependencies exist without revealing private details', async () => {
    request.mockResolvedValue({
      state: 'Blocked',
      version: 'b'.repeat(64),
      blockers: ['protected_records'],
    });
    const user = userEvent.setup();
    show();
    await user.click(screen.getByRole('button', { name: 'Assess permanent deletion' }));
    await screen.findByText('Protected dependencies prevent deletion.');
    expect(screen.getByRole('button', { name: 'Permanently delete employee' })).toBeDisabled();
    expect(screen.queryByLabelText(/Reason this/)).not.toBeInTheDocument();
  });
  it('refreshes assessment and keeps errors after a rejected command', async () => {
    request.mockImplementation(async (_path, options) => {
      if (options?.method === 'POST') throw new Error('conflict');
      return { state: 'Eligible', version: 'a'.repeat(64), blockers: [] };
    });
    const user = userEvent.setup();
    show();
    await user.click(screen.getByRole('button', { name: 'Assess permanent deletion' }));
    await screen.findByText('Eligible erroneous registration');
    await user.type(screen.getByLabelText(/Reason this/), 'Created in error');
    await user.type(screen.getByLabelText(/Type 100123/), '100123');
    await user.click(screen.getByRole('button', { name: 'Permanently delete employee' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Deletion did not complete');
  });
  it('uses accessible Escape dismissal and returns focus to its trigger', async () => {
    const user = userEvent.setup();
    show();
    const trigger = screen.getByRole('button', { name: 'Assess permanent deletion' });
    await user.click(trigger);
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });
});
