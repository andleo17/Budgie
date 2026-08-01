import { Switch } from '@/components/ui/switch'
import { Label } from '@/components/ui/label'
import { useTheme } from '@/modules/settings/useTheme'
import { useTranslation } from 'react-i18next'
import { Moon, Sun } from 'lucide-react'

export function ThemeToggleCard() {
  const { t } = useTranslation()
  const { theme, toggleTheme } = useTheme()
  return (
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-3">
        {theme === 'dark' ? <Moon className="size-4 text-muted-foreground" /> : <Sun className="size-4 text-muted-foreground" />}
        <div>
          <Label htmlFor="dark-mode" className="font-medium">{t('settings.appearance.darkMode')}</Label>
          <p className="text-xs text-muted-foreground">{t('settings.appearance.darkModeDesc')}</p>
        </div>
      </div>
      <Switch id="dark-mode" key={theme} defaultChecked={theme === 'dark'} onCheckedChange={toggleTheme} />
    </div>
  )
}
