import { Button } from "@/components/ui/button";
import { CurrencyCombobox } from "@/components/currency-combobox";
import type { CurrencyOption } from "@/lib/currencies";
import { cn } from "@/lib/utils";

export function CurrencySelector({
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

  if (currencies.length <= 1) {
    return (
      <Button
        id={id}
        type="button"
        variant="outline"
        disabled
        className="flex h-8 w-full cursor-not-allowed items-center justify-between gap-1.5 px-2.5 text-sm"
      >
        {currencies[0] && (
          <>
            <span className="font-mono font-medium">{currencies[0].code}</span>
            <span className="text-muted-foreground">- {currencies[0].name}</span>
          </>
        )}
      </Button>
    );
  }

  if (currencies.length <= 3) {
    return (
      <div
        id={id}
        className="flex h-8 gap-0 -space-x-px"
      >
        {currencies.map((c) => (
          <Button
            key={c.code}
            type="button"
            variant="outline"
            aria-pressed={value === c.code}
            className={cn(
              "flex-1 rounded-none px-2.5 text-sm first:rounded-s-lg last:rounded-e-lg focus-visible:relative focus-visible:z-10",
              value === c.code && "border-primary bg-primary/10 text-primary font-medium",
            )}
            onClick={() => onValueChange(c.code)}
          >
            <span className="font-mono">{c.code}</span>
          </Button>
        ))}
      </div>
    );
  }

  return (
    <CurrencyCombobox
      id={id}
      value={value}
      onValueChange={onValueChange}
      invalid={invalid}
      currencies={currencies}
    />
  );
}
