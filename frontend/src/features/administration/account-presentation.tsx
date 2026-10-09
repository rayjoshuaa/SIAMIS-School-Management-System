import { Badge } from '../../components/ui/feedback';
import './accounts.css';

/** Display existing assignments only; authorization continues to use capabilities. */
export function AssignedRoles({ roles }: { roles: string[] }) {
  return (
    <span className="account-role-list">
      {roles.length ? roles.map((role) => <Badge key={role}>{role}</Badge>) : 'None'}
    </span>
  );
}
