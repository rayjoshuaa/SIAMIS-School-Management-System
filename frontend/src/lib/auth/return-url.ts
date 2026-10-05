import { routes } from '../../app/router/navigation';
export function safeReturnUrl(value: string | null | undefined) {
  // Exact registered destinations only: no external URL, protocol-relative URL, query or credential link.
  return routes.some((route) => route.path === value) ? value! : '/';
}
