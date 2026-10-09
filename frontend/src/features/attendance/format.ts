export const payrollBoundary =
  'Attendance findings are factual review information. They do not authorize absence, establish misconduct or create payroll deductions.';
export function duration(value: number | null | undefined) {
  return value == null
    ? 'Unavailable'
    : Number.isSafeInteger(value)
      ? `${value.toLocaleString('en-US')} ms`
      : 'Outside supported display precision';
}

// Display only: API values and exact source timestamps are never rewritten.
const labels: Record<string, string> = {
  Live: 'Live day',
  UnfinalizedPastDay: 'Awaiting finalization',
  Stale: 'Sources changed',
  SnapshotInvalid: 'Snapshot needs review',
  ConfigurationRequired: 'Configuration required',
  WorkCalendarNotConfigured: 'Work calendar required',
  RequiresReview: 'Requires review',
  MissingClockOut: 'Missing clock-out',
  Unknown: 'Not determined',
};
export function attendanceLabel(value: string | null | undefined) {
  if (!value) return 'Not recorded';
  return labels[value] ?? value.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
}
