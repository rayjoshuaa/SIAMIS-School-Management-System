const bangkok = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  year: 'numeric',
  month: 'short',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
});
export function clockTime(value: string) {
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? bangkok.format(date) : 'Timestamp unavailable';
}
// Preserve datetime2(7) precision when deriving a completed session's elapsed duration.
// This is observation duration, never approved attendance or payroll time.
function ticks(value: string) {
  const match = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?Z$/.exec(value);
  if (!match) return null;
  const seconds = Date.parse(`${match[1]}Z`);
  return Number.isFinite(seconds)
    ? BigInt(seconds) * 10000n + BigInt((match[2] ?? '').padEnd(7, '0'))
    : null;
}
export function sessionDuration(start: string, end: string | null) {
  if (!end) return 'Open — no end time';
  const a = ticks(start),
    b = ticks(end);
  if (a === null || b === null || b < a) return 'Unavailable';
  const seconds = (b - a) / 10000000n;
  if (seconds === 0n) return 'Less than 1 second';
  return `${seconds / 3600n}h ${(seconds % 3600n) / 60n}m ${seconds % 60n}s`;
}
export function validDate(value: string) {
  return (
    !value ||
    (/^\d{4}-\d{2}-\d{2}$/.test(value) &&
      Number.isFinite(Date.parse(`${value}T00:00:00Z`)) &&
      new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value)
  );
}
