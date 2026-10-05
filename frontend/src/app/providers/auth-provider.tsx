import { useState, useRef, useEffect, useCallback, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { AuthContext, type AuthState } from '../../lib/auth/auth-context';
import { readSession } from '../../lib/auth/contracts';
import { advanceSession, onSessionLoss } from '../../lib/auth/session-events';
export function AuthProvider({ children }: { children: ReactNode }) {
  const cache = useQueryClient();
  const [state, setState] = useState<AuthState>({ status: 'bootstrapping' });
  const stateRef = useRef(state);
  useEffect(() => {
    stateRef.current = state;
  }, [state]);
  const sequence = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const clear = useCallback(() => {
    advanceSession();
    void cache.cancelQueries();
    cache.clear();
  }, [cache]);
  const refresh = useCallback(async () => {
    const wasAuthenticated = stateRef.current.status === 'authenticated';
    controller.current?.abort();
    const abort = new AbortController();
    controller.current = abort;
    const current = ++sequence.current;
    setState({ status: 'bootstrapping' });
    try {
      const user = readSession(await api('/api/auth/me', { signal: abort.signal }));
      if (current !== sequence.current || abort.signal.aborted) return;
      clear();
      setState(user.isActive ? { status: 'authenticated', user } : { status: 'expired' });
    } catch (error) {
      if (current !== sequence.current || abort.signal.aborted) return;
      const rejected = error instanceof ApiError && error.status === 401;
      clear();
      setState({ status: rejected ? (wasAuthenticated ? 'expired' : 'anonymous') : 'error' });
    }
  }, [clear]);
  useEffect(() => {
    const unsubscribe = onSessionLoss(() => {
      if (stateRef.current.status !== 'authenticated') return;
      ++sequence.current;
      controller.current?.abort();
      clear();
      setState({ status: 'expired' });
    });
    void Promise.resolve().then(() => {
      if (!disposed) return refresh();
    });
    let disposed = false;
    const onFocus = () => {
      if (stateRef.current.status === 'authenticated') void refresh();
    };
    window.addEventListener('focus', onFocus);
    const timer = window.setInterval(onFocus, 5 * 60 * 1000);
    return () => {
      disposed = true;
      controller.current?.abort();
      unsubscribe();
      window.removeEventListener('focus', onFocus);
      window.clearInterval(timer);
    };
  }, [refresh, clear]);
  async function login(userName: string, password: string) {
    await api('/api/auth/login', { method: 'POST', body: { userName, password } });
    clear();
    await refresh();
  }
  async function logout() {
    try {
      await api('/api/auth/logout', { method: 'POST' });
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) throw error;
    }
    ++sequence.current;
    controller.current?.abort();
    clear();
    setState({ status: 'anonymous' });
  }
  return (
    <AuthContext.Provider value={{ state, refresh, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
