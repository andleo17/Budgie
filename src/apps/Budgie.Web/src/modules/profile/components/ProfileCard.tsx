import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import client from '@/lib/api-client'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Loader2, Pencil, X, Check } from 'lucide-react'
import { getInitials } from '@/lib/utils'
import type { components } from '@/lib/api-types'
import { profileSchema, type ProfileFormValues } from '@/modules/profile/schemas'

interface ProfileCardProps {
  profileData: components["schemas"]["ProfileResponse"] | undefined
}

export function ProfileCard({ profileData }: ProfileCardProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)

  const updateMutation = useMutation({
    mutationFn: async (body: { name: string }) => {
      const { error } = await client.PUT('/api/users/profile', { body })
      if (error) throw error
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profile'] })
      setEditing(false)
    },
  })

  const { control, handleSubmit, reset, formState: { isValid } } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { name: '' },
  })

  useEffect(() => {
    if (profileData?.name) {
      reset({ name: profileData.name })
    }
  }, [profileData, reset])

  const onSubmit = (values: ProfileFormValues) => {
    updateMutation.mutate({ name: values.name })
  }

  const handleCancel = () => {
    reset({ name: profileData?.name || '' })
    setEditing(false)
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-4">
          <Avatar className="size-14 border border-border">
            <AvatarFallback className="text-lg">{getInitials(profileData?.name ?? '')}</AvatarFallback>
          </Avatar>
          <div>
            <CardTitle>{profileData?.name}</CardTitle>
            <CardDescription>{profileData?.email}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <form onSubmit={handleSubmit(onSubmit)}>
          <div className="grid gap-2">
            <Label htmlFor="name">{t('profile.name')}</Label>
            <div className="flex gap-2">
              <Controller
                name="name"
                control={control}
                render={({ field }) => (
                  <Input id="name" {...field} disabled={!editing} className={editing ? '' : 'opacity-70'} />
                )}
              />
            </div>
          </div>

          <div className="grid gap-2 mt-4">
            <Label>{t('profile.email')}</Label>
            <Input value={profileData?.email || ''} disabled className="opacity-70" />
          </div>

          <div className="flex gap-2 pt-4">
            {editing ? (
              <>
                <Button type="submit" disabled={updateMutation.isPending || !isValid}>
                  {updateMutation.isPending ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
                  {t('profile.save')}
                </Button>
                <Button variant="outline" type="button" onClick={handleCancel}>
                  <X className="size-4" />
                  {t('profile.cancel')}
                </Button>
              </>
            ) : (
              <Button type="button" onClick={() => setEditing(true)}>
                <Pencil className="size-4" />
                {t('profile.edit')}
              </Button>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
