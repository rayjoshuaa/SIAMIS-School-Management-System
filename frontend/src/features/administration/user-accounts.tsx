import { useState, useRef, useEffect, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../components/ui/button';
import { Input, Checkbox } from '../../components/ui/controls';
import { Alert, Badge, Spinner } from '../../components/ui/feedback';
import { Table, TableHeader, TableRow, TableHead, TableCell } from '../../components/ui/table';
import { FormField } from '../../components/shared/form-field';
import { Dialog, Sheet, AlertDialog } from '../../components/ui/overlays';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { useAuth } from '../../lib/auth/auth-context';
import { can } from '../../lib/auth/capabilities';

import { permanentRoles, type Account } from './account-contracts';
type AccountPage = { items: Account[]; totalCount: number; page: number; pageSize: number };
type CreateAccount = {
  userName: string;
  email: string;
  employeeId: string | null;
  roles: string[];
};
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function errorMessage(error: Error) {
  if (error instanceof ApiError && error.status === 409)
    return 'The account already exists or its state changed. Refresh and review before retrying.';
  if (error instanceof ApiError && error.status === 503)
    return 'Credential delivery is unavailable. No successful provisioning or delivery is implied; contact the administrator.';
  if (error instanceof ApiError && error.kind === 'validation')
    return 'Check the username, email, roles and Employee link. The server rejected this request.';
  return error instanceof ApiError ? error.message : 'The request could not be completed.';
}
function CreateUser({
  onCreated,
  onCancel,
  onPendingChange,
}: {
  onCreated: (account: Account) => void;
  onCancel: () => void;
  onPendingChange: (pending: boolean) => void;
}) {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const create = useMutation({
    mutationFn: (body: CreateAccount) => api<Account>('/api/admin/users', { method: 'POST', body }),
    onSuccess: onCreated,
  });
  useEffect(() => {
    onPendingChange(create.isPending);
    return () => onPendingChange(false);
  }, [create.isPending, onPendingChange]);
  function submit(event: FormEvent) {
    event.preventDefault();
    create.reset();
    const validation: Record<string, string> = {};
    if (!userName.trim()) validation.userName = 'Username is required.';
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()))
      validation.email = 'Enter a valid delivery email.';
    if (!roles.length) validation.roles = 'Select at least one role.';
    if (
      employeeId.trim() &&
      (!guid.test(employeeId.trim()) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(employeeId.trim()))
    )
      validation.employeeId = 'Enter a valid Employee ID.';
    if (roles.includes('Employee') && !employeeId.trim())
      validation.employeeId = 'The Employee role requires an existing Employee link.';
    setErrors(validation);
    if (Object.keys(validation).length) return;
    create.mutate({
      userName: userName.trim(),
      email: email.trim(),
      roles,
      employeeId: employeeId.trim() || null,
    });
  }
  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {create.isError && (
        <div role="alert">
          <Alert intent="danger" title="Account not created">
            {errorMessage(create.error)}
          </Alert>
        </div>
      )}
      <FormField id="account-username" label="Username" required error={errors.userName}>
        {(props) => (
          <Input
            {...props}
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
            maxLength={256}
            autoComplete="off"
            disabled={create.isPending}
          />
        )}
      </FormField>
      <FormField id="account-email" label="Delivery email" required error={errors.email}>
        {(props) => (
          <Input
            {...props}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            maxLength={256}
            disabled={create.isPending}
          />
        )}
      </FormField>
      <fieldset
        disabled={create.isPending}
        aria-describedby="account-roles-hint account-roles-error"
      >
        <legend className="text-sm font-semibold">
          Assigned roles{' '}
          <span aria-hidden="true" className="text-destructive">
            *
          </span>
        </legend>
        <p id="account-roles-hint" className="mt-2 text-xs text-muted-foreground">
          Roles are independent security assignments. HRAdmin does not grant Payroll; SystemAdmin
          alone does not grant HR Documents.
        </p>
        <div className="mt-2 grid gap-x-4 sm:grid-cols-2">
          {permanentRoles.map((role) => (
            <label key={role} className="flex items-center gap-2 text-sm">
              <Checkbox
                aria-label={role}
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
        </div>
        <p
          id="account-roles-error"
          role={errors.roles ? 'alert' : undefined}
          className="text-sm text-destructive"
        >
          {errors.roles}
        </p>
      </fieldset>
      <FormField
        id="account-employee"
        label="Employee ID"
        error={errors.employeeId}
        hint="Optional existing Employee GUID; required for the Employee role. The server validates linkage and ownership. This form never creates an Employee."
      >
        {(props) => (
          <Input
            {...props}
            value={employeeId}
            onChange={(e) => setEmployeeId(e.target.value)}
            disabled={create.isPending}
          />
        )}
      </FormField>
      <div className="sticky bottom-0 flex flex-wrap justify-end gap-2 border-t border-border bg-surface pt-3">
        <Button variant="outline" onClick={onCancel} disabled={create.isPending}>
          Cancel
        </Button>
        <Button type="submit" loading={create.isPending}>
          Create User
        </Button>
      </div>
    </form>
  );
}
export function UserAccounts() {
  const { state } = useAuth();
  if (state.status !== 'authenticated' || !can(state.user, 'Security.Manage'))
    return (
      <Alert intent="warning" title="Access denied">
        Security.Manage is required to administer accounts.
      </Alert>
    );
  return <AuthorizedAccounts actorId={state.user!.userId} />;
}
function AuthorizedAccounts({ actorId }: { actorId: string }) {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  type Overlay = 'create' | 'detail' | 'confirm' | null;
  const [overlay, setOverlay] = useState<Overlay>(null);
  const overlayRef = useRef<Overlay>(null);
  const invoker = useRef<HTMLButtonElement | null>(null);
  const [createBusy, setCreateBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<'issue' | 'collect'>('issue');
  function changeOverlay(next: Overlay) {
    overlayRef.current = next;
    setOverlay(next);
  }
  function restoreFocus(event: Event) {
    event.preventDefault();
    if (overlayRef.current === null) invoker.current?.focus();
  }
  const [selected, setSelected] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const [developmentLink, setDevelopmentLink] = useState<string | null>(null);
  const key = ['administration-users', actorId];
  const accounts = useQuery({
    queryKey: [...key, page],
    queryFn: ({ signal }) =>
      api<AccountPage>(`/api/admin/users?page=${page}&pageSize=20`, { signal }),
  });
  const detail = useQuery({
    queryKey: [...key, 'detail', selected],
    enabled: !!selected,
    queryFn: ({ signal }) => api<Account>(`/api/admin/users/${selected}`, { signal }),
  });
  const issue = useMutation({
    mutationFn: (account: Account) =>
      api<{ purpose: string }>(`/api/admin/users/${account.userId}/issue-credentials`, {
        method: 'POST',
        body: { version: account.version },
      }),
    onSuccess: async (result) => {
      setNotice(
        `${result.purpose === 'Activation' ? 'Activation' : 'Password recovery'} initiated through the configured delivery channel.`,
      );
      setDevelopmentLink(null);
      await cache.invalidateQueries({ queryKey: key });
      changeOverlay('detail');
    },
  });
  const collect = useMutation({
    mutationFn: async (account: Account) => {
      if (!import.meta.env.DEV) throw new Error('Development delivery is unavailable.');
      return api<{ userId: string; token: string; purpose: string }>(
        `/api/admin/users/${account.userId}/credential-delivery`,
        { method: 'POST' },
      );
    },
    onSuccess: (delivery) => {
      if (
        delivery.userId !== selected ||
        !guid.test(delivery.userId) ||
        !delivery.token ||
        !['Activation', 'PasswordReset'].includes(delivery.purpose)
      )
        return;
      const path = delivery.purpose === 'Activation' ? '/activate' : '/reset-password';
      setDevelopmentLink(
        `${window.location.origin}${path}?${new URLSearchParams({ userId: delivery.userId, token: delivery.token })}`,
      );
      changeOverlay('detail');
    },
  });
  function select(id: string) {
    setSelected(id);
    setDevelopmentLink(null);
    setNotice('');
    issue.reset();
    collect.reset();
    changeOverlay('detail');
  }
  const account = detail.data;
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Account provisioning and activation. Access is enforced by Security.Manage on the server.
        </p>
        <Button
          onClick={(event) => {
            invoker.current = event.currentTarget;
            changeOverlay('create');
            setNotice('');
          }}
          disabled={overlay !== null}
        >
          Create User
        </Button>
      </div>
      {notice && overlay === null && (
        <div role="status">
          <Alert intent="success" title="Account administration">
            {notice}
          </Alert>
        </div>
      )}
      <Dialog
        open={overlay === 'create'}
        onOpenChange={(open) => {
          if (!open && !createBusy) changeOverlay(null);
        }}
        dismissible={!createBusy}
        onCloseAutoFocus={restoreFocus}
        title="Create User"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          document.getElementById('account-username')?.focus();
        }}
        description="Provision a passwordless account. Activation lets the user establish their own password through the configured delivery channel."
      >
        {overlay === 'create' && (
          <CreateUser
            onPendingChange={setCreateBusy}
            onCancel={() => changeOverlay(null)}
            onCreated={(created) => {
              select(created.userId);
              setNotice(
                'Account created. Initial activation issued through the configured delivery channel.',
              );
              void cache.invalidateQueries({ queryKey: key });
            }}
          />
        )}
      </Dialog>
      {accounts.isPending ? (
        <Spinner label="Loading user accounts" />
      ) : accounts.isError ? (
        <div role="alert">
          <Alert intent="danger" title="Accounts unavailable">
            {errorMessage(accounts.error)}
          </Alert>
          <Button className="mt-3" variant="outline" onClick={() => void accounts.refetch()}>
            Retry
          </Button>
        </div>
      ) : (
        <>
          <div
            role="region"
            aria-label="User accounts"
            tabIndex={0}
            className="overflow-x-auto border border-border bg-surface"
          >
            <Table>
              <caption className="sr-only">User accounts and their real assigned roles</caption>
              <TableHeader>
                <TableRow>
                  <TableHead>Username</TableHead>
                  <TableHead>Roles</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Credential</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <tbody>
                {accounts.data.items.map((user) => (
                  <TableRow key={user.userId}>
                    <TableCell>{user.userName}</TableCell>
                    <TableCell>{user.roles.join(', ') || 'None'}</TableCell>
                    <TableCell>
                      <Badge intent={user.isActive ? 'success' : 'neutral'}>
                        {user.isActive ? 'Active' : 'Disabled'}
                      </Badge>
                      {user.isLockedOut && <span className="ml-2">Locked out</span>}
                    </TableCell>
                    <TableCell>
                      {user.credentialEstablished
                        ? user.requiresPasswordChange
                          ? 'Password change required'
                          : 'Established'
                        : 'Activation pending'}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="outline"
                        aria-label={`View ${user.userName}`}
                        disabled={issue.isPending || collect.isPending}
                        onClick={(event) => {
                          invoker.current = event.currentTarget;
                          select(user.userId);
                        }}
                      >
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </tbody>
            </Table>
          </div>
          {!accounts.data.items.length && <p className="text-sm">No user accounts found.</p>}
          <nav aria-label="Account pagination" className="flex items-center gap-3">
            <Button
              variant="outline"
              disabled={page === 1}
              onClick={() => setPage((value) => value - 1)}
            >
              Previous
            </Button>
            <span className="text-sm">
              Page {page} · {accounts.data.totalCount} accounts
            </span>
            <Button
              variant="outline"
              disabled={page * 20 >= accounts.data.totalCount}
              onClick={() => setPage((value) => value + 1)}
            >
              Next
            </Button>
          </nav>
        </>
      )}
      <Sheet
        open={overlay === 'detail'}
        onOpenChange={(open) => {
          if (!open) {
            setDevelopmentLink(null);
            collect.reset();
            changeOverlay(null);
          }
        }}
        onCloseAutoFocus={restoreFocus}
        title="Account details"
        description="Inspect this account without leaving the user list."
      >
        {selected && overlay === 'detail' && (
          <div>
            {notice && (
              <div role="status">
                <Alert intent="success" title="Account administration">
                  {notice}
                </Alert>
              </div>
            )}
            {detail.isPending ? (
              <Spinner label="Loading account details" />
            ) : detail.isError ? (
              <div role="alert">
                <Alert intent="danger" title="Account unavailable">
                  {errorMessage(detail.error)}
                </Alert>
              </div>
            ) : (
              account && (
                <>
                  <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
                    <div>
                      <dt className="text-muted-foreground">Account status</dt>
                      <dd>
                        {account.isActive ? 'Active' : 'Disabled'}
                        {account.isLockedOut ? ' · Locked out' : ''}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Credential status</dt>
                      <dd>
                        {!account.credentialEstablished
                          ? 'Activation pending'
                          : account.requiresPasswordChange
                            ? 'Password change required'
                            : 'Established'}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Username</dt>
                      <dd>{account.userName}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Delivery email</dt>
                      <dd>{account.email || 'Not configured'}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Assigned roles</dt>
                      <dd>{account.roles.join(', ')}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Employee link</dt>
                      <dd className="break-all">{account.employeeId || 'Not linked'}</dd>
                    </div>
                  </dl>
                  {issue.isError && (
                    <div role="alert" className="mt-4">
                      <Alert intent="danger" title="Credential delivery not initiated">
                        {errorMessage(issue.error)}
                      </Alert>
                    </div>
                  )}
                  <Button
                    className="mt-4"
                    variant="outline"
                    loading={issue.isPending}
                    disabled={
                      collect.isPending ||
                      !account.isActive ||
                      !account.email ||
                      (account.credentialEstablished && !account.emailConfirmed)
                    }
                    onClick={() => {
                      setNotice('');
                      setDevelopmentLink(null);
                      issue.reset();
                      setConfirmation('issue');
                      changeOverlay('confirm');
                    }}
                  >
                    {account.credentialEstablished
                      ? 'Initiate password recovery'
                      : 'Reissue activation'}
                  </Button>
                  {(!account.email ||
                    (account.credentialEstablished && !account.emailConfirmed)) && (
                    <p className="mt-2 text-sm text-muted-foreground">
                      Verified-email recovery is unavailable for this account. No recovery bypass is
                      offered.
                    </p>
                  )}
                  {import.meta.env.DEV && (
                    <div className="mt-4 space-y-3 border-t border-border pt-4">
                      <p className="text-sm text-muted-foreground">
                        Development-only handoff: collect the transient activation/recovery link
                        after issuance. Requires the existing explicitly enabled Development
                        delivery provider. Collection consumes the delivery; the link stays in
                        memory only.
                      </p>
                      <Button
                        variant="outline"
                        loading={collect.isPending}
                        disabled={!account.isActive || !!developmentLink || issue.isPending}
                        onClick={() => {
                          collect.reset();
                          setConfirmation('collect');
                          changeOverlay('confirm');
                        }}
                      >
                        Collect Development delivery link
                      </Button>
                      {collect.isError && (
                        <div role="alert">
                          <Alert intent="warning" title="No Development delivery collected">
                            {errorMessage(collect.error)}
                          </Alert>
                        </div>
                      )}
                      {developmentLink && (
                        <p className="text-sm">
                          <a
                            className="text-primary underline"
                            href={developmentLink}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Open private activation/recovery link
                          </a>
                          <span className="mt-2 block text-muted-foreground">
                            Share privately with the intended account owner only. Do not include it
                            in reports or screenshots.
                          </span>
                        </p>
                      )}
                    </div>
                  )}
                </>
              )
            )}
          </div>
        )}
      </Sheet>
      <AlertDialog
        open={overlay === 'confirm'}
        onOpenChange={(open) => {
          if (!open && !issue.isPending && !collect.isPending) changeOverlay('detail');
        }}
        onCloseAutoFocus={restoreFocus}
        title={
          confirmation === 'collect'
            ? 'Collect Development delivery?'
            : account?.credentialEstablished
              ? 'Initiate password recovery?'
              : 'Reissue activation?'
        }
        description={
          confirmation === 'collect'
            ? `Collect the one-time transient delivery for ${account?.userName ?? 'this account'}? The delivery is consumed on collection. Share the resulting link privately with its intended owner only.`
            : `Issue ${account?.credentialEstablished ? 'password recovery' : 'a replacement activation invitation'} for ${account?.userName ?? 'this account'} through the configured delivery channel? ${account?.credentialEstablished ? 'Existing sessions remain active until successful password reset.' : 'Previous activation invitations will become invalid.'}`
        }
        loading={issue.isPending || collect.isPending}
        closeOnConfirm={false}
        variant="primary"
        confirmLabel={confirmation === 'collect' ? 'Collect delivery' : 'Issue instructions'}
        onConfirm={() => {
          if (account) {
            if (confirmation === 'collect') collect.mutate(account);
            else issue.mutate(account);
          }
        }}
      >
        {(confirmation === 'collect' ? collect.isError : issue.isError) && (
          <div role="alert">
            <Alert intent="danger" title="Action not completed">
              {errorMessage((confirmation === 'collect' ? collect.error : issue.error)!)}
            </Alert>
          </div>
        )}
      </AlertDialog>
    </div>
  );
}
