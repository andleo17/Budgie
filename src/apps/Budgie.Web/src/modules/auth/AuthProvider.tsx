import { useState, useEffect, useCallback, startTransition } from 'react';
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

  const setToken = useCallback((newToken: string | null) => {
    if (newToken) {
      localStorage.setItem('token', newToken);
      startTransition(() => setIsLoading(true));
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
            navigate('/setup');
          }
        } else if (response?.status === 401) {
          setToken(null);
        }
        return;
      }

      startTransition(() => setIsConfigured(data.isConfigured));
      if (!data.isConfigured && window.location.pathname !== '/setup') {
        navigate('/setup');
      } else if (data.isConfigured && window.location.pathname === '/setup') {
        navigate('/dashboard');
      }
    } catch {
      // ignore
    }
  }, [navigate, setToken]);

  useEffect(() => {
    if (token) {
      startTransition(() => setIsLoading(true));
      setTimeout(() =>
        checkSettings().finally(() => startTransition(() => setIsLoading(false))),
        0
      );
    } else {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      startTransition(() => setIsLoading(false));
    }
  }, [token, navigate, checkSettings]);

  const logout = useCallback(() => {
    setToken(null);
    navigate('/login');
  }, [setToken, navigate]);

  return (
    <AuthContext.Provider value={{ token, setToken, user, isConfigured, logout, isLoading, checkSettings }}>
      {children}
    </AuthContext.Provider>
  );
}
