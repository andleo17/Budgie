import { enUS, es } from "date-fns/locale";

export function getDateLocale(language: string) {
  if (language.startsWith("es")) return es;
  return enUS;
}

export function formatLocal(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  const hh = String(date.getHours()).padStart(2, "0");
  const mm = String(date.getMinutes()).padStart(2, "0");
  const ss = String(date.getSeconds()).padStart(2, "0");
  return `${y}-${m}-${d}T${hh}:${mm}:${ss}`;
}

export function localToUtc(localStr: string): string {
  if (!localStr) return localStr;
  const date = new Date(localStr);
  if (isNaN(date.getTime())) return localStr;
  return date.toISOString();
}

export function parseDbDate(dateStr: string): Date {
  const [datePart, timePart] = dateStr.split("T");
  const [y, m, d] = datePart!.split("-").map(Number);
  const clean = (timePart ?? "00:00:00").replace("Z", "");
  const [hh, mn, ss] = clean.split(":").map(Number);
  return new Date(y, m - 1, d, hh, mn, ss ?? 0);
}
