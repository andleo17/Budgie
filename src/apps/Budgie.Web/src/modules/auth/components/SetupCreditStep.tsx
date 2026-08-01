import { Controller, type Control, type FieldErrors } from "react-hook-form"
import type { SetupFormValues } from "@/modules/auth/schemas"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Field, FieldLabel, FieldContent, FieldError } from "@/components/ui/field"

export function SetupCreditStep({
  control,
  errors,
  t,
}: {
  control: Control<SetupFormValues>
  errors: FieldErrors<SetupFormValues>
  t: (key: string) => string
}) {
  const items = [
    { label: t("setup.credit.total"), value: "0" },
    { label: t("setup.credit.partial"), value: "1" },
  ]

  return (
    <Controller
      control={control}
      name="defaultCreditConsumptionMode"
      render={({ field }) => (
        <Field>
          <FieldLabel htmlFor="creditMode">
            {t("setup.credit.label")}
          </FieldLabel>
          <FieldContent>
            <Select
              items={items}
              value={String(field.value)}
              onValueChange={(v) => v && field.onChange(v)}
            >
              <SelectTrigger id="creditMode" className="h-auto py-3">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="0">
                  <div className="py-1">
                    <p className="font-medium">{t("setup.credit.total")}</p>
                    <p className="text-xs text-muted-foreground">
                      {t("setup.credit.totalDesc")}
                    </p>
                  </div>
                </SelectItem>
                <SelectItem value="1">
                  <div className="py-1">
                    <p className="font-medium">{t("setup.credit.partial")}</p>
                    <p className="text-xs text-muted-foreground">
                      {t("setup.credit.partialDesc")}
                    </p>
                  </div>
                </SelectItem>
              </SelectContent>
            </Select>
            <FieldError
              errors={
                errors.defaultCreditConsumptionMode
                  ? [
                      {
                        message:
                          errors.defaultCreditConsumptionMode.message,
                      },
                    ]
                  : undefined
              }
            />
          </FieldContent>
        </Field>
      )}
    />
  )
}
