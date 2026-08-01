import { useTranslation } from "react-i18next";
import type { CurrencyOption } from "@/lib/currencies";
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxTrigger,
  ComboboxValue,
} from "@/components/ui/combobox";

export function CurrencyCombobox({
  value,
  onValueChange,
  invalid,
  id,
  currencies,
}: {
  value: string
  onValueChange: (code: string) => void
  invalid?: boolean
  id?: string
  currencies: CurrencyOption[]
}) {
  const { t } = useTranslation();

  return (
    <Combobox
      items={currencies}
      itemToStringValue={(c: CurrencyOption) => c.code}
      value={currencies.find((c) => c.code === value) ?? null}
      onValueChange={(c: CurrencyOption | null) => onValueChange(c?.code ?? "")}
    >
      <ComboboxTrigger
        id={id}
        aria-invalid={invalid}
        className="flex h-8 w-full items-center justify-between gap-1.5 rounded-lg border border-input bg-transparent px-2.5 text-sm whitespace-nowrap transition-colors outline-none select-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 data-placeholder:text-muted-foreground"
      >
        <ComboboxValue placeholder={t("accounts.selectCurrency")}>
          {(c: CurrencyOption | null) => c && `${c.code} - ${c.name}`}
        </ComboboxValue>
      </ComboboxTrigger>
      <ComboboxContent>
        <ComboboxInput placeholder={t("accounts.currencySearch")} showTrigger={false} />
        <ComboboxEmpty>{t("accounts.currencyNotFound")}</ComboboxEmpty>
        <ComboboxList>
          {(currency: CurrencyOption) => (
            <ComboboxItem key={currency.code} value={currency}>
              <span className="font-mono font-medium">{currency.code}</span>
              <span className="text-muted-foreground">{currency.name}</span>
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
