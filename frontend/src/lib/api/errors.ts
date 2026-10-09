export type ErrorKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'conflict'
  | 'server'
  | 'network'
  | 'request';
const messages: Record<ErrorKind, string> = {
  validation: 'Check the highlighted fields and try again.',
  unauthorized: 'Sign in to continue.',
  forbidden: 'You do not have access to this action.',
  'not-found': 'This item could not be found.',
  conflict: 'This item has changed. Refresh before continuing.',
  server: 'The service could not complete your request. Try again later.',
  network: 'Unable to reach the service. Check your connection.',
  request: 'The request could not be completed.',
};
export class ApiError extends Error {
  constructor(
    public readonly kind: ErrorKind,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
    public readonly code?:
      'idempotency_conflict' | 'registration_in_progress' | 'registration_key_expired',
  ) {
    super(messages[kind]);
    this.name = 'ApiError';
  }
}
export function normalizeProblem(status: number, body: unknown): ApiError {
  const kind: ErrorKind =
    status === 400
      ? 'validation'
      : status === 401
        ? 'unauthorized'
        : status === 403
          ? 'forbidden'
          : status === 404
            ? 'not-found'
            : status === 409
              ? 'conflict'
              : status >= 500
                ? 'server'
                : 'request';
  const fields = Object.create(null) as Record<string, string[]>;
  if (
    status === 400 &&
    body &&
    typeof body === 'object' &&
    'errors' in body &&
    body.errors &&
    typeof body.errors === 'object'
  ) {
    for (const [key, value] of Object.entries(body.errors)) {
      if (Array.isArray(value) && value.every((item) => typeof item === 'string'))
        fields[key] = value.map((item: string) => item.slice(0, 300));
    }
  }
  // Do not propagate arbitrary detail, stack traces or server extension fields to the UI.
  const code =
    body &&
    typeof body === 'object' &&
    'code' in body &&
    (body.code === 'idempotency_conflict' ||
      body.code === 'registration_in_progress' ||
      body.code === 'registration_key_expired')
      ? body.code
      : undefined;
  return new ApiError(kind, status, fields, code);
}
