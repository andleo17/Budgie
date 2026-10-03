import { LayoutDashboard, Wallet, ArrowLeftRight, Target, Tags, Users, FileText, FileUp, Mail } from 'lucide-react'

export const MAIN_NAV_ITEMS = [
  { key: 'dashboard', path: '/dashboard', icon: LayoutDashboard },
  { key: 'accounts', path: '/accounts', icon: Wallet },
  { key: 'transactions', path: '/transactions', icon: ArrowLeftRight },
  { key: 'templates', path: '/templates', icon: FileText },
  { key: 'categories', path: '/categories', icon: Tags },
  { key: 'goals', path: '/goals', icon: Target },
  { key: 'persons', path: '/persons', icon: Users },
  { key: 'import', path: '/import', icon: FileUp },
  { key: 'emailImport', path: '/email-import', icon: Mail },
] as const
