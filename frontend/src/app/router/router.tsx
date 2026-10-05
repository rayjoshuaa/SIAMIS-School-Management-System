import { lazy, Suspense } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { ShellError, ShellLoading, ShellNotFound } from '../../components/layout/shell-states';
import { ModulePlaceholder } from '../../features/shell/placeholder';
import { SchoolDashboard } from '../../features/shell/dashboard';
import { routes, paths } from './navigation';
const Showcase = import.meta.env.DEV
  ? lazy(() => import('../../features/design-system/showcase'))
  : null;
const ShellEntry = lazy(() => import('../../components/layout/shell-entry'));
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
      <Suspense fallback={<ShellLoading />}>
        <ShellEntry />
      </Suspense>
    ),
    errorElement: <ShellError />,
    children: [
      ...routes.map((route) => ({
        path: route.path,
        element: route.path === paths.dashboard ? <SchoolDashboard /> : <ModulePlaceholder />,
      })),
      { path: '*', element: <ShellNotFound /> },
    ],
  },
]);
export function AppRouter() {
  return <RouterProvider router={router} />;
}
