import { createContext, useContext } from 'react';
import type { SessionUser } from './contracts';
export type AuthState = {
  status: 'bootstrapping' | 'authenticated' | 'anonymous' | 'expired' | 'error';
  user?: SessionUser;
};
export type AuthContextValue = {
  state: AuthState;
  refresh: () => Promise<void>;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
};
export const AuthContext = createContext<AuthContextValue | null>(null);
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('Auth provider is required');
  return value;
}
