import { Outlet, Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AuthBrand } from './auth-presentation';
import campusPhoto from '../../assets/branding/siamis-campus-login.png';
import './auth-layout.css';
export function AuthLayout() {
  return (
    <AuthSurface>
      <Outlet />
    </AuthSurface>
  );
}
export function AuthSurface({ children }: { children: ReactNode }) {
  return (
    <main className="auth-page">
      <div className="auth-campus" aria-hidden="true">
        <img src={campusPhoto} alt="" className="auth-campus-photo" />
      </div>
      <div className="auth-panel">
        <div className="auth-task">
          <AuthBrand />
          {children}
        </div>
      </div>
    </main>
  );
}
export function LoginLink() {
  return (
    <Link to="/login" className="auth-secondary-link auth-back-link">
      Back to sign in
    </Link>
  );
}
