import { format, isWithinInterval, type Locale } from "date-fns";

export interface PeriodRange {
  from: string;
  to: string;
  label: string;
  key: string;
}

export function getMonthRange(year: number, month: number, locale?: Locale): PeriodRange {
  const start = new Date(year, month, 1);
  const end = new Date(year, month + 1, 0);
  return {
    from: format(start, "yyyy-MM-dd"),
    to: format(end, "yyyy-MM-dd"),
    label: format(start, "MMMM yyyy", { locale }),
    key: `${year}-${String(month + 1).padStart(2, "0")}`,
  };
}

export function navigateMonth(
  year: number,
  month: number,
  direction: number,
): { year: number; month: number } {
  const d = new Date(year, month + direction, 1);
  return { year: d.getFullYear(), month: d.getMonth() };
}

function clampDay(day: number): number {
  return Math.min(Math.max(day, 1), 28);
}

export function getBillingPeriodRange(
  refDate: Date,
  cycleDay: number,
  inclusive: boolean,
  locale?: Locale,
): PeriodRange {
  const cd = clampDay(cycleDay);
  const day = refDate.getDate();
  let periodEnd: Date;
  if (day >= cd) {
    periodEnd = new Date(refDate.getFullYear(), refDate.getMonth() + 1, cd);
  } else {
    periodEnd = new Date(refDate.getFullYear(), refDate.getMonth(), cd);
  }
  const periodStart = new Date(periodEnd);
  periodStart.setMonth(periodStart.getMonth() - 1);
  if (!inclusive) {
    periodEnd.setDate(periodEnd.getDate() - 1);
  }
  const fromLabel = format(periodStart, "MMM d", { locale });
  const toLabel = format(periodEnd, "MMM d, yyyy", { locale });
  const displayMonth = format(periodEnd, "MMMM yyyy", { locale });
  return {
    from: format(periodStart, "yyyy-MM-dd"),
    to: format(periodEnd, "yyyy-MM-dd"),
    label: `${displayMonth} (${fromLabel} – ${toLabel})`,
    key: `${refDate.getFullYear()}-${String(refDate.getMonth() + 1).padStart(2, "0")}-${String(refDate.getDate()).padStart(2, "0")}`,
  };
}

export function navigateBillingPeriod(
  refDate: Date,
  _cycleDay: number,
  direction: number,
): Date {
  const d = new Date(refDate);
  d.setMonth(d.getMonth() + direction);
  return d;
}

export function findCurrentBillingPeriod(
  now: Date,
  cycleDay: number,
  inclusive: boolean,
  locale?: Locale,
): PeriodRange {
  return getBillingPeriodRange(now, cycleDay, inclusive, locale);
}

function parseLocalDate(dateStr: string): Date {
  const [y, m, d] = dateStr.split("-").map(Number);
  return new Date(y, m - 1, d);
}

function withTimeOfDay(day: Date, source: Date): Date {
  const result = new Date(day);
  result.setHours(source.getHours(), source.getMinutes(), source.getSeconds(), 0);
  return result;
}

/**
 * Default date/time for a brand-new transaction, given the period currently
 * selected in the filters.
 *
 * - Period containing today → today at the current time.
 * - Any other period, credit card → first day of the period at the current time.
 * - Any other period, regular account → same day-of-month as today (clamped to
 *   the last day of the period) at the current time.
 *
 * Returns `yyyy-MM-ddTHH:mm:ss` in local time, or undefined when there is no
 * period (the caller then falls back to "now").
 */
export function resolveDefaultTransactionDate(
  from: string | undefined,
  to: string | undefined,
  isCreditCard: boolean,
  now: Date = new Date(),
): string | undefined {
  if (!from || !to) return undefined;

  const periodStart = parseLocalDate(from);
  const periodEnd = parseLocalDate(to);
  if (isNaN(periodStart.getTime()) || isNaN(periodEnd.getTime())) return undefined;

  if (isWithinInterval(now, { start: periodStart, end: periodEnd })) {
    return format(now, "yyyy-MM-dd'T'HH:mm:ss");
  }

  if (isCreditCard) {
    return format(withTimeOfDay(periodStart, now), "yyyy-MM-dd'T'HH:mm:ss");
  }

  // Same day-of-month as today, clamped to the last day of that month and to
  // the period bounds (e.g. today is the 31st and the period is February → 28/29).
  const lastDayOfMonth = new Date(
    periodStart.getFullYear(),
    periodStart.getMonth() + 1,
    0,
  ).getDate();
  const candidate = withTimeOfDay(
    new Date(
      periodStart.getFullYear(),
      periodStart.getMonth(),
      Math.min(now.getDate(), lastDayOfMonth),
    ),
    now,
  );
  if (candidate.getTime() < periodStart.getTime()) return format(withTimeOfDay(periodStart, now), "yyyy-MM-dd'T'HH:mm:ss");
  if (candidate.getTime() > periodEnd.getTime()) return format(withTimeOfDay(periodEnd, now), "yyyy-MM-dd'T'HH:mm:ss");

  return format(candidate, "yyyy-MM-dd'T'HH:mm:ss");
}
