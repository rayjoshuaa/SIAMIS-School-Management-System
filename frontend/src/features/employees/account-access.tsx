import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { useAuth } from '../../lib/auth/auth-context';
import { Button } from '../../components/ui/button';
import { Checkbox } from '../../components/ui/controls';
import { Alert } from '../../components/ui/feedback';
import { AlertDialog } from '../../components/ui/overlays';
import { permanentRoles, type Account } from '../administration/account-contracts';
import type { AccountLifecycle, Employee } from './contracts';
import { Facts, QueryState } from './presentation';

export function AccountAccess({ employee }: { employee: Employee }) {
  const { state } = useAuth();
  const security =
    state.status === 'authenticated' && state.user?.capabilities.includes('Security.Manage');
  const lifecycle = useQuery({
    queryKey: ['employees', 'account-lifecycle', employee.employeeId],
    queryFn: ({ signal }) =>
      api<AccountLifecycle>(`/api/employees/${employee.employeeId}/account-lifecycle`, { signal }),
  });
  const linked = lifecycle.data;
  return (
    <section className="employee-section space-y-4" aria-label="Account access">
      <h2>Account access</h2>
      <p className="text-sm text-muted-foreground">
        Employee, employment, account and credential statuses are separate. Rehire does not
        reactivate an account or change its roles.
      </p>
      <Facts
        items={[
          ['Employee record status', employee.isActive ? 'Active' : 'Inactive'],
          [
            'Employment status',
            linked?.currentEmploymentStatus ?? employee.currentEmployment?.employmentStatus,
          ],
        ]}
      />
      <QueryState
        loading={lifecycle.isPending}
        error={lifecycle.error}
        retry={() => void lifecycle.refetch()}
      />
      {linked && !lifecycle.isError && (
        <>
          <Facts
            items={[
              [
                'System account status',
                linked.accountLinked ? linked.accountStatus : 'No linked account',
              ],
              ['Current employment', linked.hasCurrentEmployment ? 'Present' : 'Not present'],
            ]}
          />
          {!linked.accountLinked ? (
            <p className="text-sm">
              No Identity account is linked to this employee. An employee record does not itself
              grant application access.
            </p>
          ) : security && linked.linkedUserId ? (
            <LinkedAccount
              key={linked.linkedUserId}
              userId={linked.linkedUserId}
              employeeId={employee.employeeId}
            />
          ) : (
            <p className="text-sm text-muted-foreground">
              Assigned roles, credential details and account administration require Security.Manage.
            </p>
          )}
          {linked.accountStatus === 'Disabled' && linked.hasCurrentEmployment && (
            <Alert intent="info" title="Access review">
              Current employment is present and the linked account is disabled. These facts do not
              imply a policy violation.
            </Alert>
          )}
          {linked.accountStatus === 'Active' && !linked.hasCurrentEmployment && (
            <Alert intent="info" title="Access review">
              The linked account is active and no current employment is reported. Review access
              explicitly; no account action has been taken.
            </Alert>
          )}
          {security && (
            <div className="space-y-2">
              <Link className="ui-link inline-flex min-h-11 items-center" to="/hr/security">
                {linked.accountLinked ? 'Open User Accounts' : 'Provision through User Accounts'}
              </Link>
              {!linked.accountLinked && (
                <p className="break-all text-sm text-muted-foreground">
                  For provisioning, use Employee ID: {employee.employeeId}. Linkage is entered and
                  validated in the existing Create User workflow.
                </p>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
}

function LinkedAccount({ userId, employeeId }: { userId: string; employeeId: string }) {
  const cache = useQueryClient();
  const key = ['employees', 'linked-account', employeeId, userId];
  const detail = useQuery({
    queryKey: key,
    queryFn: ({ signal }) => api<Account>(`/api/admin/users/${userId}`, { signal }),
  });
  const [command, setCommand] = useState<'status' | 'roles' | null>(null);
  const [roles, setRoles] = useState<string[]>([]);
  const [notice, setNotice] = useState('');
  const invoker = useRef<HTMLButtonElement | null>(null);
  const submitting = useRef(false);
  const update = useMutation({
    mutationFn: async () => {
      const account = detail.data;
      if (!account || account.employeeId !== employeeId || !command)
        throw new Error('Reload account linkage before continuing.');
      return api<Account>(`/api/admin/users/${userId}/${command}`, {
        method: command === 'roles' ? 'PUT' : 'PATCH',
        body:
          command === 'roles'
            ? { roles, version: account.version }
            : { isActive: !account.isActive, version: account.version },
      });
    },
    onSuccess: async () => {
      setNotice(
        'Account change saved. Existing sessions are revoked by the backend. Employment was not changed.',
      );
      setCommand(null);
      await Promise.all([
        cache.invalidateQueries({ queryKey: ['employees', 'linked-account', employeeId] }),
        cache.invalidateQueries({ queryKey: ['employees', 'account-lifecycle', employeeId] }),
        cache.invalidateQueries({ queryKey: ['administration-users'] }),
      ]);
    },
    onSettled: () => {
      submitting.current = false;
    },
  });
  const account = detail.data;
  if (!account || detail.isError)
    return (
      <QueryState
        loading={detail.isPending}
        error={detail.error}
        retry={() => void detail.refetch()}
      />
    );
  if (account.employeeId !== employeeId)
    return (
      <Alert intent="warning" title="Linkage changed">
        Reload the employee before administering this account.
      </Alert>
    );
  function open(next: 'status' | 'roles', button: HTMLButtonElement) {
    invoker.current = button;
    setRoles(account!.roles.slice());
    setNotice('');
    update.reset();
    setCommand(next);
  }
  const knownRoles = account.roles.every((role) => permanentRoles.some((value) => value === role));
  return (
    <div className="space-y-4">
      <Facts
        items={[
          ['Linked account', account.userName],
          ['Account status', account.isActive ? 'Active' : 'Disabled'],
          ['Assigned roles', account.roles.join(', ') || 'None'],
          [
            'Credential status',
            !account.credentialEstablished
              ? 'Activation pending'
              : account.requiresPasswordChange
                ? 'Password change required'
                : 'Established',
          ],
          ['Email verification', account.emailConfirmed ? 'Confirmed' : 'Not confirmed'],
          ['Lockout status', account.isLockedOut ? 'Locked out' : 'Not locked out'],
        ]}
      />
      {!account.credentialEstablished && (
        <Alert intent="info" title="Activation pending">
          The user must establish their own password through the existing activation workflow.
        </Alert>
      )}
      {notice && (
        <p role="status" className="text-sm">
          {notice}
        </p>
      )}
      <div className="flex flex-wrap gap-3">
        <Button variant="outline" onClick={(e) => open('status', e.currentTarget)}>
          {account.isActive ? 'Disable account' : 'Enable account'}
        </Button>
        <Button
          variant="outline"
          disabled={!knownRoles}
          onClick={(e) => open('roles', e.currentTarget)}
        >
          Replace assigned roles
        </Button>
      </div>
      {!knownRoles && (
        <p className="text-sm">
          Unrecognized role assignments require review in User Accounts before replacement.
        </p>
      )}
      <p className="text-sm text-muted-foreground">
        Activation and password recovery are initiated with confirmation in User Accounts. No
        password or token is displayed here.
      </p>
      <AlertDialog
        open={command !== null}
        onOpenChange={(open) => {
          if (!open && !update.isPending) setCommand(null);
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          invoker.current?.focus();
        }}
        title={
          command === 'roles'
            ? 'Replace assigned roles?'
            : account.isActive
              ? 'Disable linked account?'
              : 'Enable linked account?'
        }
        description={`This changes application access for ${account.userName} and revokes existing sessions. It does not change employment. The backend validates the current version and protects the last usable SystemAdmin.`}
        confirmLabel="Confirm account change"
        closeOnConfirm={false}
        loading={update.isPending}
        onConfirm={() => {
          if (!submitting.current) {
            submitting.current = true;
            update.mutate();
          }
        }}
      >
        {command === 'roles' && (
          <fieldset disabled={update.isPending}>
            <legend className="text-sm font-semibold">Complete replacement role set</legend>
            <p className="my-2 text-sm">
              Unselected roles will be removed. Selecting no roles removes all role grants.
            </p>
            {permanentRoles.map((role) => (
              <label className="flex min-h-11 items-center gap-2 text-sm" key={role}>
                <Checkbox
                  checked={roles.includes(role)}
                  onCheckedChange={(checked) =>
                    setRoles((previous) =>
                      checked === true
                        ? [...previous, role]
                        : previous.filter((value) => value !== role),
                    )
                  }
                />
                {role}
              </label>
            ))}
          </fieldset>
        )}
        {update.isError && (
          <p role="alert" className="mt-3 text-sm text-destructive">
            {update.error.message} Reload Account access before retrying a conflict. No successful
            change is implied.
          </p>
        )}
      </AlertDialog>
      <Button variant="ghost" onClick={() => void detail.refetch()}>
        Refresh account details
      </Button>
    </div>
  );
}
