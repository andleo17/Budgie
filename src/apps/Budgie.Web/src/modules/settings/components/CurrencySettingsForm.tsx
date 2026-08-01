import { useState, useEffect } from 'react'
import { useForm, Controller, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import client from '@/lib/api-client'
import type { components } from '@/lib/api-types'
import { enrichCurrencies, getCurrencies } from '@/lib/currencies'
import { CurrencySelector } from '@/components/currency-selector'
import { CurrencyCombobox } from '@/components/currency-combobox'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Loader2, Check, X } from 'lucide-react'
import { settingsSchema, type SettingsFormValues } from '@/modules/settings/schemas'

interface CurrencySettingsFormProps {
  settingsData: components["schemas"]["UserSettingsResponse"] | undefined
}

export function CurrencySettingsForm({ settingsData }: CurrencySettingsFormProps) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()

  const [supported, setSupported] = useState<string[]>(() => settingsData?.supportedCurrencies ?? [])

  const currencyOptions = enrichCurrencies(supported, i18n.language)
  const allCurrencies = getCurrencies(i18n.language)
  const availableToAdd = allCurrencies.filter((c) => !supported.includes(c.code))

  const mutation = useMutation({
    mutationFn: async (body: { mainCurrency: string; defaultCreditConsumptionMode: number; supportedCurrencies: string[] }) => {
      const { error } = await client.PUT('/api/users/settings', { body })
      if (error) throw error
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })

  const { control, handleSubmit, reset, formState: { isValid } } = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: { mainCurrency: '' },
  })

  const currentMainCurrency = useWatch({ control, name: 'mainCurrency' })

  useEffect(() => {
    if (settingsData?.mainCurrency) {
      reset({ mainCurrency: settingsData.mainCurrency })
    }
  }, [settingsData, reset])

  const addCurrency = (code: string) => {
    if (!supported.includes(code)) {
      setSupported((prev) => [...prev, code])
    }
  }

  const removeCurrency = (code: string) => {
    setSupported((prev) => prev.filter((c) => c !== code))
  }

  const onSubmit = (values: SettingsFormValues) => {
    mutation.mutate({
      mainCurrency: values.mainCurrency,
      defaultCreditConsumptionMode: settingsData?.defaultCreditConsumptionMode ?? 0,
      supportedCurrencies: supported,
    })
  }

  const mutationError = mutation.error
  let errorMessage = ''
  if (mutationError) {
    const errs = Array.isArray(mutationError) ? mutationError : [{ description: 'Failed to save settings.' }]
    errorMessage = errs.map((e: { description: string }) => e.description).join('. ')
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>{t('settings.general.title')}</CardTitle>
          <CardDescription>{t('settings.general.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            <div className="grid gap-2">
              <label className="text-sm font-medium" htmlFor="base-currency">
                {t('settings.general.currency')}
              </label>
              <Controller
                name="mainCurrency"
                control={control}
                render={({ field, fieldState }) => (
                  <CurrencySelector
                    id="base-currency"
                    value={field.value}
                    onValueChange={(code) => {
                      field.onChange(code)
                      if (!supported.includes(code)) {
                        setSupported((prev) => [...prev, code])
                      }
                    }}
                    invalid={fieldState.invalid}
                    currencies={currencyOptions}
                  />
                )}
              />
            </div>

            <div className="grid gap-2">
              <label className="text-sm font-medium">
                {t('settings.supportedCurrencies.title')}
              </label>
              <p className="text-sm text-muted-foreground">
                {t('settings.supportedCurrencies.description')}
              </p>
              <div className="space-y-1.5">
                {supported.map((code) => {
                  const info = allCurrencies.find((c) => c.code === code)
                  const isMain = code === currentMainCurrency
                  return (
                    <div
                      key={code}
                      className="flex items-center justify-between rounded-lg border px-3 py-2"
                    >
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-medium">{code}</span>
                        <span className="text-sm text-muted-foreground">{info?.name ?? code}</span>
                        {isMain && (
                          <Badge variant="secondary" className="text-[10px] px-1.5 py-0">
                            ★ Main
                          </Badge>
                        )}
                      </div>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-xs"
                        disabled={isMain}
                        title={isMain ? t('settings.supportedCurrencies.cannotRemoveMain') : t('settings.supportedCurrencies.remove')}
                        onClick={() => removeCurrency(code)}
                      >
                        <X className="size-4" />
                      </Button>
                    </div>
                  )
                })}
              </div>

              <div className="flex items-center gap-2">
                <CurrencyCombobox
                  value=""
                  onValueChange={(code) => code && addCurrency(code)}
                  invalid={false}
                  currencies={availableToAdd}
                />
              </div>
            </div>

            {errorMessage && (
              <p className="text-sm text-destructive">{errorMessage}</p>
            )}

            <Button
              type="submit"
              disabled={mutation.isPending || !isValid}
            >
              {mutation.isPending ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
              {t('settings.save')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
