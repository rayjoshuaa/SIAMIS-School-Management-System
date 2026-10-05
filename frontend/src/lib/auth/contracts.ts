export type SessionUser = {
  userId: string;
  userName: string;
  employeeId: string | null;
  isActive: boolean;
  requiresPasswordChange: boolean;
  roles: string[];
  capabilities: string[];
};
export function readSession(value: unknown): SessionUser {
  if (!value || typeof value !== 'object') throw new Error('Invalid session response');
  const v = value as Record<string, unknown>;
  if (
    typeof v.userId !== 'string' ||
    typeof v.userName !== 'string' ||
    typeof v.isActive !== 'boolean' ||
    typeof v.requiresPasswordChange !== 'boolean' ||
    !Array.isArray(v.roles) ||
    !v.roles.every((x) => typeof x === 'string') ||
    !Array.isArray(v.capabilities) ||
    !v.capabilities.every((x) => typeof x === 'string') ||
    !(v.employeeId === null || typeof v.employeeId === 'string')
  )
    throw new Error('Invalid session response');
  return {
    userId: v.userId,
    userName: v.userName,
    employeeId: v.employeeId,
    isActive: v.isActive,
    requiresPasswordChange: v.requiresPasswordChange,
    roles: v.roles,
    capabilities: v.isActive && !v.requiresPasswordChange ? v.capabilities : [],
  };
}
