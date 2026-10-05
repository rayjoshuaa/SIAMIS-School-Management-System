import { useState, type ReactNode } from 'react';
import { NavigationSessionProvider } from '../../lib/auth/navigation-session';
const previewCapabilities: Record<string, string[]> = {
  full: [
    'Reporting.Read',
    'Employee.Read',
    'Leave.Read',
    'Attendance.Read',
    'Payroll.Read',
    'HRDocuments.Read',
    'Security.Manage',
  ],
  people: ['Employee.Read'],
  none: [],
};
export default function DevelopmentNavigation({ children }: { children: ReactNode }) {
  const [preview, setPreview] = useState('full');
  return (
    <NavigationSessionProvider
      value={{
        userName: 'Development preview',
        mode: 'development',
        capabilities: previewCapabilities[preview] ?? [],
        preview,
        changePreview: setPreview,
      }}
    >
      {children}
    </NavigationSessionProvider>
  );
}
