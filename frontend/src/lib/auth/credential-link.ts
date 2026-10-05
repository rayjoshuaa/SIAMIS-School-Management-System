type CredentialLink = { userId: string; token: string };
let pending: CredentialLink | null = null;
export function captureCredentialLink() {
  if (!['/activate', '/reset-password'].includes(window.location.pathname)) return;
  const params = new URLSearchParams(window.location.search);
  const userId = params.get('userId') ?? '';
  const token = params.get('token') ?? '';
  if (
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(userId) &&
    token &&
    token.length <= 4096
  )
    pending = { userId, token };
  // Remove sensitive parameters before rendering or issuing any API request. Tokens live in RAM only.
  window.history.replaceState(null, '', window.location.pathname);
}
export function readCredentialLink() {
  return pending;
}
export function clearCredentialLink() {
  pending = null;
}
