import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import client from '@/lib/api-client';
import { ProfileCard } from '@/modules/profile/components/ProfileCard';

export default function Profile() {
  const { t } = useTranslation();

  const { data: profileData } = useQuery({
    queryKey: ['profile'],
    queryFn: async () => {
      const { data, error } = await client.GET('/api/users/profile')
      if (error) throw error
      return data
    },
  });

  return (
    <div className="w-full space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{t('profile.title')}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t('profile.description')}</p>
      </div>

      <ProfileCard profileData={profileData} />
    </div>
  );
}
