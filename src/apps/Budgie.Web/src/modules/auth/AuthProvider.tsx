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

// Runs the settings check for `token` and publishes the result once it settles.
// Declared outside the effect so the effect body only wires up the subscription.
const resolveSettings = async (
  token: string,
  checkSettings: () => Promise<void>,
  publish: (token: string) => void,
  isCancelled: () => boolean
) => {
  await checkSettings();
  if (!isCancelled()) publish(token);
};

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(() => localStorage.getItem('token'));
  const [user, setUser] = useState<{ id: string; name: string; email: string } | null>(
    () => decodeUser(localStorage.getItem('token'))
  );
  const [isConfigured, setIsConfigured] = useState<boolean>(false);
  // Token whose settings have already been resolved. `undefined` = not resolved yet.
  // `isLoading` is derived from it so the effect never calls setState synchronously.
  const [resolvedToken, setResolvedToken] = useState<string | null | undefined>(undefined);
  const navigate = useNavigate();

  const isLoading = token !== null && resolvedToken !== token;

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
  }, [setToken, navigate]);

  useEffect(() => {
    if (!token) {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      return;
    }

    // Only the latest run may publish its result, so a token change mid-flight
    // does not leave the provider stuck on the loading state.
    let cancelled = false;
    void resolveSettings(token, checkSettings, setResolvedToken, () => cancelled);
    return () => {
      cancelled = true;
    };
  }, [token, checkSettings]);

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
