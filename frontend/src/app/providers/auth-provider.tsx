import { useState, useRef, useEffect, useCallback, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';
import { AuthContext, type AuthState } from '../../lib/auth/auth-context';
import type { SessionUser } from '../../lib/auth/contracts';
import { readSession } from '../../lib/auth/contracts';
import {
  advanceSession,
  onSessionLoss,
  blockProtectedRequests,
} from '../../lib/auth/session-events';

function securityContext(user: SessionUser) {
  return JSON.stringify([
    user.userId,
    user.employeeId,
    user.isActive,
    user.requiresPasswordChange,
    [...new Set(user.roles)].sort(),
    [...new Set(user.capabilities)].sort(),
  ]);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const cache = useQueryClient();
  const [state, setState] = useState<AuthState>({ status: 'bootstrapping' });
  const stateRef = useRef(state);
  const publish = useCallback((next: AuthState) => {
    // Synchronize event handlers before React commits, including consecutive focus events.
    stateRef.current = next;
    setState(next);
  }, []);
  const sequence = useRef(0);
  const contextVersion = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const closing = useRef(false);
  const invalidateRefresh = useCallback(() => {
    ++sequence.current;
    controller.current?.abort();
  }, []);
  const clear = useCallback(() => {
    advanceSession();
    void cache.cancelQueries();
    cache.clear();
    contextVersion.current++;
  }, [cache]);
  const refresh = useCallback(async () => {
    if (closing.current) return;
    const previous = stateRef.current;
    const wasAuthenticated = previous.status === 'authenticated';
    controller.current?.abort();
    const abort = new AbortController();
    controller.current = abort;
    const current = ++sequence.current;
    publish(
      wasAuthenticated
        ? {
            ...previous,
            revalidation: previous.revalidation === 'unavailable' ? 'unavailable' : 'pending',
          }
        : { status: 'bootstrapping' },
    );
    try {
      const user = readSession(await api('/api/auth/me', { signal: abort.signal }));
      if (current !== sequence.current || abort.signal.aborted) return;
      if (!previous.user || securityContext(previous.user) !== securityContext(user)) clear();
      blockProtectedRequests(false);
      publish(
        user.isActive
          ? { status: 'authenticated', user, contextVersion: contextVersion.current }
          : { status: 'expired' },
      );
    } catch (error) {
      if (current !== sequence.current || abort.signal.aborted) return;
      const rejected = error instanceof ApiError && error.status === 401;
      if (wasAuthenticated && !rejected) {
        // Keep drafts mounted but inaccessible. Cancel reads; do not erase their initialized data.
        blockProtectedRequests(true);
        void cache.cancelQueries();
        publish({ ...previous, revalidation: 'unavailable' });
      } else {
        clear();
        blockProtectedRequests(false);
        publish({ status: rejected ? (wasAuthenticated ? 'expired' : 'anonymous') : 'error' });
      }
    }
  }, [cache, clear, publish]);
  useEffect(() => {
    blockProtectedRequests(false);
    const unsubscribe = onSessionLoss(() => {
      if (stateRef.current.status !== 'authenticated') return;
      invalidateRefresh();
      clear();
      blockProtectedRequests(false);
      publish({ status: 'expired' });
    });
    let disposed = false;
    void Promise.resolve().then(() => {
      if (!disposed) return refresh();
    });
    const onFocus = () => {
      if (stateRef.current.status === 'authenticated') void refresh();
    };
    window.addEventListener('focus', onFocus);
    const timer = window.setInterval(onFocus, 5 * 60 * 1000);
    return () => {
      disposed = true;
      invalidateRefresh();
      unsubscribe();
      window.removeEventListener('focus', onFocus);
      window.clearInterval(timer);
      blockProtectedRequests(false);
    };
  }, [refresh, clear, publish, invalidateRefresh]);
  async function login(userName: string, password: string) {
    await api('/api/auth/login', { method: 'POST', body: { userName, password } });
    clear();
    await refresh();
  }
  async function logout() {
    // Invalidate in-flight checks before sending logout, not only after its response.
    closing.current = true;
    invalidateRefresh();
    try {
      await api('/api/auth/logout', { method: 'POST' });
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) throw error;
    } finally {
      closing.current = false;
    }
    clear();
    blockProtectedRequests(false);
    publish({ status: 'anonymous' });
  }
  return (
    <AuthContext.Provider value={{ state, refresh, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
