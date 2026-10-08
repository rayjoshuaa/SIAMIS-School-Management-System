// Assignment choices mirror the existing permanent backend role contract.
// Authorization uses capabilities, never these role names.
export const permanentRoles = [
  'SystemAdmin',
  'HRAdmin',
  'PayrollAdmin',
  'Management',
  'Employee',
] as const;
export type Account = {
  userId: string;
  userName: string;
  email: string | null;
  employeeId: string | null;
  isActive: boolean;
  requiresPasswordChange: boolean;
  credentialEstablished: boolean;
  emailConfirmed: boolean;
  isLockedOut: boolean;
  roles: string[];
  version: string;
};
