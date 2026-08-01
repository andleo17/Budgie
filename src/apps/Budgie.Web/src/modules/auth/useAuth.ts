import { createContext, useContext } from 'react';

export interface AuthContextType {
  token: string | null;
  setToken: (token: string | null) => void;
  user: { id: string; name: string; email: string } | null;
  isConfigured: boolean;
  logout: () => void;
  isLoading: boolean;
  checkSettings: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
