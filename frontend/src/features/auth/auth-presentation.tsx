import type { ReactNode } from 'react';
import { CircleAlert, CircleCheck, Info, Unplug } from 'lucide-react';
import { Alert } from '../../components/ui/feedback';
import schoolLogo from '../../assets/branding/siam-international-school-logo.png';

export function AuthBrand() {
  return (
    <header className="auth-brand">
      <img
        src={schoolLogo}
        alt="Siam International School crest"
        className="auth-school-logo"
        width={1536}
        height={1024}
      />
      <div>
        <p className="auth-school-name">SIAM International School</p>
        <p className="auth-system-name">School Management System</p>
      </div>
    </header>
  );
}

export function AuthHeader({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="auth-header">
      <h1>{title}</h1>
      {children && <p>{children}</p>}
    </div>
  );
}

export function AuthStatus({
  intent,
  children,
}: {
  intent: 'error' | 'connection' | 'warning' | 'success' | 'info';
  children: ReactNode;
}) {
  const Icon =
    intent === 'error' || intent === 'warning'
      ? CircleAlert
      : intent === 'connection'
        ? Unplug
        : intent === 'success'
          ? CircleCheck
          : Info;
  const title =
    intent === 'warning'
      ? 'Please wait before retrying'
      : intent === 'error'
        ? 'Request not completed'
        : intent === 'connection'
          ? 'Service unavailable'
          : intent === 'success'
            ? 'Request completed'
            : 'Account access';
  return (
    <div
      className="auth-status"
      role={
        intent === 'error' || intent === 'connection' || intent === 'warning' ? 'alert' : 'status'
      }
    >
      <Alert
        intent={
          intent === 'error'
            ? 'danger'
            : intent === 'warning'
              ? 'warning'
              : intent === 'success'
                ? 'success'
                : 'info'
        }
        title={title}
      >
        <div className="flex items-start gap-2">
          <Icon aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <div>{children}</div>
        </div>
      </Alert>
    </div>
  );
}
