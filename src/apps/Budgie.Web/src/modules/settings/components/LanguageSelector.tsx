import { Languages } from 'lucide-react'
import { useLanguage } from '@/i18n/useLanguage'
import { useTranslation } from 'react-i18next'

const languages = [
  { code: 'en', labelKey: 'sidebar.english' },
  { code: 'es', labelKey: 'sidebar.spanish' },
] as const

export function LanguageSelector() {
  const { t } = useTranslation()
  const { changeLanguage, currentLanguage } = useLanguage()
  return (
    <div className="space-y-1">
      {languages.map(lang => (
        <button key={lang.code} type="button" onClick={() => changeLanguage(lang.code)}
          className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition-colors cursor-pointer ${
            currentLanguage.startsWith(lang.code) ? 'bg-primary/10 text-primary font-medium' : 'hover:bg-muted text-foreground'
          }`}>
          <Languages className="size-4 shrink-0 text-muted-foreground" />
          <span>{t(lang.labelKey)}</span>
          {currentLanguage.startsWith(lang.code) && <span className="ml-auto text-xs text-primary">{t('settings.active')}</span>}
        </button>
      ))}
    </div>
  )
}
