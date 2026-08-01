import { Target, Plus } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export function GoalsEmptyState({ onCreate }: { onCreate: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed bg-muted/10 py-24 text-center">
      <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/10 ring-1 ring-primary/20">
        <Target className="h-8 w-8 text-primary" />
      </div>
      <h2 className="text-xl font-semibold mb-2">{t('goals.noGoals')}</h2>
      <p className="text-muted-foreground text-sm max-w-xs mb-6">{t('goals.noGoalsDesc')}</p>
      <button onClick={onCreate} className="inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-lg border border-transparent bg-primary px-2.5 text-sm font-medium text-primary-foreground transition-all hover:bg-primary/80 active:translate-y-px select-none whitespace-nowrap outline-none">
        <Plus className="h-4 w-4" />
        {t('goals.createFirstGoal')}
      </button>
    </div>
  )
}
