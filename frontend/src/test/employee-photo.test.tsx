import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { EmployeeAvatar, EmployeePhotoControl } from '../features/employees/photo';
import { ApiError } from '../lib/api/errors';
import { api } from '../lib/api/client';
vi.mock('../lib/api/client', () => ({ api: vi.fn() }));
const request = vi.mocked(api);
let metadata = {
  version: null as string | null,
  hasPhoto: false,
  width: null as number | null,
  height: null as number | null,
  sizeBytes: null as number | null,
  contentType: null as string | null,
};
const createUrl = vi.fn(() => 'blob:private-preview');
const revokeUrl = vi.fn();
beforeEach(() => {
  metadata = {
    version: null,
    hasPhoto: false,
    width: null,
    height: null,
    sizeBytes: null,
    contentType: null,
  };
  createUrl.mockClear();
  revokeUrl.mockClear();
  request.mockReset();
  vi.stubGlobal(
    'URL',
    class extends URL {
      static createObjectURL = createUrl;
      static revokeObjectURL = revokeUrl;
    },
  );
  request.mockImplementation(async (_path, options) => {
    if (options?.response === 'blob') return new Blob(['private bytes'], { type: 'image/png' });
    if (options?.method === 'PUT') return { ...metadata, hasPhoto: true, version: 'new-version' };
    if (options?.method === 'DELETE')
      return { ...metadata, hasPhoto: false, version: 'removed-version' };
    return metadata;
  });
});
afterEach(() => vi.unstubAllGlobals());
function setup(manage = true) {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={cache}>
      <EmployeeAvatar id="employee-id" name="Fixture Employee" />
      <EmployeePhotoControl id="employee-id" name="Fixture Employee" manage={manage} />
    </QueryClientProvider>,
  );
}
describe('F5.1C private photographs', () => {
  it('keeps initials and omits mutation controls without Employee.Manage', async () => {
    setup(false);
    expect(await screen.findByLabelText('Fixture Employee')).toHaveTextContent('FE');
    expect(screen.queryByRole('button', { name: 'Manage photo' })).not.toBeInTheDocument();
    expect(request.mock.calls.every(([, options]) => !options?.method)).toBe(true);
  });
  it('loads an authenticated blob rather than using a public or legacy image URL', async () => {
    metadata = { ...metadata, hasPhoto: true, version: 'revision-id' };
    const view = setup();
    await waitFor(() => expect(createUrl).toHaveBeenCalled());
    expect(request).toHaveBeenCalledWith(
      '/api/employees/employee-id/photo/content?version=revision-id',
      expect.objectContaining({ response: 'blob' }),
    );
    view.unmount();
    expect(revokeUrl).toHaveBeenCalledWith('blob:private-preview');
  });
  it('uploads selected JPEG/PNG with the observed version in FormData', async () => {
    metadata = { ...metadata, hasPhoto: true, version: 'revision-id' };
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Manage photo' }));
    const file = new File(['synthetic PNG'], 'photo.png', { type: 'image/png' });
    await userEvent.upload(await screen.findByLabelText('JPEG or PNG photograph'), file);
    await userEvent.click(screen.getByRole('button', { name: 'Replace photo' }));
    expect(await screen.findByText('Profile photograph saved.')).toBeVisible();
    const [, options] = request.mock.calls.find(([, value]) => value?.method === 'PUT')!;
    expect(options?.body).toBeInstanceOf(FormData);
    expect((options!.body as FormData).get('expectedVersion')).toBe('revision-id');
    expect((options!.body as FormData).get('file')).toBe(file);
  });
  it('rejects oversized and unsupported selections before submitting', async () => {
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Manage photo' }));
    const input = await screen.findByLabelText('JPEG or PNG photograph');
    await userEvent.upload(
      input,
      new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'large.png', { type: 'image/png' }),
    );
    expect(screen.getByText('Maximum photo size is 5 MiB.')).toBeVisible();
    await userEvent
      .setup({ applyAccept: false })
      .upload(input, new File(['pdf'], 'bad.pdf', { type: 'application/pdf' }));
    expect(screen.getByText('Choose a JPEG or PNG photograph.')).toBeVisible();
    expect(request.mock.calls.some(([, options]) => options?.method)).toBe(false);
  });
  it('requires explicit removal confirmation and uses the current version', async () => {
    metadata = { ...metadata, hasPhoto: true, version: 'revision-id' };
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Manage photo' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Remove photo' }));
    expect(request.mock.calls.some(([, options]) => options?.method === 'DELETE')).toBe(false);
    await userEvent.click(screen.getByRole('button', { name: 'Confirm removal' }));
    expect(await screen.findByText('Profile photograph removed.')).toBeVisible();
    expect(request).toHaveBeenCalledWith('/api/employees/employee-id/photo', {
      method: 'DELETE',
      body: { expectedVersion: 'revision-id' },
    });
  });
  it('preserves the editor on a stale-version error and refreshes metadata', async () => {
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Manage photo' }));
    await userEvent.upload(
      await screen.findByLabelText('JPEG or PNG photograph'),
      new File(['jpeg'], 'photo.jpg', { type: 'image/jpeg' }),
    );
    request.mockImplementation(async (_path, options) => {
      if (options?.method === 'PUT') throw new ApiError('conflict', 409);
      return metadata;
    });
    await userEvent.click(screen.getByRole('button', { name: 'Upload photo' }));
    expect(
      await screen.findByText('This item has changed. Refresh before continuing.'),
    ).toBeVisible();
    expect(screen.getByRole('dialog')).toBeVisible();
  });
  it('fails closed when metadata is unavailable and restores focus on Escape', async () => {
    request.mockRejectedValue(new ApiError('server', 503));
    setup();
    const opener = screen.getByRole('button', { name: 'Manage photo' });
    await userEvent.click(opener);
    expect(await screen.findByText(/Photo management is unavailable/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Upload photo' })).toBeDisabled();
    await userEvent.keyboard('{Escape}');
    expect(opener).toHaveFocus();
  });
});
