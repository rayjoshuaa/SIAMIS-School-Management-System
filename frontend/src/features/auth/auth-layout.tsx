import { Outlet, Link } from 'react-router-dom';
import { AuthBrand } from './auth-presentation';
import campusPhoto from '../../assets/branding/siamis-campus-login.png';
import './auth-layout.css';
export function AuthLayout() {
  return (
    <main className="auth-page">
      <div className="auth-campus" aria-hidden="true">
        <img src={campusPhoto} alt="" className="auth-campus-photo" />
      </div>
      <div className="auth-panel">
        <div className="auth-task">
          <AuthBrand />
          <Outlet />
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
