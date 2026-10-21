import { createContext, useContext } from 'react';
import type { AuthenticatedAgent } from '../types/api';

export interface AuthContextValue {
  agent: AuthenticatedAgent | null;
  isInitializing: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }

  return context;
}
