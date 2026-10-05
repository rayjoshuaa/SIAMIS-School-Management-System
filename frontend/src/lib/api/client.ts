import { ApiError, normalizeProblem } from './errors';
import { sessionEpoch, reportSessionLoss } from '../auth/session-events';

type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  signal?: AbortSignal;
  response?: 'json' | 'blob';
};
export function createApiClient(baseUrl = '') {
  const base = baseUrl.replace(/\/$/, '');
  if (base && !/^https?:\/\//.test(base))
    throw new Error('API base URL must be an absolute HTTP(S) URL or empty.');
  function url(path: string) {
    if (!path.startsWith('/api/') || path.includes('..') || path.includes('\\'))
      throw new Error('Use an API-root-relative path.');
    return `${base}${path}`;
  }
  async function readJson(response: Response, signal?: AbortSignal | null): Promise<unknown> {
    try {
      return await response.json();
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new ApiError('request', response.status);
    }
  }
  async function send(path: string, init: RequestInit) {
    const started = sessionEpoch();
    let response: Response;
    try {
      response = await fetch(url(path), {
        ...init,
        credentials: 'include',
        cache: 'no-store',
        redirect: 'error',
      });
    } catch (error) {
      if (init.signal?.aborted) throw error;
      throw new ApiError('network', 0);
    }
    if (!response.ok) {
      if (
        response.status === 401 &&
        ![
          '/api/auth/login',
          '/api/auth/activate',
          '/api/auth/reset-password',
          '/api/auth/forgot-password',
          '/api/auth/csrf',
        ].includes(path)
      )
        reportSessionLoss(started);
      const body: unknown = await readJson(response, init.signal).catch((error: unknown) => {
        if (init.signal?.aborted) throw error;
        return null;
      });
      throw normalizeProblem(response.status, body);
    }
    return response;
  }
  return async function request<T = unknown>(
    path: string,
    options: RequestOptions = {},
  ): Promise<T> {
    url(path); // Validate the destination before even requesting a CSRF token.
    const method = options.method ?? 'GET';
    const headers = new Headers({
      Accept: options.response === 'blob' ? '*/*' : 'application/json',
    });
    if (method !== 'GET') {
      // Fresh token per command prevents token reuse across login/logout identity changes.
      const response = await send('/api/auth/csrf', { signal: options.signal });
      const csrf: unknown = await readJson(response, options.signal);
      if (
        !csrf ||
        typeof csrf !== 'object' ||
        !('token' in csrf) ||
        typeof csrf.token !== 'string' ||
        !csrf.token
      )
        throw new ApiError('request', 0);
      headers.set('X-CSRF-TOKEN', csrf.token);
    }
    let body: BodyInit | undefined;
    if (options.body instanceof FormData) body = options.body;
    else if (options.body !== undefined) {
      headers.set('Content-Type', 'application/json');
      body = JSON.stringify(options.body);
    }
    const response = await send(path, { method, headers, body, signal: options.signal });
    if (response.status === 204) return undefined as T;
    return (
      options.response === 'blob' ? await response.blob() : await readJson(response, options.signal)
    ) as T;
  };
}
export const api = createApiClient(import.meta.env.VITE_API_BASE_URL || '');
