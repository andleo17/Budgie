import { TrendingUp, Target, Trophy } from 'lucide-react'
import { cn } from '@/lib/utils'
import { formatCurrency } from '@/lib/format'
import { useTranslation } from 'react-i18next'

interface GoalsStatsBarProps {
  goals: { balance: number; currency: string; targetAmount: number | null }[]
}

export function GoalsStatsBar({ goals }: GoalsStatsBarProps) {
  const { t } = useTranslation()
  const totalSaved = goals.reduce((sum, g) => sum + g.balance, 0)
  const totalTarget = goals.filter(g => g.targetAmount).reduce((sum, g) => sum + (g.targetAmount ?? 0), 0)
  const completed = goals.filter(g => g.targetAmount && g.balance >= g.targetAmount).length

  const stats = [
    { label: t('goals.totalSaved'), value: formatCurrency(totalSaved, goals[0]?.currency || 'USD'), sub: t('goals.acrossAllGoals'), icon: TrendingUp, color: 'text-emerald-400' },
    { label: t('goals.totalTarget'), value: totalTarget > 0 ? formatCurrency(totalTarget, goals[0]?.currency || 'USD') : '—', sub: t('goals.combinedTarget'), icon: Target, color: 'text-primary' },
    { label: t('goals.completed'), value: `${completed} / ${goals.length}`, sub: t('goals.goalsReached'), icon: Trophy, color: 'text-amber-400' },
  ]

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      {stats.map(s => (
        <div key={s.label} className="rounded-xl border bg-card px-4 py-3 flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-muted">
            <s.icon className={cn('h-4.5 w-4.5', s.color)} />
          </div>
          <div className="min-w-0">
            <p className="text-xs text-muted-foreground truncate">{s.label}</p>
            <p className="text-lg font-bold tabular-nums leading-tight truncate">{s.value}</p>
            <p className="text-xs text-muted-foreground/70 truncate">{s.sub}</p>
          </div>
        </div>
      ))}
    </div>
  )
}
