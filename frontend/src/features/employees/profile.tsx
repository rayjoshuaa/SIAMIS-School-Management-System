import { useParams, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../lib/auth/auth-context';
import { Table, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { Tabs } from '../../components/ui/navigation';
import { TableViewport } from '../../components/shared/workspace';
import { Badge } from '../../components/ui/feedback';
import { Pencil, CalendarRange, UserRoundMinus, UserRoundPlus } from 'lucide-react';
import { BackLink, LinkButton } from '../../components/ui/button';
import { employeeName, employmentContext } from './contracts';
import { masterLabel, useEmployee, useHistory, useMasters } from './data';
import { EmploymentSummary, Facts, QueryState } from './presentation';
import './employees.css';
import { AccountAccess } from './account-access';
import { SupportingInformation } from './supporting-information';
import { EmployeeAvatar, EmployeePhotoControl } from './photo';
import { PermanentDeletion } from './permanent-deletion';

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
    <div className="employee-workspace employee-profile">
      <BackLink to="/hr/employees">Employee directory</BackLink>
      {params.get('notice') === 'saved' && <p role="status">Employee saved successfully.</p>}
      {params.get('notice') === 'created' && (
        <p role="status">
          Employee registered successfully. Permanent employee number: {value.employeeNumber}.
        </p>
      )}
      <header className="employee-toolbar employee-profile-header">
        <div className="employee-identity">
          <span aria-hidden="true">
            <EmployeeAvatar id={value.employeeId} name={employeeName(value)} />
          </span>
          <div>
            <h2 className="text-lg font-semibold">{employeeName(value)}</h2>
            <p className="text-sm text-muted-foreground">
              {value.employeeNumber}
              {value.preferredName ? ` · ${value.preferredName}` : ''}
            </p>
            <div className="employee-identity-status">
              <Badge intent={value.isActive ? 'success' : 'neutral'}>
                Record {value.isActive ? 'active' : 'inactive'}
              </Badge>
              <span className="employee-context">Employment: {employmentContext(value)}</span>
            </div>
          </div>
        </div>
        {manage && (
          <div className="ui-record-actions">
            <EmployeePhotoControl
              id={value.employeeId}
              name={employeeName(value)}
              manage={!!manage}
            />
            <LinkButton to="edit">
              <Pencil aria-hidden="true" className="size-4" />
              Edit profile
            </LinkButton>
            {value.currentEmployment && value.isActive ? (
              <>
                <LinkButton to="employment-change">
                  <CalendarRange aria-hidden="true" />
                  Record employment change
                </LinkButton>
                <LinkButton variant="destructive-outline" to="end-employment">
                  <UserRoundMinus aria-hidden="true" />
                  End employment
                </LinkButton>
              </>
            ) : (
              !value.isActive &&
              !value.currentEmployment && (
                <LinkButton to="rehire">
                  <UserRoundPlus aria-hidden="true" />
                  Rehire
                </LinkButton>
              )
            )}
          </div>
        )}
      </header>
      <Tabs
        label="Employee 360 sections"
        overflow="scroll"
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
            content: <SupportingInformation employee={value} manage={!!manage} />,
          },
          {
            value: 'employment',
            label: 'Employment history',
            content: (
              <>
                <div className="employee-history-heading">
                  <h3>Employment records</h3>
                  <p>Current and historical periods are retained. Dates are shown as recorded.</p>
                </div>
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
                              <TableCell>
                                <Badge intent={record.isCurrent ? 'info' : 'neutral'}>
                                  {record.isCurrent ? 'Current' : 'Historical'}
                                </Badge>
                              </TableCell>
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
      <PermanentDeletion id={value.employeeId} number={value.employeeNumber} />
    </div>
  );
}
