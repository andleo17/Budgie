import { getFormatLocale } from '@/i18n/config';

export function formatCurrency(amount: number | string, currency?: string): string {
  return new Intl.NumberFormat(getFormatLocale(), {
    style: 'currency',
    currency: currency ?? 'USD',
  }).format(Number(amount));
}

interface ApiErrorItem {
  description?: string;
  message?: string;
  code?: string;
}

export function getApiError(err: string | ApiErrorItem | ApiErrorItem[]): string {
  if (typeof err === "string") return err;
  if (Array.isArray(err)) return err[0]?.description ?? "Error";
  return err?.description ?? err?.message ?? "Error";
}
