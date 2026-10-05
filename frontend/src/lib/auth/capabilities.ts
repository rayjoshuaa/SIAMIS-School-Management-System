export type CapabilitySession = {
  userId: string;
  employeeId?: string | null;
  capabilities: readonly string[];
};
export function can(session: CapabilitySession | null | undefined, capability: string): boolean {
  return session?.capabilities.includes(capability) ?? false;
}
// This helper controls presentation only. Every operation remains server-authorized.
