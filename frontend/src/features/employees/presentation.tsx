import { DetailFacts as Facts } from '../../components/shared/detail-facts';
export { DetailFacts as Facts } from '../../components/shared/detail-facts';
import { Button } from '../../components/ui/button';
import { SystemState } from '../../components/shared/workspace';
import { ApiError } from '../../lib/api/errors';
import type { Employee } from './contracts';
import { employmentContext } from './contracts';
import type { ReactNode } from 'react';

export function QueryState({
  loading,
  error,
  retry,
}: {
  loading: boolean;
  error: Error | null;
  retry: () => void;
}) {
  if (loading)
    return (
      <p role="status" className="py-6 text-sm text-muted-foreground">
        Loading employee information…
      </p>
    );
  if (!error) return null;
  return (
    <SystemState
      kind={error instanceof ApiError && error.status === 403 ? 'permission' : 'error'}
      title="Employee information unavailable"
      action={
        <Button variant="outline" onClick={retry}>
          Retry
        </Button>
      }
    >
      {error instanceof ApiError ? error.message : 'Unable to load this information. Try again.'}
    </SystemState>
  );
}
export function EmploymentSummary({ employee }: { employee: Employee }) {
  const current = employee.currentEmployment;
  return (
    <div className="employee-summary">
      <section aria-label="Employment information">
        <h3>Employment information</h3>
        <Facts
          items={[
            ['Employee record', employee.isActive ? 'Active' : 'Inactive'],
            ['Employment context', employmentContext(employee)],
            ['Employment record status', current?.employmentStatus],
            ['Department', current?.department],
            ['Designation', current?.designation],
            ['Employment type', current?.employmentType],
            ['Location', current?.location],
          ]}
        />
      </section>
      <section aria-label="Dates and reporting">
        <h3>Dates &amp; reporting</h3>
        <Facts
          items={[
            ['Hire date', current?.hireDate],
            ['Start date', current?.startDate],
            ['Reports to', current?.reportingToName],
          ]}
        />
      </section>
    </div>
  );
}
export function ContactSummary({
  employee,
  actions,
}: {
  employee: Employee;
  actions?: (contact: Employee['contacts'][number]) => ReactNode;
}) {
  return employee.contacts.length ? (
    <div className="space-y-4">
      {employee.contacts.map((contact) => (
        <section key={contact.employeeContactId}>
          <div className="employee-toolbar mb-2">
            <h3 className="ui-subsection-title">
              {contact.isPrimary ? 'Primary contact' : 'Contact'}
            </h3>
            {actions?.(contact)}
          </div>
          <Facts
            items={[
              ['Work email', contact.workEmail],
              ['Personal email', contact.personalEmail],
              ['Mobile', contact.mobile],
              ['Phone', contact.phone],
              ['Work phone', contact.workPhone],
            ]}
          />
        </section>
      ))}
    </div>
  ) : (
    <p className="text-sm text-muted-foreground">No contact information recorded.</p>
  );
}
