import { useState, useEffect, useCallback, useRef, startTransition } from 'react';
import type { ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';
import client from '@/lib/api-client';
import { useNavigate } from 'react-router-dom';
import { AuthContext } from '@/modules/auth/useAuth';
import type { AuthContextType } from '@/modules/auth/useAuth';

export type { AuthContextType };

interface JwtPayload {
  sub: string;
  email: string;
  name: string;
  exp?: number;
}

function decodeUser(token: string | null): { id: string; name: string; email: string } | null {
  if (!token) return null;
  try {
    const decoded = jwtDecode<JwtPayload>(token);
    return {
      id: decoded.sub,
      name: decoded.name || decoded.sub,
      email: decoded.email,
    };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(() => localStorage.getItem('token'));
  const [user, setUser] = useState<{ id: string; name: string; email: string } | null>(
    () => decodeUser(localStorage.getItem('token'))
  );
  const [isConfigured, setIsConfigured] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const navigate = useNavigate();

  // Keep a stable reference to navigate so the settings check only runs
  // on token changes, not on every render (avoiding redundant HTTP calls
  // and full-screen "Loading..." flashes while navigating).
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  const setToken = useCallback((newToken: string | null) => {
    if (newToken) {
      localStorage.setItem('token', newToken);
    } else {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
    }
    setTokenState(newToken);
    setUser(decodeUser(newToken));
  }, []);

  const checkSettings = useCallback(async () => {
    try {
      const { data, response } = await client.GET('/api/users/settings');
      if (!data || !response.ok) {
        if (response?.status === 403) {
          startTransition(() => setIsConfigured(false));
          if (window.location.pathname !== '/setup') {
            navigateRef.current('/setup');
          }
        } else if (response?.status === 401) {
          setToken(null);
        }
        return;
      }

      startTransition(() => setIsConfigured(data.isConfigured));
      if (!data.isConfigured && window.location.pathname !== '/setup') {
        navigateRef.current('/setup');
      } else if (data.isConfigured && window.location.pathname === '/setup') {
        navigateRef.current('/dashboard');
      }
    } catch {
      // ignore
    }
  }, [setToken]);

  useEffect(() => {
    if (token) {
      setIsLoading(true);
      checkSettings().finally(() => setIsLoading(false));
    } else {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      setIsLoading(false);
    }
  }, [token, checkSettings]);

  const logout = useCallback(() => {
    setToken(null);
    navigateRef.current('/login');
  }, [setToken]);

  return (
    <AuthContext.Provider value={{ token, setToken, user, isConfigured, logout, isLoading, checkSettings }}>
      {children}
    </AuthContext.Provider>
  );
}
