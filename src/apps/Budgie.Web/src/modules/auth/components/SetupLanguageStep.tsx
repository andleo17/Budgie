import { Controller, type Control } from "react-hook-form"
import type { SetupFormValues } from "@/modules/auth/schemas"
import i18n from "@/i18n/config"
import { Languages } from "lucide-react"
import { Field, FieldLabel, FieldContent } from "@/components/ui/field"

const languages = [
  { code: "en", labelKey: "sidebar.english" },
  { code: "es", labelKey: "sidebar.spanish" },
] as const

export function SetupLanguageStep({
  control,
  t,
}: {
  control: Control<SetupFormValues>
  t: (key: string) => string
}) {
  return (
    <Controller
      control={control}
      name="language"
      render={({ field }) => (
        <Field>
          <FieldLabel htmlFor="language">{t("setup.language.label")}</FieldLabel>
          <FieldContent>
            <div className="space-y-1">
              {languages.map((lang) => (
                <button
                  key={lang.code}
                  type="button"
                  onClick={() => {
                    i18n.changeLanguage(lang.code)
                    field.onChange(lang.code)
                  }}
                  className={`flex w-full items-center gap-3 rounded-lg border px-3 py-2.5 text-left text-sm transition-colors cursor-pointer ${
                    field.value === lang.code
                      ? "border-primary bg-primary/10 text-primary font-medium"
                      : "border-border hover:bg-muted text-foreground"
                  }`}
                >
                  <Languages className="size-4 shrink-0 text-muted-foreground" />
                  <span>{t(lang.labelKey)}</span>
                  {field.value === lang.code && (
                    <span className="ml-auto text-xs text-primary">{t("settings.active")}</span>
                  )}
                </button>
              ))}
            </div>
          </FieldContent>
        </Field>
      )}
    />
  )
}
