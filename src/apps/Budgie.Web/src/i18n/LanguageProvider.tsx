import { useEffect } from 'react';
import type { ReactNode } from 'react';
import i18n from './config';
import { useAuth } from '@/modules/auth/useAuth';
import client from '@/lib/api-client';

export function LanguageProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth();

  useEffect(() => {
    if (!token) return;
    client.GET('/api/users/settings')
      .then(({ data }) => {
        const lang = data?.language;
        if (lang && lang !== i18n.language) {
          i18n.changeLanguage(lang);
        }
      })
      .catch(() => {});
  }, [token]);

  return <>{children}</>;
}
