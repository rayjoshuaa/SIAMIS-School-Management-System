// Read-only projections of the frozen HR response contracts; no EF entities or financial data.
export type Page<T> = { items: T[]; page: number; pageSize: number; totalCount: number };
export type AttendanceCounts = {
  effectiveEmployees: number;
  scheduled: number;
  notScheduled: number;
  late: number;
  approvedLeave: number;
  requiresReview: number;
  stale: number;
  configurationRequired: number;
  readyToFinalize: number;
};
export type AttendanceOverview = {
  businessDate: string;
  businessTimeZone: string;
  generatedAtUtc: string;
  populationCount: number;
  counts: AttendanceCounts;
};
export type PendingLeave = {
  leaveId: string;
  employeeName: string;
  employeeNumber: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  status: string;
};
