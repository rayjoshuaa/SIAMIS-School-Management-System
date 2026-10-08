export const payrollBoundary =
  'Attendance findings are factual review information. They do not authorize absence, establish misconduct or create payroll deductions.';
export function duration(value: number | null | undefined) {
  return value == null
    ? 'Unavailable'
    : Number.isSafeInteger(value)
      ? `${value.toLocaleString('en-US')} ms`
      : 'Outside supported display precision';
}
