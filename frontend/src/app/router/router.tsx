import { lazy, Suspense } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { ShellError, ShellLoading, ShellNotFound } from '../../components/layout/shell-states';
import { ModulePlaceholder } from '../../features/shell/placeholder';
import { SchoolDashboard } from '../../features/shell/dashboard';
import { HrDashboard } from '../../features/hr/dashboard';
import { AuthProvider } from '../providers/auth-provider';
import {
  SessionBoundary,
  ProtectedRoutes,
  CapabilityRoute,
} from '../../features/auth/auth-boundaries';
import { AuthLayout } from '../../features/auth/auth-layout';
import { captureCredentialLink } from '../../lib/auth/credential-link';
const AuthForm = lazy(() => import('../../features/auth/auth-form'));
function form(mode: 'login' | 'activate' | 'forgot' | 'reset' | 'change') {
  return (
    <Suspense fallback={<ShellLoading />}>
      <AuthForm key={mode} mode={mode} />
    </Suspense>
  );
}
import { routes, paths } from './navigation';
const Showcase = import.meta.env.DEV
  ? lazy(() => import('../../features/design-system/showcase'))
  : null;
const ShellEntry = lazy(() => import('../../components/layout/shell-entry'));
const UserAccounts = lazy(() =>
  import('../../features/administration/user-accounts').then((module) => ({
    default: module.UserAccounts,
  })),
);
const EmployeeDirectory = lazy(() =>
  import('../../features/employees/directory').then((module) => ({
    default: module.EmployeeDirectory,
  })),
);
const EmployeeProfile = lazy(() =>
  import('../../features/employees/profile').then((module) => ({
    default: module.EmployeeProfile,
  })),
);
const EmployeeForm = lazy(() =>
  import('../../features/employees/form').then((module) => ({ default: module.EmployeeForm })),
);
const AttendanceWorkspace = lazy(() =>
  import('../../features/attendance/workspace').then((module) => ({
    default: module.AttendanceWorkspace,
  })),
);
const AttendanceReview = lazy(() =>
  import('../../features/attendance/review').then((module) => ({
    default: module.AttendanceReview,
  })),
);
const MyAttendance = lazy(() =>
  import('../../features/clocking/my-attendance').then((module) => ({
    default: module.MyAttendance,
  })),
);
captureCredentialLink();
const router = createBrowserRouter([
  ...(Showcase
    ? [
        {
          path: paths.designSystem,
          element: (
            <Suspense fallback={<ShellLoading />}>
              <Showcase />
            </Suspense>
          ),
        },
      ]
    : []),
  {
    element: (
      <AuthProvider>
        <SessionBoundary />
      </AuthProvider>
    ),
    children: [
      {
        element: <AuthLayout />,
        children: [
          { path: '/login', element: form('login') },
          { path: '/activate', element: form('activate') },
          { path: '/forgot-password', element: form('forgot') },
          { path: '/reset-password', element: form('reset') },
          { path: '/change-password', element: form('change') },
        ],
      },
      {
        element: <ProtectedRoutes />,
        children: [
          {
            element: (
              <Suspense fallback={<ShellLoading />}>
                <ShellEntry />
              </Suspense>
            ),
            errorElement: <ShellError />,
            children: [
              {
                element: <CapabilityRoute />,
                children: [
                  ...routes.map((route) => ({
                    path: route.path,
                    element:
                      route.path === paths.dashboard ? (
                        <SchoolDashboard />
                      ) : route.path === paths.hr ? (
                        <HrDashboard />
                      ) : route.path === '/hr/security' ? (
                        <UserAccounts />
                      ) : route.path === '/hr/employees' ? (
                        <EmployeeDirectory />
                      ) : route.path === '/hr/attendance' ? (
                        <AttendanceWorkspace />
                      ) : route.path === '/self/attendance' ? (
                        <MyAttendance />
                      ) : (
                        <ModulePlaceholder />
                      ),
                  })),
                  { path: '/hr/employees/new', element: <EmployeeForm mode="create" /> },
                  { path: '/hr/attendance/:employeeId/:date', element: <AttendanceReview /> },
                  { path: '/hr/employees/:employeeId', element: <EmployeeProfile /> },
                  { path: '/hr/employees/:employeeId/edit', element: <EmployeeForm mode="edit" /> },
                  {
                    path: '/hr/employees/:employeeId/employment-change',
                    element: <EmployeeForm mode="employment-change" />,
                  },
                  {
                    path: '/hr/employees/:employeeId/end-employment',
                    element: <EmployeeForm mode="end-employment" />,
                  },
                  {
                    path: '/hr/employees/:employeeId/rehire',
                    element: <EmployeeForm mode="rehire" />,
                  },
                  { path: '*', element: <ShellNotFound /> },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
]);
export function AppRouter() {
  return <RouterProvider router={router} />;
}
