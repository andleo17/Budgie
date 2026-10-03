import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import client from '@/lib/api-client';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ThemeToggleCard } from '@/modules/settings/components/ThemeToggleCard';
import { LanguageSelector } from '@/modules/settings/components/LanguageSelector';
import { CurrencySettingsForm } from '@/modules/settings/components/CurrencySettingsForm';
import { TransactionSettingsCard } from '@/modules/settings/components/TransactionSettingsCard';
import { RecurringSettingsCard } from '@/modules/settings/components/RecurringSettingsCard';
import { BackupCard } from '@/modules/settings/components/BackupCard';
import { EmailImportSettingsCard } from '@/modules/settings/components/EmailImportSettingsCard';

export default function Settings() {
  const { t } = useTranslation();
  const { data: settingsData } = useQuery({
    queryKey: ['settings'],
    queryFn: async () => {
      const { data, error } = await client.GET('/api/users/settings')
      if (error) throw error
      return data
    },
  });

  const pageSize = settingsData?.transactionPageSize ?? 100;

  return (
    <div className="w-full space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{t('settings.title')}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t('settings.description')}</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('settings.appearance.title')}</CardTitle>
          <CardDescription>{t('settings.appearance.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <ThemeToggleCard />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('settings.language.title')}</CardTitle>
          <CardDescription>{t('settings.language.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-1">
          <LanguageSelector />
        </CardContent>
      </Card>

      <CurrencySettingsForm settingsData={settingsData} />
      <TransactionSettingsCard currentPageSize={pageSize} />
      <RecurringSettingsCard />
      <EmailImportSettingsCard />
      <BackupCard />
    </div>
  );
}
