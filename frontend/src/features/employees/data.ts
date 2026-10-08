import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import {
  masterNames,
  type Employee,
  type Employment,
  type Masters,
  type Master,
} from './contracts';

export function useEmployee(id?: string) {
  return useQuery({
    queryKey: ['employees', 'detail', id],
    enabled: !!id,
    queryFn: ({ signal }) => api<Employee>(`/api/employees/${id}`, { signal }),
  });
}
export function useHistory(id?: string) {
  return useQuery({
    queryKey: ['employees', 'history', id],
    enabled: !!id,
    queryFn: ({ signal }) =>
      api<Employment[]>(`/api/employees/${id}/employment-history`, { signal }),
  });
}
export function useMasters() {
  return useQuery({
    queryKey: ['employee-masters', 'including-inactive'],
    queryFn: async ({ signal }) =>
      Object.fromEntries(
        await Promise.all(
          masterNames.map(async (name) => [
            name,
            await api<Master[]>(`/api/master-data/${name}?includeInactive=true`, { signal }),
          ]),
        ),
      ) as Masters,
  });
}
export function masterLabel(masters: Masters | undefined, name: keyof Masters, id: string | null) {
  return id
    ? (masters?.[name].find((item) => item.id === id)?.name ?? `Reference ${id}`)
    : 'Not recorded';
}
// PUT replaces personal scalar fields. Never guess an ID from an ambiguous display name.
export function resolveMasterId(items: Master[], name: string | null): string {
  if (!name) return '';
  const matches = items.filter((item) => item.name === name);
  if (matches.length !== 1)
    throw new Error(
      'Existing personal information cannot be safely resolved. No changes were submitted.',
    );
  return matches[0].id;
}
