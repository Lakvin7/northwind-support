import { useCallback, useEffect, useState } from 'react';
import { authApi } from '../api/auth';
import type { AuthenticatedAgent } from '../types/api';
import {
  clearAuthSession,
  onAuthSessionCleared,
  readAuthSession,
  writeAuthSession,
  type StoredAuthSession,
} from './authStorage';
import { AuthContext } from './useAuth';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<StoredAuthSession | null>(() => readAuthSession());
  const [agent, setAgent] = useState<AuthenticatedAgent | null>(null);
  const [isInitializing, setIsInitializing] = useState(session !== null);

  const forgetSession = useCallback(() => {
    setSession(null);
    setAgent(null);
    setIsInitializing(false);
  }, []);

  const logout = useCallback(() => {
    clearAuthSession();
    forgetSession();
  }, [forgetSession]);

  useEffect(() => onAuthSessionCleared(forgetSession), [forgetSession]);

  useEffect(() => {
    if (!session || agent) {
      return;
    }

    let cancelled = false;

    void authApi
      .getCurrentAgent()
      .then((currentAgent) => {
        if (!cancelled) {
          setAgent(currentAgent);
          setIsInitializing(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          logout();
        }
      });

    return () => {
      cancelled = true;
    };
  }, [agent, logout, session]);

  useEffect(() => {
    if (!session) {
      return;
    }

    const millisecondsUntilExpiry = Date.parse(session.expiresAtUtc) - Date.now();
    const timer = window.setTimeout(logout, Math.max(0, millisecondsUntilExpiry));
    return () => window.clearTimeout(timer);
  }, [logout, session]);

  const login = useCallback(async (email: string, password: string) => {
    const response = await authApi.login({ email, password });
    const authenticatedSession: StoredAuthSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
    };

    writeAuthSession(authenticatedSession);
    setSession(authenticatedSession);
    setAgent(response.agent);
    setIsInitializing(false);
  }, []);

  return (
    <AuthContext.Provider value={{ agent, isInitializing, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
