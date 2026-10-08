import { Link, useParams, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../lib/auth/auth-context';
import { Table, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { Tabs } from '../../components/ui/navigation';
import { TableViewport } from '../../components/shared/workspace';
import { employeeName } from './contracts';
import { masterLabel, useEmployee, useHistory, useMasters } from './data';
import { ContactSummary, EmploymentSummary, Facts, QueryState } from './presentation';
import './employees.css';
import { AccountAccess } from './account-access';

export function EmployeeProfile() {
  const { employeeId } = useParams();
  const [params] = useSearchParams();
  const { state } = useAuth();
  const employee = useEmployee(employeeId);
  const history = useHistory(employeeId);
  const masters = useMasters();
  if (!employee.data || employee.isError)
    return (
      <QueryState
        loading={employee.isPending}
        error={employee.error}
        retry={() => void employee.refetch()}
      />
    );
  const value = employee.data;
  const manage = state.user?.capabilities.includes('Employee.Manage');
  return (
    <div className="employee-workspace">
      <Link className="ui-link" to="/hr/employees">
        ← Employee directory
      </Link>
      {params.get('notice') === 'saved' && <p role="status">Employee saved successfully.</p>}
      <header className="employee-toolbar">
        <div>
          <h2 className="text-lg font-semibold">{employeeName(value)}</h2>
          <p className="text-sm text-muted-foreground">
            {value.employeeNumber}
            {value.preferredName ? ` · ${value.preferredName}` : ''}
          </p>
        </div>
        {manage && (
          <div className="flex flex-wrap gap-3">
            <Link className="ui-link inline-flex min-h-11 items-center" to="edit">
              Edit profile
            </Link>
            {value.currentEmployment && value.isActive ? (
              <>
                <Link className="ui-link inline-flex min-h-11 items-center" to="employment-change">
                  Record employment change
                </Link>
                <Link className="ui-link inline-flex min-h-11 items-center" to="end-employment">
                  End employment
                </Link>
              </>
            ) : (
              !value.isActive &&
              !value.currentEmployment && (
                <Link className="ui-link inline-flex min-h-11 items-center" to="rehire">
                  Rehire
                </Link>
              )
            )}
          </div>
        )}
      </header>
      <Tabs
        label="Employee 360 sections"
        tabs={[
          { value: 'overview', label: 'Overview', content: <EmploymentSummary employee={value} /> },
          {
            value: 'account',
            label: 'Account access',
            content: <AccountAccess employee={value} />,
          },
          {
            value: 'personal',
            label: 'Personal & contacts',
            content: (
              <div className="space-y-6">
                <Facts
                  items={[
                    ['Date of birth', value.dateOfBirth],
                    ['Gender', value.gender],
                    ['Marital status', value.maritalStatus],
                    ['Nationality', value.nationality],
                  ]}
                />
                <section className="employee-section">
                  <h2>Contact information</h2>
                  <ContactSummary employee={value} />
                </section>
                <section className="employee-section">
                  <h2>Addresses</h2>
                  {value.addresses.length ? (
                    value.addresses.map((address) => (
                      <div className="mb-4 text-sm" key={address.employeeAddressId}>
                        <h3 className="font-semibold">
                          {address.addressType}
                          {address.isPrimary ? ' · Primary' : ''}
                        </h3>
                        <p className="break-words">
                          {[
                            address.addressLine1,
                            address.addressLine2,
                            address.city,
                            address.stateProvince,
                            address.postalCode,
                            address.country,
                          ]
                            .filter(Boolean)
                            .join(', ')}
                        </p>
                      </div>
                    ))
                  ) : (
                    <p className="text-sm text-muted-foreground">No addresses recorded.</p>
                  )}
                </section>
                <section className="employee-section">
                  <h2>Emergency contacts</h2>
                  {value.emergencyContacts.length ? (
                    value.emergencyContacts.map((contact) => (
                      <div className="mb-4" key={contact.emergencyContactId}>
                        <h3 className="mb-2 text-sm font-semibold">
                          {contact.name}
                          {contact.isPrimary ? ' · Primary' : ''}
                        </h3>
                        <Facts
                          items={[
                            ['Relationship', contact.relationship],
                            ['Mobile', contact.mobile],
                            ['Phone', contact.phone],
                            ['Email', contact.email],
                          ]}
                        />
                      </div>
                    ))
                  ) : (
                    <p className="text-sm text-muted-foreground">No emergency contacts recorded.</p>
                  )}
                </section>
              </div>
            ),
          },
          {
            value: 'employment',
            label: 'Employment history',
            content: (
              <>
                <QueryState
                  loading={history.isPending}
                  error={history.error}
                  retry={() => void history.refetch()}
                />
                {masters.isError && (
                  <QueryState
                    loading={false}
                    error={masters.error}
                    retry={() => void masters.refetch()}
                  />
                )}
                {!history.isError &&
                  history.data &&
                  (history.data.length ? (
                    <TableViewport label="Employment history">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            {['Period', 'Department', 'Designation', 'Type / status', 'Record'].map(
                              (label) => (
                                <TableHead key={label}>{label}</TableHead>
                              ),
                            )}
                          </TableRow>
                        </TableHeader>
                        <tbody>
                          {history.data.map((record) => (
                            <TableRow key={record.employmentRecordId}>
                              <TableCell>
                                {record.startDate ?? record.hireDate} →{' '}
                                {record.endDate ?? 'Open-ended'}
                              </TableCell>
                              <TableCell>
                                {masterLabel(masters.data, 'departments', record.departmentId)}
                              </TableCell>
                              <TableCell>
                                {masterLabel(masters.data, 'designations', record.designationId)}
                              </TableCell>
                              <TableCell>
                                {masterLabel(
                                  masters.data,
                                  'employment-types',
                                  record.employmentTypeId,
                                )}
                                <div>
                                  {masterLabel(
                                    masters.data,
                                    'employment-statuses',
                                    record.employmentStatusId,
                                  )}
                                </div>
                              </TableCell>
                              <TableCell>{record.isCurrent ? 'Current' : 'Historical'}</TableCell>
                            </TableRow>
                          ))}
                        </tbody>
                      </Table>
                    </TableViewport>
                  ) : (
                    <p className="text-sm text-muted-foreground">No employment records recorded.</p>
                  ))}
              </>
            ),
          },
          ...(value.teacherProfile
            ? [
                {
                  value: 'teaching',
                  label: 'Teacher profile',
                  content: (
                    <Facts
                      items={[
                        ['Teacher code', value.teacherProfile.teacherCode],
                        ['Teaching level', value.teacherProfile.teachingLevel],
                        ['Specialization', value.teacherProfile.specialization],
                        ['Years of experience', value.teacherProfile.yearsOfExperience],
                        ['Teaching status', value.teacherProfile.teachingStatus],
                      ]}
                    />
                  ),
                },
              ]
            : []),
        ]}
      />
    </div>
  );
}
