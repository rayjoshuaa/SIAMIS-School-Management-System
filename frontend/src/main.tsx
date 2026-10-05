import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Providers } from './app/providers/providers';
import { AppRouter } from './app/router/router';
import { ErrorBoundary } from './components/shared/error-boundary';
import './app/styles/tokens.css';
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <Providers>
        <AppRouter />
      </Providers>
    </ErrorBoundary>
  </StrictMode>,
);
