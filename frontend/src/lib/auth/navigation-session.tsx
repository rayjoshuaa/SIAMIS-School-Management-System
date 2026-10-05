import { createContext, useContext, type ReactNode } from 'react';
export type NavigationSession = {
  userName: string;
  capabilities: readonly string[];
  mode: 'development' | 'unconnected';
  preview?: string;
  changePreview?: (value: string) => void;
};
const context = createContext<NavigationSession>({
  userName: 'Session not connected',
  capabilities: [],
  mode: 'unconnected',
});
export function NavigationSessionProvider({
  value,
  children,
}: {
  value: NavigationSession;
  children: ReactNode;
}) {
  return <context.Provider value={value}>{children}</context.Provider>;
}
// eslint-disable-next-line react-refresh/only-export-components
export const useNavigationSession = () => useContext(context);
