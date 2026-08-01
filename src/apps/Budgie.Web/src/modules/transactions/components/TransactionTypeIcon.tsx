import { ArrowUpRight, ArrowDownLeft, ArrowLeftRight, TrendingDown, RotateCcw } from 'lucide-react'

export const TRANSACTION_TYPES = [
  { labelKey: 'transactions.income', icon: ArrowUpRight, color: 'text-emerald-400' },
  { labelKey: 'transactions.expense', icon: ArrowDownLeft, color: 'text-red-400' },
  { labelKey: 'transactions.transfer', icon: ArrowLeftRight, color: 'text-blue-400' },
  { labelKey: 'transactions.loan', icon: TrendingDown, color: 'text-amber-400' },
  { labelKey: 'transactions.repayment', icon: RotateCcw, color: 'text-purple-400' },
] as const

export function getTypeInfo(typeCode: number) {
  return TRANSACTION_TYPES[typeCode] ?? TRANSACTION_TYPES[1]
}
