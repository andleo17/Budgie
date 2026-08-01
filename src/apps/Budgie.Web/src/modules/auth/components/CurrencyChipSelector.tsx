export function CurrencyChipSelector({
  currencies,
  selected,
  onChange,
}: {
  currencies: string[]
  selected: string
  onChange: (value: string) => void
}) {
  return (
    <div className="flex flex-wrap gap-2 mt-1">
      {currencies.map((c) => (
        <button
          key={c}
          type="button"
          onClick={() => onChange(c)}
          className={`px-2.5 py-1 rounded-md text-xs font-mono transition-colors cursor-pointer ${selected === c ? "bg-primary text-primary-foreground" : "bg-muted hover:bg-muted/80 text-muted-foreground"}`}
        >
          {c}
        </button>
      ))}
    </div>
  )
}
