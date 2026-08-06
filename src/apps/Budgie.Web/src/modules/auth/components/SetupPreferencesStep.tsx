import { Controller, type Control, type FieldErrors } from "react-hook-form"
import type { SetupFormValues } from "@/modules/auth/schemas"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { Field, FieldLabel, FieldContent, FieldError } from "@/components/ui/field"

export function SetupPreferencesStep({
  control,
  errors,
  t,
}: {
  control: Control<SetupFormValues>
  errors: FieldErrors<SetupFormValues>
  t: (key: string) => string
}) {
  const applicabilityItems = [
    { label: t("templates.automatic"), value: "1" },
    { label: t("templates.manual"), value: "2" },
  ]

  return (
    <div className="space-y-5">
      <Controller
        control={control}
        name="transactionPageSize"
        render={({ field }) => (
          <Field>
            <FieldLabel htmlFor="pageSize">{t("setup.preferences.pageSize")}</FieldLabel>
            <FieldContent>
              <Input
                id="pageSize"
                type="number"
                min={10}
                max={1000}
                value={field.value}
                onChange={(e) => field.onChange(parseInt(e.target.value, 10) || 0)}
              />
              <p className="text-xs text-muted-foreground">
                {t("setup.preferences.pageSizeDesc")}
              </p>
              <FieldError
                errors={
                  errors.transactionPageSize
                    ? [{ message: errors.transactionPageSize.message }]
                    : undefined
                }
              />
            </FieldContent>
          </Field>
        )}
      />

      <Controller
        control={control}
        name="defaultRecurringApplicability"
        render={({ field }) => (
          <Field>
            <FieldLabel htmlFor="applicability">
              {t("setup.preferences.applicability")}
            </FieldLabel>
            <FieldContent>
              <Select
                items={applicabilityItems}
                value={String(field.value)}
                onValueChange={(v) => v && field.onChange(v)}
              >
                <SelectTrigger id="applicability" className="h-auto py-3">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {applicabilityItems.map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldContent>
          </Field>
        )}
      />

      <Controller
        control={control}
        name="autoApplyOnLoad"
        render={({ field }) => (
          <Field>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <FieldLabel>{t("setup.preferences.autoApply")}</FieldLabel>
                <p className="text-xs text-muted-foreground">
                  {t("setup.preferences.autoApplyDesc")}
                </p>
              </div>
              <Switch checked={field.value} onCheckedChange={field.onChange} />
            </div>
          </Field>
        )}
      />
    </div>
  )
}
