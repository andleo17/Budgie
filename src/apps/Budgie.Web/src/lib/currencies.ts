export interface CurrencyOption {
  code: string
  name: string
}

const COMMON_CODES = [
  "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "CNY",
  "SEK", "NOK", "DKK", "NZD", "MXN", "SGD", "HKD", "KRW",
  "INR", "BRL", "ZAR", "TRY", "PLN", "THB", "IDR", "MYR",
  "PHP", "CZK", "ILS", "AED", "COP", "CLP", "PEN", "VND",
  "NGN", "EGP", "SAR", "ARS",
]

let _currencies: CurrencyOption[] | null = null

export function enrichCurrencies(codes: string[], locale?: string): CurrencyOption[] {
  if (codes.length === 0) return []
  const displayNames = new Intl.DisplayNames([locale ?? "en"], { type: "currency", fallback: "none" })
  return codes
    .filter((code) => {
      try { return displayNames.of(code) !== undefined } catch { return false }
    })
    .map((code) => ({
      code,
      name: displayNames.of(code) ?? code,
    }))
}

export function getCurrencies(locale?: string): CurrencyOption[] {
  if (_currencies) return _currencies
  const displayNames = new Intl.DisplayNames([locale ?? "en"], { type: "currency", fallback: "none" })
  _currencies = COMMON_CODES
    .filter((code) => {
      try { return displayNames.of(code) !== undefined } catch { return false }
    })
    .map((code) => ({
      code,
      name: displayNames.of(code) ?? code,
    }))
  return _currencies
}
