import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/modules/auth/useAuth'
import { useTheme } from '@/modules/settings/useTheme'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { getInitials } from '@/lib/utils'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  SidebarMenuButton,
} from '@/components/ui/sidebar'
import { UserIcon, UserRound, Moon, Sun, Settings, LogOut, ChevronUp } from 'lucide-react'

interface UserDropdownMenuProps {
  user: { id: string; name: string; email: string } | null
}

export function UserDropdownMenu({ user }: UserDropdownMenuProps) {
  const { t } = useTranslation()
  const { logout } = useAuth()
  const { theme, toggleTheme } = useTheme()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <SidebarMenuButton size="lg" tooltip={user?.name || t('sidebar.user')} />
        }
      >
        <Avatar className="size-5 border border-border">
          <AvatarFallback className="text-xs">{user?.name ? getInitials(user.name) : 'U'}</AvatarFallback>
        </Avatar>
        <span className="truncate">{user?.name || t('sidebar.user')}</span>
        <ChevronUp className="ml-auto" />
      </DropdownMenuTrigger>
      <DropdownMenuContent
        side="right"
        align="start"
        sideOffset={8}
        className="w-(--radix-popper-anchor-width)"
      >
        <DropdownMenuItem disabled className="gap-2">
          <UserIcon />
          <span className="truncate">{user?.email || user?.name || t('sidebar.account')}</span>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <DropdownMenuItem className="gap-2" render={<Link to="/profile" />}>
          <UserRound />
          <span>{t('profile.title')}</span>
        </DropdownMenuItem>

        <DropdownMenuItem onClick={toggleTheme} className="gap-2">
          {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
          <span>{theme === 'dark' ? t('settings.appearance.lightMode') : t('settings.appearance.darkMode')}</span>
        </DropdownMenuItem>

        <DropdownMenuItem className="gap-2" render={<Link to="/settings" />}>
          <Settings />
          <span>{t('nav.settings')}</span>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <DropdownMenuItem onClick={logout} className="gap-2">
          <LogOut />
          <span>{t('sidebar.logout')}</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
