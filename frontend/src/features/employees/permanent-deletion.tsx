import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../lib/auth/auth-context';
import { api } from '../../lib/api/client';
import { Button } from '../../components/ui/button';
import { Dialog } from '../../components/ui/overlays';
import { Input } from '../../components/ui/controls';
import { FormField } from '../../components/shared/form-field';

type Assessment = {
  state: 'Eligible' | 'Blocked' | 'ReviewRequired';
  version: string;
  blockers: string[];
};
const explanations: Record<string, string> = {
  identity_requires_review: 'Permanent identity protection needs review.',
  employment_history: 'Employment history or record state prevents deletion.',
  registration_provenance_missing: 'Audited initial registration provenance is unavailable.',
  protected_history: 'Retained history prevents deletion.',
  protected_records: 'Protected dependencies prevent deletion.',
  linked_account:
    'A linked account requires separate administration; disabling it does not remove linkage.',
  payroll_reference: 'An employee payroll-rule reference prevents deletion.',
  schema_requires_review: 'An unreviewed database dependency prevents deletion.',
  audit_requires_review: 'Retained audit evidence requires review before deletion.',
};
export function PermanentDeletion({ id, number }: { id: string; number: string }) {
  const { state } = useAuth();
  const allowed =
    state.user?.capabilities.includes('Employee.DeletePermanent') &&
    state.user.capabilities.includes('Employee.Read');
  if (!allowed) return null;
  return <DeletionArea id={id} number={number} />;
}
function DeletionArea({ id, number }: { id: string; number: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [identity, setIdentity] = useState('');
  const navigate = useNavigate();
  const cache = useQueryClient();
  const path = `/api/employees/${id}/permanent-deletion`;
  const assessment = useQuery<Assessment>({
    queryKey: ['employee-deletion', id],
    queryFn: ({ signal }) => api(path, { signal }),
    enabled: open,
    retry: false,
  });
  const deletion = useMutation({
    mutationFn: () =>
      api(path, {
        method: 'POST',
        body: {
          employeeNumber: identity,
          reason: reason.trim(),
          version: assessment.data?.version,
        },
      }),
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ['employees'] });
      cache.removeQueries({ queryKey: ['employee', id] });
      navigate('/hr/employees', { replace: true });
    },
    onError: () => {
      void assessment.refetch();
    },
  });
  const ready =
    assessment.data?.state === 'Eligible' && !assessment.isFetching && !assessment.isError;
  return (
    <section className="mt-6 border-t border-border pt-4" aria-labelledby="employee-administration">
      <h3 id="employee-administration">Restricted administration</h3>
      <p className="text-sm text-muted-foreground">
        End Employment is the standard departure workflow. Permanent deletion is only for erroneous
        registrations.
      </p>
      <Dialog
        open={open}
        onOpenChange={(value) => {
          setOpen(value);
          if (!value) {
            setReason('');
            setIdentity('');
            deletion.reset();
          }
        }}
        trigger={
          <Button
            variant="destructive-outline"
            density="compact"
            onClick={() => {
              deletion.reset();
              void assessment.refetch();
            }}
          >
            Assess permanent deletion
          </Button>
        }
        title="Permanently delete erroneous registration"
        description={`Employee ${number}. This cannot be undone. The permanent number and audit evidence will remain reserved.`}
        size="md"
        dirty={!!reason || !!identity}
        pending={deletion.isPending}
        footer={
          <Button
            variant="destructive"
            loading={deletion.isPending}
            disabled={
              !ready || identity !== number || reason.trim().length < 5 || deletion.isPending
            }
            onClick={() => deletion.mutate()}
          >
            Permanently delete employee
          </Button>
        }
      >
        {assessment.isFetching && <p role="status">Checking dependencies…</p>}
        {assessment.isError && (
          <div role="alert">
            Dependency assessment unavailable.{' '}
            <Button onClick={() => void assessment.refetch()}>Try again</Button>
          </div>
        )}
        {assessment.data && !assessment.isError && (
          <>
            <p role="status">
              {assessment.data.state === 'Eligible'
                ? 'Eligible erroneous registration'
                : 'Permanent deletion unavailable'}
            </p>
            <ul>
              {assessment.data.blockers.map((code) => (
                <li key={code}>{explanations[code] ?? 'A dependency requires review.'}</li>
              ))}
            </ul>
          </>
        )}
        {ready && (
          <div className="grid gap-4">
            <FormField
              id="delete-reason"
              label="Reason this registration was created by mistake"
              required
            >
              {(props) => (
                <Input
                  {...props}
                  value={reason}
                  maxLength={120}
                  onChange={(event) => setReason(event.target.value)}
                />
              )}
            </FormField>
            <FormField
              id="delete-identity"
              label={`Type ${number} to confirm the employee identity`}
              required
            >
              {(props) => (
                <Input
                  {...props}
                  value={identity}
                  autoComplete="off"
                  onChange={(event) => setIdentity(event.target.value)}
                />
              )}
            </FormField>
          </div>
        )}
        {deletion.isError && (
          <p role="alert">
            Deletion did not complete. Review the refreshed assessment before retrying.
          </p>
        )}
      </Dialog>
    </section>
  );
}
