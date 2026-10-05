import { Component, type ReactNode } from 'react';
import { Alert } from '../ui/feedback';
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <main className="mx-auto max-w-lg p-6">
        <Alert intent="danger" title="This view could not be displayed">
          Refresh the page. If the problem continues, contact support.
        </Alert>
      </main>
    ) : (
      this.props.children
    );
  }
}
