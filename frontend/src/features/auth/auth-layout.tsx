import { Outlet, Link, useLocation } from 'react-router-dom';
import { Card } from '../../components/ui/feedback';
import schoolLogo from '../../assets/branding/siam-international-school-logo.png';
import campusPhoto from '../../assets/branding/siamis-campus-login.png';
import './auth-layout.css';
export function AuthLayout() {
  const isLogin = useLocation().pathname === '/login';
  const branding = (
    <header
      className={`flex flex-col items-center text-center ${isLogin ? 'text-[#182e46]' : 'text-white'}`}
    >
      <img
        src={schoolLogo}
        alt="Siam International School crest"
        className="auth-school-logo"
        width={1536}
        height={1024}
      />
      <p
        className={
          isLogin
            ? 'mt-2 text-xl font-semibold'
            : 'mt-3 text-xl font-semibold tracking-wide sm:text-2xl'
        }
      >
        SIAM International School
      </p>
      <p className={`mt-1 text-sm ${isLogin ? 'text-muted-foreground' : 'text-white/90'}`}>
        School Management System
      </p>
    </header>
  );
  return (
    <main className="auth-campus flex min-h-dvh items-center justify-center px-4 py-6 sm:px-6 sm:py-8">
      <div className="auth-campus-backdrop" aria-hidden="true">
        <img src={campusPhoto} alt="" className="auth-campus-photo" />
      </div>
      <div className={`relative w-full space-y-5 ${isLogin ? 'max-w-[26rem]' : 'max-w-md'}`}>
        {!isLogin && branding}
        <Card className={`auth-login-card ${isLogin ? 'px-6 py-6 sm:px-7' : 'p-5 sm:p-7'}`}>
          {isLogin && (
            <div className="auth-login-brand mb-4 border-b border-accent/30 pb-4">{branding}</div>
          )}
          <Outlet />
        </Card>
      </div>
    </main>
  );
}
export function LoginLink() {
  return (
    <Link to="/login" className="inline-flex min-h-11 items-center text-sm text-primary underline">
      Back to sign in
    </Link>
  );
}
