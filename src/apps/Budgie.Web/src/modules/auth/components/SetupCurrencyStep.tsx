import { Controller, type Control, type FieldErrors } from "react-hook-form"
import type { SetupFormValues } from "@/modules/auth/schemas"
import { Input } from "@/components/ui/input"
import { Field, FieldLabel, FieldContent, FieldError } from "@/components/ui/field"
import { CurrencyChipSelector } from "./CurrencyChipSelector"

const QUICK_CURRENCIES = ["USD", "EUR", "PEN", "BRL", "COP", "MXN"]

export function SetupCurrencyStep({
  control,
  errors,
  t,
}: {
  control: Control<SetupFormValues>
  errors: FieldErrors<SetupFormValues>
  t: (key: string) => string
}) {
  return (
    <Controller
      control={control}
      name="mainCurrency"
      render={({ field }) => (
        <Field>
          <FieldLabel htmlFor="currency">
            {t("setup.currency.label")}{" "}
            <span className="text-muted-foreground font-normal">
              {t("setup.currency.hint")}
            </span>
          </FieldLabel>
          <FieldContent>
            <Input
              id="currency"
              placeholder={t("setup.currency.placeholder")}
              maxLength={3}
              className="text-lg tracking-widest uppercase font-mono"
              value={field.value}
              onChange={(e) => field.onChange(e.target.value.toUpperCase())}
              onBlur={field.onBlur}
              ref={field.ref}
            />
            <CurrencyChipSelector
              currencies={QUICK_CURRENCIES}
              selected={field.value}
              onChange={field.onChange}
            />
            <FieldError
              errors={
                errors.mainCurrency
                  ? [{ message: errors.mainCurrency.message }]
                  : undefined
              }
            />
          </FieldContent>
        </Field>
      )}
    />
  )
}
