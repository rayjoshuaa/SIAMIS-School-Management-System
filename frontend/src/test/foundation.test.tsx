import { describe, expect, it, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button } from '../components/ui/button';
import { Notifications } from '../app/providers/notifications';
import { DemoForm } from '../features/design-system/demo-form';
import { normalizeProblem, ApiError } from '../lib/api/errors';
import { createApiClient } from '../lib/api/client';
import { can } from '../lib/auth/capabilities';
import { formatDateOnly, formatInstant } from '../lib/utils/format';
import { createQueryClient } from '../app/providers/query-client';

afterEach(() => vi.unstubAllGlobals());
describe('UI foundations', () => {
  it('uses a semantic disabled button and prevents interaction', async () => {
    const click = vi.fn();
    render(
      <Button disabled onClick={click}>
        Save example
      </Button>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Save example' }));
    expect(click).not.toHaveBeenCalled();
    expect(screen.getByRole('button')).toBeDisabled();
  });
  it('announces loading and disables duplicate submission', () => {
    render(<Button loading>Submitting</Button>);
    expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByRole('button')).toBeDisabled();
  });
  it('associates validation errors with their field', async () => {
    render(
      <Notifications>
        <DemoForm />
      </Notifications>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Validate sample' }));
    expect(await screen.findByText('Enter a display name.')).toHaveAttribute('role', 'alert');
    expect(screen.getByLabelText(/Display name/)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText(/Display name/)).toHaveAttribute(
      'aria-describedby',
      'display-name-hint display-name-error',
    );
  });
});
describe('API security and errors', () => {
  it('hides malformed successful JSON diagnostics', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<private server response>')));
    await expect(createApiClient()('/api/sample')).rejects.toEqual(new ApiError('request', 200));
  });
  it('rejects a non-API destination before fetching credentials or a token', async () => {
    const fetcher = vi.fn();
    vi.stubGlobal('fetch', fetcher);
    await expect(
      createApiClient()('https://external.example/api/sample', { method: 'POST' }),
    ).rejects.toThrow('API-root-relative');
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('does not expose arbitrary server diagnostics', () => {
    const error = normalizeProblem(500, {
      title: 'Sensitive stack',
      detail: 'private path',
      stackTrace: 'secret',
    });
    expect(error.kind).toBe('server');
    expect(JSON.stringify(error)).not.toContain('secret');
    expect(error.message).not.toContain('Sensitive');
  });
  it.each([
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'not-found'],
    [409, 'conflict'],
  ])('normalizes HTTP %s', (status, kind) => {
    expect(normalizeProblem(Number(status), null).kind).toBe(kind);
  });
  it('keeps validation messages and ignores malformed errors', () => {
    expect(
      normalizeProblem(400, { errors: { name: ['Required'], malformed: 42 } }).fieldErrors,
    ).toEqual({ name: ['Required'] });
  });
  it('sends credentials and a fresh CSRF token on every mutation', async () => {
    const fetcher = vi
      .fn()
      .mockImplementation(async (url: string) =>
        url.endsWith('/csrf')
          ? new Response(JSON.stringify({ token: 'sample-csrf' }), { status: 200 })
          : new Response(null, { status: 204 }),
      );
    vi.stubGlobal('fetch', fetcher);
    const request = createApiClient();
    await request('/api/sample', { method: 'POST', body: { sample: true } });
    await request('/api/sample', { method: 'DELETE' });
    expect(fetcher).toHaveBeenCalledTimes(4);
    const options = fetcher.mock.calls[1][1] as RequestInit;
    expect(options.credentials).toBe('include');
    expect(new Headers(options.headers).get('X-CSRF-TOKEN')).toBe('sample-csrf');
  });
  it('lets the browser set multipart boundaries and supports binary data', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(new Response('{"token":"sample"}'))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response('bytes'));
    vi.stubGlobal('fetch', fetcher);
    const request = createApiClient();
    const body = new FormData();
    body.append('name', 'sample');
    await request('/api/sample', { method: 'POST', body });
    expect(new Headers(fetcher.mock.calls[1][1].headers).has('Content-Type')).toBe(false);
    const blob = await request<Blob>('/api/sample', { response: 'blob' });
    expect(blob.size).toBe(5);
  });
  it('maps network failures but preserves cancellation', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('private network detail')));
    await expect(createApiClient()('/api/sample')).rejects.toEqual(new ApiError('network', 0));
    const controller = new AbortController();
    controller.abort();
    await expect(
      createApiClient()('/api/sample', { signal: controller.signal }),
    ).rejects.toBeInstanceOf(TypeError);
  });
  it('uses capabilities, never inferred roles', () => {
    expect(can(null, 'Payroll.Read')).toBe(false);
    expect(can({ userId: 'sample', capabilities: ['Employee.Read'] }, 'Payroll.Read')).toBe(false);
    expect(can({ userId: 'sample', capabilities: ['Employee.Read'] }, 'Employee.Read')).toBe(true);
  });
  it('does not retry mutations or HTTP authorization failures', () => {
    const client = createQueryClient();
    const defaults = client.getDefaultOptions();
    expect(defaults.mutations?.retry).toBe(false);
    const retry = defaults.queries?.retry;
    expect(typeof retry).toBe('function');
    if (typeof retry === 'function')
      expect(retry(0, new ApiError('unauthorized', 401))).toBe(false);
  });
});
describe('date display contracts', () => {
  it('preserves date-only calendar values', () => {
    expect(formatDateOnly('2026-01-01')).toBe('01 Jan 2026');
  });
  it('rejects invalid dates and datetime values in date-only formatting', () => {
    expect(() => formatDateOnly('2026-02-30')).toThrow();
    expect(() => formatDateOnly('2026-01-01T00:00:00Z')).toThrow();
  });
  it('converts explicit UTC instants to Bangkok', () => {
    expect(formatInstant('2026-01-01T23:30:00Z')).toContain('2 Jan 2026');
  });
  it('rejects ambiguous offsetless instants', () => {
    expect(() => formatInstant('2026-01-01T12:00:00')).toThrow();
  });
});
