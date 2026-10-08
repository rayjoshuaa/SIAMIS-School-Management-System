import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { Input } from '../../components/ui/controls';
import { Button } from '../../components/ui/button';
import { employeeName, type EmployeePage } from './contracts';
import { QueryState } from './presentation';

export function ReportingEmployee({
  value,
  onChange,
  employeeId,
  disabled,
  retainCurrent,
  ...props
}: {
  value: string;
  onChange: (value: string) => void;
  employeeId?: string;
  disabled: boolean;
  retainCurrent: boolean;
  id: string;
  'aria-describedby'?: string;
  'aria-invalid': boolean;
}) {
  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [selectedLabel, setSelectedLabel] = useState('Selected reporting employee');
  const query = useQuery({
    queryKey: ['employees', 'manager-search', submitted],
    enabled: !!submitted,
    queryFn: ({ signal }) =>
      api<EmployeePage>(
        `/api/employees?${new URLSearchParams({ page: '1', pageSize: '20', search: submitted, isActive: 'true' })}`,
        { signal },
      ),
  });
  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        <Input
          aria-label="Find reporting employee"
          placeholder="Search active employees"
          value={search}
          disabled={disabled}
          maxLength={100}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Button
          type="button"
          variant="outline"
          disabled={disabled || !search.trim()}
          onClick={() => setSubmitted(search.trim())}
        >
          Find
        </Button>
      </div>
      <select
        {...props}
        className="ui-control employee-native-select"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          setSelectedLabel(event.target.selectedOptions[0].text);
          onChange(event.target.value);
        }}
      >
        <option value="">
          {retainCurrent ? 'Keep current reporting employee' : 'No reporting employee'}
        </option>
        {value && !query.data?.items.some((employee) => employee.employeeId === value) && (
          <option value={value}>{selectedLabel}</option>
        )}
        {query.data?.items
          .filter((employee) => employee.employeeId !== employeeId)
          .map((employee) => (
            <option key={employee.employeeId} value={employee.employeeId}>
              {employeeName(employee)} · {employee.employeeNumber}
            </option>
          ))}
      </select>
      {submitted && (
        <>
          <QueryState
            loading={query.isPending}
            error={query.error}
            retry={() => void query.refetch()}
          />
          {query.data && (
            <p className="text-sm text-muted-foreground">
              {query.data.totalCount
                ? `Showing up to 20 matches. Refine the search if needed.`
                : 'No active employees match this search.'}
            </p>
          )}
        </>
      )}
    </div>
  );
}
