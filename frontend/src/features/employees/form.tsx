import { useEffect, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { useAuth } from '../../lib/auth/auth-context';
import { FormField } from '../../components/shared/form-field';
import { Input } from '../../components/ui/controls';
import { Button, BackLink, LinkButton } from '../../components/ui/button';
import { AlertDialog } from '../../components/ui/overlays';
import { SystemState } from '../../components/shared/workspace';
import {
  type AccountLifecycle,
  type Employee,
  type Employment,
  type Masters,
  type MasterName,
} from './contracts';
import { masterLabel, resolveMasterId, useEmployee, useHistory, useMasters } from './data';
import { Facts, QueryState } from './presentation';
import { ReportingEmployee } from './reporting-employee';
import './employees.css';

type Mode = 'create' | 'edit' | 'employment-change' | 'rehire' | 'end-employment';
type Values = Record<string, string>;
const titles: Record<Mode, string> = {
  create: 'Create employee',
  edit: 'Edit employee profile',
  'employment-change': 'Record employment change',
  rehire: 'Rehire employee',
  'end-employment': 'End employment',
};
const contextFields: [string, MasterName, string, boolean][] = [
  ['departmentId', 'departments', 'Department', true],
  ['designationId', 'designations', 'Designation', true],
  ['employmentTypeId', 'employment-types', 'Employment type', true],
  ['employmentStatusId', 'employment-statuses', 'Employment status', true],
  ['locationId', 'locations', 'Location', false],
  ['hiringSourceId', 'hiring-sources', 'Hiring source', false],
];
export function EmployeeForm({ mode }: { mode: Mode }) {
  const { employeeId } = useParams();
  const { state } = useAuth();
  const allowed = state.user?.capabilities.includes('Employee.Manage') ?? false;
  const employee = useEmployee(allowed && mode !== 'create' ? employeeId : undefined);
  const history = useHistory(allowed && mode === 'edit' ? employeeId : undefined);
  const masters = useMasters();
  const account = useQuery({
    queryKey: ['employees', 'account-lifecycle', employeeId],
    enabled: allowed && mode === 'end-employment',
    queryFn: ({ signal }) =>
      api<AccountLifecycle>(`/api/employees/${employeeId}/account-lifecycle`, { signal }),
  });
  if (!allowed)
    return (
      <SystemState kind="permission" title="Employee management access required">
        You do not have access to this action.
      </SystemState>
    );
  const loading =
    masters.isPending ||
    (mode !== 'create' && employee.isPending) ||
    (mode === 'edit' && history.isPending) ||
    (mode === 'end-employment' && account.isPending);
  const error =
    masters.error ??
    (mode !== 'create' ? employee.error : null) ??
    (mode === 'edit' ? history.error : null) ??
    (mode === 'end-employment' ? account.error : null);
  if (loading || error)
    return (
      <QueryState
        loading={loading}
        error={error}
        retry={() => {
          void masters.refetch();
          if (mode !== 'create') void employee.refetch();
          if (mode === 'edit') void history.refetch();
          if (mode === 'end-employment') void account.refetch();
        }}
      />
    );
  if (!masters.data) return null;
  const record = employee.data?.currentEmployment ?? history.data?.at(-1);
  let initial: Values = {};
  try {
    if (mode === 'edit' && employee.data) {
      if (!record) throw new Error('Profile editing requires an existing employment record.');
      initial = {
        firstName: employee.data.firstName,
        lastName: employee.data.lastName,
        middleName: employee.data.middleName ?? '',
        preferredName: employee.data.preferredName ?? '',
        dateOfBirth: employee.data.dateOfBirth ?? '',
        genderId: resolveMasterId(masters.data.genders, employee.data.gender),
        maritalStatusId: resolveMasterId(
          masters.data['marital-statuses'],
          employee.data.maritalStatus,
        ),
        nationalityId: resolveMasterId(masters.data.nationalities, employee.data.nationality),
      };
    }
  } catch (failure) {
    return (
      <SystemState kind="configuration" title="Cannot safely edit this profile">
        {failure instanceof Error ? failure.message : 'Existing values could not be resolved.'}
      </SystemState>
    );
  }
  return (
    <EmployeeEditor
      key={`${mode}-${employeeId ?? 'new'}`}
      mode={mode}
      employee={employee.data}
      record={record}
      account={account.data}
      masters={masters.data}
      initial={initial}
      securityManage={state.user?.capabilities.includes('Security.Manage') ?? false}
    />
  );
}

function EmployeeEditor({
  mode,
  employee,
  record,
  account,
  masters,
  initial,
  securityManage,
}: {
  mode: Mode;
  employee?: Employee;
  record?: Employment;
  account?: AccountLifecycle;
  masters: Masters;
  initial: Values;
  securityManage: boolean;
}) {
  const navigate = useNavigate();
  const cache = useQueryClient();
  const saved = useRef(false);
  const submitting = useRef(false);
  const registration = useRef<{
    key: string;
    fingerprint: string;
    body: unknown;
    values: Values;
  } | null>(null);
  const [registrationState, setRegistrationState] = useState<'ready' | 'uncertain' | 'terminal'>(
    'ready',
  );
  const [general, setGeneral] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<Values | null>(null);
  const personalFields = [
    'firstName',
    'middleName',
    'lastName',
    'preferredName',
    'dateOfBirth',
    'genderId',
    'maritalStatusId',
    'nationalityId',
  ];
  const visibleContextFields = contextFields.filter(
    ([name]) => mode !== 'create' || name !== 'employmentStatusId',
  );
  const employmentFields = [...visibleContextFields.map(([name]) => name), 'reportingToEmployeeId'];
  const fields =
    mode === 'edit'
      ? personalFields
      : mode === 'end-employment'
        ? [
            'endDate',
            'employmentStatusId',
            ...(account?.requiresOffboardingDecision ? ['disableLinkedAccount'] : []),
          ]
        : [
            ...(mode === 'create' ? [...personalFields, 'initialWorkEmail', 'initialMobile'] : []),
            ...employmentFields,
            ...(mode === 'employment-change' ? ['effectiveDate'] : ['hireDate', 'startDate']),
          ];
  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isDirty },
    setError,
    getValues,
    clearErrors,
  } = useForm<Values>({
    defaultValues: Object.fromEntries(fields.map((name) => [name, initial[name] ?? ''])),
  });
  const back = employee ? `/hr/employees/${employee.employeeId}` : '/hr/employees';
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      !saved.current &&
      (isDirty || submitting.current) &&
      currentLocation.pathname !== nextLocation.pathname,
  );
  useEffect(() => {
    const prevent = (event: BeforeUnloadEvent) => {
      if ((isDirty || submitting.current) && !saved.current) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', prevent);
    return () => window.removeEventListener('beforeunload', prevent);
  }, [isDirty]);
  const mutation = useMutation({
    mutationFn: (values: Values) => {
      const nonempty = Object.fromEntries(
        Object.entries(values).map(([key, value]) => [key, (value ?? '').trim() || null]),
      );
      let body: Record<string, unknown> = nonempty;
      if (mode === 'create') {
        body = Object.fromEntries(
          Object.entries(nonempty).filter(([key]) => !key.startsWith('initial')),
        );
        if (nonempty.initialWorkEmail || nonempty.initialMobile)
          body.contacts = [
            {
              workEmail: nonempty.initialWorkEmail,
              mobile: nonempty.initialMobile,
              isPrimary: true,
            },
          ];
      }
      if (mode === 'edit' && employee && record) {
        body = {
          ...nonempty,
          ...Object.fromEntries(
            contextFields.map(([key]) => [key, record[key as keyof Employment]]),
          ),
          reportingToEmployeeId: record.reportingToEmployeeId,
          hireDate: record.hireDate,
          startDate: record.startDate,
          endDate: record.endDate,
        };
        // Child collections and TeacherProfile are intentionally omitted: existing data is retained by PUT.
      }
      if (mode === 'employment-change')
        body = Object.fromEntries(Object.entries(nonempty).filter(([, value]) => value !== null));
      if (mode === 'end-employment')
        body = {
          endDate: nonempty.endDate,
          employmentStatusId: nonempty.employmentStatusId,
          expectedEmploymentRecordId: record?.employmentRecordId,
          ...(account?.requiresOffboardingDecision
            ? {
                disableLinkedAccount: values.disableLinkedAccount === 'true',
                expectedLinkedAccountVersion: account.linkedAccountVersion,
              }
            : {}),
        };
      const endpoint =
        mode === 'create'
          ? '/api/employees'
          : `/api/employees/${employee!.employeeId}${mode === 'edit' ? '' : `/${mode === 'employment-change' ? 'employment-changes' : mode}`}`;
      if (mode === 'create') {
        const fingerprint = JSON.stringify(body);
        if (
          !registration.current ||
          (registrationState === 'ready' && registration.current.fingerprint !== fingerprint)
        )
          registration.current = {
            key: crypto.randomUUID(),
            fingerprint,
            body,
            values: { ...values },
          };
        // Retry the frozen original request after an uncertain outcome, never silently
        // submit edited values or generate a new key after a timeout.
        return api<Employee>(endpoint, {
          method: 'POST',
          body: registration.current.body,
          idempotencyKey: registration.current.key,
        });
      }
      return api<Employee>(endpoint, { method: mode === 'edit' ? 'PUT' : 'POST', body });
    },
    onSuccess: async (result) => {
      saved.current = true;
      setConfirmation(null);
      await cache.invalidateQueries({ queryKey: ['employees'] });
      navigate(
        mode === 'create'
          ? `/hr/employees/${result.employeeId}?notice=created`
          : `${back}?notice=saved`,
      );
    },
    onError: (error: Error) => {
      if (mode === 'create') {
        if (
          error instanceof ApiError &&
          (error.code === 'idempotency_conflict' || error.code === 'registration_key_expired')
        ) {
          setRegistrationState('terminal');
          setGeneral(
            error.code === 'registration_key_expired'
              ? 'This registration key has expired and cannot create another employee. Review the directory before starting a new registration.'
              : 'This registration key was used with different details. No new employee was created by this request. Review the directory before starting a new registration.',
          );
          return;
        }
        if (
          !(error instanceof ApiError) ||
          error.status === 0 ||
          error.status >= 500 ||
          error.status === 409
        ) {
          setRegistrationState('uncertain');
          setGeneral(
            'The registration outcome is not confirmed. Retry the original submission to safely retrieve its result. The original details and registration key are retained.',
          );
          return;
        }
        setRegistrationState('ready');
      }
      const fieldMessages: string[] = [];
      if (error instanceof ApiError)
        for (const [key, messages] of Object.entries(error.fieldErrors)) {
          const field = key.split('.').pop() ?? key;
          const normalized = field.charAt(0).toLowerCase() + field.slice(1);
          if (!Object.hasOwn(getValues(), normalized)) fieldMessages.push(...messages);
          else {
            setError(normalized, { type: 'server', message: messages.join(' ') });
          }
        }
      setGeneral(
        fieldMessages.join(' ') ||
          (error instanceof ApiError
            ? error.status === 409
              ? 'This record changed or conflicts with existing data. Reload before retrying.'
              : error.message
            : 'Unable to save. Try again later.'),
      );
      setConfirmation(null);
    },
    onSettled: () => {
      submitting.current = false;
    },
  });
  function submit(values: Values) {
    if (submitting.current) return;
    clearErrors();
    setGeneral(null);
    if (mode === 'end-employment') setConfirmation(values);
    else {
      submitting.current = true;
      mutation.mutate(values);
    }
  }
  const unavailableEnd =
    mode === 'end-employment' &&
    (!record?.isCurrent || (account?.requiresOffboardingDecision && !securityManage));
  function text(name: string, label: string, required = false, maxLength = 100, type = 'text') {
    return (
      <FormField
        key={name}
        id={name}
        label={label}
        required={required}
        error={errors[name]?.message}
      >
        {(props) => (
          <Input
            {...props}
            type={type}
            maxLength={type !== 'date' ? maxLength : undefined}
            disabled={mutation.isPending}
            {...register(name, {
              validate: (value) => !required || !!value?.trim() || `${label} is required.`,
              maxLength: {
                value: maxLength,
                message: `${label} must be ${maxLength} characters or fewer.`,
              },
              ...(type === 'email'
                ? {
                    pattern: {
                      value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                      message: 'Enter a valid email address.',
                    },
                  }
                : {}),
            })}
          />
        )}
      </FormField>
    );
  }
  function select(name: string, master: MasterName, label: string, required = false) {
    const options = masters[master].filter(
      (item) =>
        (item.isActive || item.id === initial[name]) &&
        (master !== 'employment-statuses' ||
          (mode === 'end-employment' ? item.isTerminal : !item.isTerminal)),
    );
    return (
      <FormField
        key={name}
        id={name}
        label={label}
        required={required}
        error={errors[name]?.message}
      >
        {(props) => (
          <select
            {...props}
            className="ui-control employee-native-select"
            disabled={mutation.isPending}
            {...register(name, { required: required ? `${label} is required.` : false })}
          >
            <option value="">
              {mode === 'employment-change' ? 'Keep current value' : 'Select a value'}
            </option>
            {options.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
                {item.isActive ? '' : ' (inactive)'}
              </option>
            ))}
          </select>
        )}
      </FormField>
    );
  }
  return (
    <div className="employee-workspace employee-form">
      <BackLink to={back}>{employee ? 'Employee 360' : 'Employee directory'}</BackLink>
      {(mode === 'create' || mode === 'edit') && (
        <div>
          <p className="mt-2 text-sm text-muted-foreground">
            {mode === 'create'
              ? 'Enter personal information and initial employment details. User account provisioning remains a separate administration workflow.'
              : mode === 'edit'
                ? 'Correct personal information. Existing employment context and related records are retained; record an employment change to start a new period.'
                : 'Record employment dates and context. Previous employment history remains available.'}
          </p>
        </div>
      )}
      {employee && mode !== 'edit' && (
        <section className="employee-lifecycle-context" aria-label="Employment being reviewed">
          <h2>
            {employee.firstName} {employee.lastName}
          </h2>
          <p className="employee-context mb-4">
            {employee.employeeNumber} ·{' '}
            {record?.isCurrent
              ? 'Current employment'
              : record
                ? 'Previous employment context'
                : 'No current employment'}
          </p>
          <Facts
            items={[
              ['Employee record', employee.isActive ? 'Active' : 'Inactive'],
              [
                'Department',
                record ? masterLabel(masters, 'departments', record.departmentId) : null,
              ],
              [
                'Employment status',
                record
                  ? masterLabel(masters, 'employment-statuses', record.employmentStatusId)
                  : null,
              ],
            ]}
          />
          <p className="mt-4 text-sm">
            {mode === 'employment-change'
              ? 'A new period starts on the effective date. The current period ends on the preceding day. Leave a field unchanged to keep its current value.'
              : mode === 'end-employment'
                ? 'The end date is inclusive. Ending employment closes the current period and makes the employee record inactive. Review any linked account decision separately.'
                : 'Rehire starts a new employment period and retains previous history. It does not reactivate a linked account or change its roles.'}
          </p>
        </section>
      )}
      {unavailableEnd ? (
        <SystemState kind="permission" title="Employment cannot be ended here">
          {!record?.isCurrent
            ? 'There is no current employment record.'
            : 'This linked account requires an explicit offboarding decision by a user with Security.Manage. No changes were made.'}
        </SystemState>
      ) : (
        <form
          noValidate
          onSubmit={handleSubmit(submit)}
          className="space-y-6"
          aria-label={titles[mode]}
        >
          {general && (
            <div role="alert" className="rounded-md border border-danger p-4 text-sm">
              <p>{general}</p>
              {mode !== 'create' &&
                mutation.error instanceof ApiError &&
                mutation.error.status === 409 && (
                  <Button variant="outline" onClick={() => window.location.reload()}>
                    Reload record
                  </Button>
                )}
            </div>
          )}
          <fieldset
            className="min-w-0 space-y-6 border-0 p-0"
            disabled={mode === 'create' && (mutation.isPending || registrationState !== 'ready')}
          >
            {(mode === 'create' || mode === 'edit') && (
              <section className="employee-section">
                <h2>Personal information</h2>
                <p className="mb-4 text-sm text-muted-foreground">
                  {employee
                    ? `Employee number: ${employee.employeeNumber} · Permanent`
                    : 'Employee number is assigned automatically after registration.'}
                </p>
                <p className="mb-4 text-sm text-muted-foreground">
                  {mode === 'create'
                    ? 'A private profile photograph can be added from Employee 360 after registration.'
                    : 'Manage the private profile photograph from Employee 360. Photo changes are saved separately from profile details.'}
                </p>
                <div className="employee-form-grid">
                  {text('firstName', 'First name', true)}
                  {text('middleName', 'Middle name')}
                  {text('lastName', 'Last name', true)}
                  {text('preferredName', 'Preferred name')}
                  {text('dateOfBirth', 'Date of birth', false, 100, 'date')}
                  {select('genderId', 'genders', 'Gender')}
                  {select('maritalStatusId', 'marital-statuses', 'Marital status')}
                  {select('nationalityId', 'nationalities', 'Nationality')}
                </div>
              </section>
            )}
            {mode === 'create' && (
              <section className="employee-section">
                <h2>Initial contact information</h2>
                <p className="mb-4 text-sm text-muted-foreground">
                  Optional. This contact is saved with registration. Addresses and emergency
                  contacts can be added from Employee 360 after registration.
                </p>
                <div className="employee-form-grid">
                  {text('initialWorkEmail', 'Initial work email', false, 254, 'email')}
                  {text('initialMobile', 'Initial mobile', false, 30, 'tel')}
                </div>
              </section>
            )}
            {mode !== 'edit' && (
              <section className="employee-section">
                <h2>
                  {mode === 'end-employment' ? 'End current employment' : 'Employment information'}
                </h2>
                <div className="employee-form-grid">
                  {mode === 'end-employment' ? (
                    <>
                      {text('endDate', 'End date', true, 100, 'date')}
                      {select(
                        'employmentStatusId',
                        'employment-statuses',
                        'End-of-employment status',
                        true,
                      )}
                      {account?.requiresOffboardingDecision && (
                        <FormField
                          id="disableLinkedAccount"
                          label="Linked account decision"
                          required
                          error={errors.disableLinkedAccount?.message}
                        >
                          {(props) => (
                            <select
                              {...props}
                              className="ui-control employee-native-select"
                              disabled={mutation.isPending}
                              {...register('disableLinkedAccount', {
                                required: 'Choose an explicit account decision.',
                              })}
                            >
                              <option value="">Select a decision</option>
                              <option value="false">Keep linked account active</option>
                              <option value="true">Disable linked account</option>
                            </select>
                          )}
                        </FormField>
                      )}
                    </>
                  ) : (
                    <>
                      {visibleContextFields.map(([name, master, label, required]) =>
                        select(name, master, label, required && mode !== 'employment-change'),
                      )}
                      {mode === 'employment-change' ? (
                        text('effectiveDate', 'Effective date', true, 100, 'date')
                      ) : (
                        <>
                          {text('hireDate', 'Hire date', true, 100, 'date')}
                          {text('startDate', 'Start date', false, 100, 'date')}
                        </>
                      )}
                      <FormField
                        id="reportingToEmployeeId"
                        label="Reporting employee"
                        hint="Optional. Search for an existing active employee."
                        error={errors.reportingToEmployeeId?.message}
                      >
                        {(props) => (
                          <Controller
                            name="reportingToEmployeeId"
                            defaultValue=""
                            control={control}
                            render={({ field }) => (
                              <ReportingEmployee
                                {...props}
                                value={field.value ?? ''}
                                onChange={field.onChange}
                                employeeId={employee?.employeeId}
                                disabled={mutation.isPending}
                                retainCurrent={mode === 'employment-change'}
                              />
                            )}
                          />
                        )}
                      </FormField>
                    </>
                  )}
                </div>
              </section>
            )}
          </fieldset>
          <div className="employee-form-actions">
            <Button
              type={mode === 'create' && registrationState === 'uncertain' ? 'button' : 'submit'}
              loading={mutation.isPending}
              disabled={mode === 'create' && registrationState === 'terminal'}
              onClick={
                mode === 'create' && registrationState === 'uncertain'
                  ? () => {
                      if (registration.current) submit(registration.current.values);
                    }
                  : undefined
              }
            >
              {mode === 'create' && registrationState === 'uncertain'
                ? 'Retry original submission'
                : mode === 'end-employment'
                  ? 'Review end of employment'
                  : 'Save employee'}
            </Button>
            <LinkButton to={back}>Cancel</LinkButton>
          </div>
        </form>
      )}
      <AlertDialog
        open={!!confirmation}
        onOpenChange={(open) => {
          if (!open && !mutation.isPending) setConfirmation(null);
        }}
        title="End this employment?"
        description="This closes the current employment record and makes the employee inactive. Previous employment history is retained. Review the linked account decision before confirming."
        confirmLabel="End employment"
        loading={mutation.isPending}
        closeOnConfirm={false}
        onConfirm={() => {
          if (confirmation && !submitting.current) {
            submitting.current = true;
            mutation.mutate(confirmation);
          }
        }}
      />
      <AlertDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open && blocker.state === 'blocked') blocker.reset();
        }}
        title="Leave without saving?"
        description={
          mutation.isPending
            ? 'A save is in progress. Wait for the server response before leaving.'
            : 'Your unsaved employee changes will be discarded.'
        }
        confirmLabel="Leave page"
        loading={mutation.isPending}
        onConfirm={() => {
          if (!mutation.isPending && blocker.state === 'blocked') blocker.proceed();
        }}
      />
    </div>
  );
}
