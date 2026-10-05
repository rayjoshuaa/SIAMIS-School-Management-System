import { lazy, Suspense } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { PageContainer, PageTitle, PageDescription } from '../../components/layout/page';
import { Spinner } from '../../components/ui/feedback';
const Showcase = import.meta.env.DEV
  ? lazy(() => import('../../features/design-system/showcase'))
  : null;
function Foundation() {
  return (
    <PageContainer>
      <PageTitle>SIAMIS</PageTitle>
      <PageDescription>
        Frontend foundation. Application workflows will follow in separately approved checkpoints.
      </PageDescription>
      {import.meta.env.DEV && (
        <a className="mt-6 inline-flex min-h-11 items-center text-primary underline" href="/dev/ui">
          Open design system
        </a>
      )}
    </PageContainer>
  );
}
const router = createBrowserRouter([
  { path: '/', element: <Foundation /> },
  ...(Showcase
    ? [
        {
          path: '/dev/ui',
          element: (
            <Suspense
              fallback={
                <PageContainer>
                  <Spinner label="Loading design system" />
                </PageContainer>
              }
            >
              <Showcase />
            </Suspense>
          ),
        },
      ]
    : []),
  {
    path: '*',
    element: (
      <PageContainer>
        <PageTitle>Page not found</PageTitle>
        <a className="inline-flex min-h-11 items-center text-primary underline" href="/">
          Return to SIAMIS
        </a>
      </PageContainer>
    ),
  },
]);
export function AppRouter() {
  return <RouterProvider router={router} />;
}
