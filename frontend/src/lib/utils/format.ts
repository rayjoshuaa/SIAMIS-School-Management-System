const locale = 'en-GB';
export const schoolTimeZone = 'Asia/Bangkok';
export function formatDateOnly(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) throw new RangeError('Expected an ISO date-only value.');
  const [, year, month, day] = match;
  const date = new Date(`${year}-${month}-${day}T00:00:00Z`);
  if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== value)
    throw new RangeError('Invalid calendar date.');
  // UTC here is a formatting carrier; date-only values never enter local instant conversion.
  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    timeZone: 'UTC',
  }).format(date);
}
export function formatInstant(value: string, timeZone = schoolTimeZone): string {
  if (!/(Z|[+-]\d{2}:\d{2})$/.test(value))
    throw new RangeError('An instant must include an offset.');
  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone,
  }).format(new Date(value));
}
export const formatInteger = (value: number) =>
  new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(value);
export const formatDecimal = (value: number) =>
  new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(value);
export const formatPercent = (fraction: number) =>
  new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 2 }).format(fraction);
export const formatThb = (value: number) =>
  new Intl.NumberFormat(locale, { style: 'currency', currency: 'THB' }).format(value);
