import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Dialog as RadixDialog } from 'radix-ui';
import { Dialog } from '../../components/ui/overlays';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/controls';
import { FormField } from '../../components/shared/form-field';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import type { Employee, Master } from './contracts';
import { Facts, ContactSummary, QueryState } from './presentation';

type Kind = 'contacts' | 'addresses' | 'emergency-contacts';
type RecordValue = Record<string, string | boolean | null | undefined>;
type Field = {
  name: string;
  label: string;
  max: number;
  required?: boolean;
  type?: 'email' | 'tel';
  master?: string;
};
const titles: Record<Kind, string> = {
  contacts: 'Contact',
  addresses: 'Address',
  'emergency-contacts': 'Emergency contact',
};
const fields: Record<Kind, Field[]> = {
  contacts: [
    { name: 'workEmail', label: 'Work email', max: 254, type: 'email' },
    { name: 'personalEmail', label: 'Personal email', max: 254, type: 'email' },
    { name: 'mobile', label: 'Mobile', max: 30, type: 'tel' },
    { name: 'phone', label: 'Phone', max: 30, type: 'tel' },
    { name: 'workPhone', label: 'Work phone', max: 30, type: 'tel' },
  ],
  addresses: [
    {
      name: 'addressTypeId',
      label: 'Address type',
      max: 100,
      required: true,
      master: 'address-types',
    },
    { name: 'addressLine1', label: 'Address line 1', max: 200, required: true },
    { name: 'addressLine2', label: 'Address line 2', max: 200 },
    { name: 'city', label: 'City', max: 100 },
    { name: 'stateProvince', label: 'State / province', max: 100 },
    { name: 'countryId', label: 'Country', max: 100, master: 'countries' },
    { name: 'postalCode', label: 'Postal code', max: 20 },
  ],
  'emergency-contacts': [
    { name: 'name', label: 'Contact name', max: 200, required: true },
    { name: 'relationship', label: 'Relationship', max: 80, required: true },
    { name: 'phone', label: 'Phone', max: 30, required: true, type: 'tel' },
    { name: 'alternativePhone', label: 'Alternative phone', max: 30, type: 'tel' },
    { name: 'address', label: 'Contact address', max: 500 },
  ],
};

export function SupportingInformation({
  employee,
  manage,
}: {
  employee: Employee;
  manage: boolean;
}) {
  const [editing, setEditing] = useState<{ kind: Kind; id?: string; record?: RecordValue } | null>(
    null,
  );
  const [notice, setNotice] = useState('');
  const opener = useRef<HTMLButtonElement | null>(null);
  const actions = (kind: Kind, id?: string, record?: RecordValue) =>
    manage && (
      <Button
        variant="outline"
        onClick={(event) => {
          opener.current = event.currentTarget;
          setNotice('');
          setEditing({ kind, id, record });
        }}
      >
        {id ? `Edit ${titles[kind].toLowerCase()}` : `Add ${titles[kind].toLowerCase()}`}
      </Button>
    );
  return (
    <div className="space-y-6">
      {notice && <p role="status">{notice}</p>}
      <section aria-label="Personal details">
        <div className="employee-toolbar mb-4">
          <h3 className="ui-section-title">Personal details</h3>
        </div>
        <Facts
          items={[
            ['Permanent employee number', employee.employeeNumber],
            ['Date of birth', employee.dateOfBirth],
            ['Gender', employee.gender],
            ['Marital status', employee.maritalStatus],
            ['Nationality', employee.nationality],
          ]}
        />
      </section>
      <section className="employee-section" aria-label="Contact information">
        <div className="employee-toolbar mb-4">
          <h3 className="ui-section-title">Contact information</h3>
          {actions('contacts')}
        </div>
        <ContactSummary
          employee={employee}
          actions={(record) => actions('contacts', record.employeeContactId, record)}
        />
      </section>
      <section className="employee-section" aria-label="Addresses">
        <div className="employee-toolbar mb-4">
          <h3 className="ui-section-title">Addresses</h3>
          {actions('addresses')}
        </div>
        {employee.addresses.length ? (
          employee.addresses.map((record) => (
            <article className="employee-support-record" key={record.employeeAddressId}>
              <div className="employee-toolbar">
                <h4 className="ui-subsection-title">
                  {record.addressType}
                  {record.isPrimary ? ' · Primary' : ''}
                </h4>
                {actions('addresses', record.employeeAddressId, record)}
              </div>
              <p className="text-sm break-words">
                {[
                  record.addressLine1,
                  record.addressLine2,
                  record.city,
                  record.stateProvince,
                  record.postalCode,
                  record.country,
                ]
                  .filter(Boolean)
                  .join(', ')}
              </p>
            </article>
          ))
        ) : (
          <p className="text-sm text-muted-foreground">No addresses recorded.</p>
        )}
      </section>
      <section className="employee-section" aria-label="Emergency contacts">
        <div className="employee-toolbar mb-4">
          <h3 className="ui-section-title">Emergency contacts</h3>
          {actions('emergency-contacts')}
        </div>
        {employee.emergencyContacts.length ? (
          employee.emergencyContacts.map((record) => (
            <article className="employee-support-record" key={record.emergencyContactId}>
              <div className="employee-toolbar mb-2">
                <h4 className="ui-subsection-title">
                  {record.name}
                  {record.isPrimary ? ' · Primary' : ''}
                </h4>
                {actions('emergency-contacts', record.emergencyContactId, record)}
              </div>
              <Facts
                items={[
                  ['Relationship', record.relationship],
                  ['Phone', record.phone],
                  ['Alternative phone', record.alternativePhone],
                  ['Contact address', record.address],
                  ['Mobile', record.mobile],
                  ['Email', record.email],
                ]}
              />
            </article>
          ))
        ) : (
          <p className="text-sm text-muted-foreground">No emergency contacts recorded.</p>
        )}
      </section>
      {editing && (
        <SupportingEditor
          key={`${editing.kind}-${editing.id ?? 'new'}`}
          {...editing}
          employeeId={employee.employeeId}
          returnFocus={() => opener.current?.focus()}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setNotice(`${titles[editing.kind]} saved successfully.`);
            setEditing(null);
          }}
        />
      )}
    </div>
  );
}

function SupportingEditor({
  kind,
  id,
  record,
  employeeId,
  onClose,
  onSaved,
  returnFocus,
}: {
  kind: Kind;
  id?: string;
  record?: RecordValue;
  employeeId: string;
  onClose: () => void;
  onSaved: () => void;
  returnFocus: () => void;
}) {
  const cache = useQueryClient();
  const submitting = useRef(false);
  const [general, setGeneral] = useState('');
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm<Record<string, string>>({
    defaultValues: Object.fromEntries([
      ...fields[kind].map((field) => [field.name, String(record?.[field.name] ?? '')]),
      ['isPrimary', record?.isPrimary ? 'true' : 'false'],
    ]),
  });
  useEffect(() => {
    const protect = (event: BeforeUnloadEvent) => {
      if (isDirty || submitting.current) event.preventDefault();
    };
    window.addEventListener('beforeunload', protect);
    return () => window.removeEventListener('beforeunload', protect);
  }, [isDirty]);
  const masters = useQuery({
    queryKey: ['supporting-address-masters'],
    enabled: kind === 'addresses',
    queryFn: async ({ signal }) => ({
      'address-types': await api<Master[]>('/api/master-data/address-types?includeInactive=true', {
        signal,
      }),
      countries: await api<Master[]>('/api/master-data/countries?includeInactive=true', { signal }),
    }),
  });
  const mutation = useMutation({
    mutationFn: (values: Record<string, string>) =>
      api(`/api/employees/${employeeId}/${kind}${id ? `/${id}` : ''}`, {
        method: id ? 'PUT' : 'POST',
        body: {
          ...Object.fromEntries(
            fields[kind].map((field) => [
              field.name,
              values[field.name]?.trim() || (kind === 'emergency-contacts' && id ? '' : null),
            ]),
          ),
          isPrimary: values.isPrimary === 'true',
        },
      }),
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ['employees'] });
      onSaved();
    },
    onError: (error: Error) => {
      let unknown: string[] = [];
      if (error instanceof ApiError)
        for (const [key, messages] of Object.entries(error.fieldErrors)) {
          const name = key.charAt(0).toLowerCase() + key.slice(1);
          if (fields[kind].some((field) => field.name === name))
            setError(name, { message: messages.join(' ') });
          else unknown = [...unknown, ...messages];
        }
      setGeneral(
        unknown.join(' ') ||
          (error instanceof ApiError ? error.message : 'Unable to save this record. Try again.'),
      );
    },
    onSettled: () => {
      submitting.current = false;
    },
  });
  const submit = (event: React.FormEvent<HTMLFormElement>) =>
    handleSubmit((values) => {
      if (submitting.current) return;
      setGeneral('');
      if (kind === 'contacts' && !fields.contacts.some((field) => values[field.name]?.trim())) {
        setError('workEmail', { message: 'Enter at least one contact value.' });
        return;
      }
      if (id && kind !== 'emergency-contacts') {
        const removed = fields[kind].find(
          (field) => record?.[field.name] && !values[field.name]?.trim(),
        );
        if (removed) {
          setError(removed.name, {
            message:
              'This API preserves blank values. Enter a replacement or keep the existing value.',
          });
          return;
        }
      }
      submitting.current = true;
      mutation.mutate(values);
    })(event);
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="md"
      dirty={isDirty}
      pending={mutation.isPending}
      onCloseAutoFocus={(event) => {
        event.preventDefault();
        returnFocus();
      }}
      title={`${id ? 'Edit' : 'Add'} ${titles[kind].toLowerCase()}`}
      description="Update this employee’s supporting information. Account and employment records are unchanged."
      footer={
        <>
          <Button
            type="submit"
            form="supporting-record-form"
            loading={mutation.isPending}
            disabled={kind === 'addresses' && !masters.data}
          >
            Save {titles[kind].toLowerCase()}
          </Button>
          <RadixDialog.Close asChild>
            <Button variant="outline" disabled={mutation.isPending}>
              Cancel
            </Button>
          </RadixDialog.Close>
        </>
      }
    >
      {kind === 'addresses' && (
        <QueryState
          loading={masters.isPending}
          error={masters.error}
          retry={() => void masters.refetch()}
        />
      )}
      <form id="supporting-record-form" noValidate onSubmit={submit} className="employee-form-grid">
        {general && (
          <p className="employee-form-wide text-sm text-danger" role="alert">
            {general}
          </p>
        )}
        {fields[kind].map((field) => (
          <FormField
            key={field.name}
            id={`support-${field.name}`}
            label={field.label}
            required={field.required}
            error={errors[field.name]?.message}
          >
            {(props) =>
              field.master ? (
                <select
                  {...props}
                  className="ui-control employee-native-select"
                  disabled={mutation.isPending}
                  {...register(field.name, {
                    required: field.required && `${field.label} is required.`,
                  })}
                >
                  <option value="">Select a value</option>
                  {masters.data?.[field.master as 'address-types' | 'countries']
                    .filter((item) => item.isActive || item.id === record?.[field.name])
                    .map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name}
                        {!item.isActive ? ' (inactive)' : ''}
                      </option>
                    ))}
                </select>
              ) : (
                <Input
                  {...props}
                  type={field.type ?? 'text'}
                  maxLength={field.max}
                  disabled={mutation.isPending}
                  {...register(field.name, {
                    validate: (value) =>
                      !field.required || !!value.trim() || `${field.label} is required.`,
                    maxLength: {
                      value: field.max,
                      message: `${field.label} must be ${field.max} characters or fewer.`,
                    },
                    ...(field.type === 'email'
                      ? {
                          pattern: {
                            value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                            message: 'Enter a valid email address.',
                          },
                        }
                      : {}),
                  })}
                />
              )
            }
          </FormField>
        ))}
        <FormField
          id="support-primary"
          label="Primary record"
          hint="The first record becomes primary automatically. Existing primary-selection rules remain in effect."
        >
          {(props) => (
            <select
              {...props}
              className="ui-control employee-native-select"
              disabled={mutation.isPending}
              {...register('isPrimary')}
            >
              <option value="false">No</option>
              <option value="true">Yes</option>
            </select>
          )}
        </FormField>
        {id && kind !== 'emergency-contacts' && (
          <p className="employee-form-wide text-sm text-muted-foreground">
            Existing contact and address APIs preserve empty fields; replacement values can be
            entered here.
          </p>
        )}
      </form>
    </Dialog>
  );
}
