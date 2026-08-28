import { authFetch } from "@/lib/api-client";

/**
 * Tipos locales del flujo Money Manager Import.
 *
 * El backend (slices 1-7) expone estos endpoints como multipart/form-data y aún no
 * se ha regenerado `src/lib/api-types.ts` (api:gen). Por eso se tipan aquí a mano,
 * alineados con los DTOs reales de `Budgie.Modules.Finances.Application.Import`
 * (serializados en camelCase por BudgieJsonContext).
 */

export interface PreviewCurrency {
  iso: string;
  rate: number;
  isMain: boolean;
}

export interface PreviewAccount {
  name: string;
  type: string;
  currency: string;
  externalUid: string;
}

export interface PreviewCategory {
  name: string;
  type: string;
  parentUid: string | null;
  externalUid: string;
  monthlyBudget: number | null;
}

export interface PreviewTransactionsSummary {
  income: number;
  expense: number;
  transfer: number;
  initialBalance: number;
  total: number;
  dedupedTransfers: number;
  dateRange: string;
}

export interface PreviewBudget {
  categoryUid: string;
  amount: number;
}

export interface PreviewRecurring {
  frequency: string;
  notes: string | null;
}

export interface PreviewImportResponse {
  currencies: PreviewCurrency[];
  accounts: PreviewAccount[];
  categories: PreviewCategory[];
  summary: PreviewTransactionsSummary;
  budgets: PreviewBudget[];
  recurrings: PreviewRecurring[];
  warnings: string[];
  errors: string[];
  skippedPlaceholders: number;
  fileHash: string;
  alreadyImported: boolean;
}

export interface CommitImportResponse {
  createdAccounts: number;
  skippedAccounts: number;
  createdCategories: number;
  skippedCategories: number;
  createdTransactions: number;
  skippedTransactions: number;
  createdRecurring: number;
  warnings: string[];
  fileHash: string;
}

const PREVIEW_URL = "/api/import/money-manager/preview";
const COMMIT_URL = "/api/import/money-manager/commit";

function formData(file: File): FormData {
  const form = new FormData();
  form.append("file", file);
  return form;
}

/**
 * Extrae un mensaje legible del cuerpo de error del backend.
 * El contrato de ErrorOr devuelve `{ [code]: string[] }` (p.ej.
 * `{ "Import.FileTooLarge": ["..."] }`), pero también toleramos ProblemDetails.
 */
async function extractError(res: Response): Promise<string> {
  try {
    const body: unknown = await res.json();
    if (typeof body === "string" && body) return body;
    if (body && typeof body === "object") {
      const obj = body as Record<string, unknown>;
      const errors = obj.errors;
      if (errors && typeof errors === "object") {
        const first = Object.values(errors as Record<string, unknown>)[0];
        if (Array.isArray(first) && first.length) return String(first[0]);
        if (typeof first === "string" && first) return first;
      }
      if (typeof obj.detail === "string" && obj.detail) return obj.detail;
      if (typeof obj.title === "string" && obj.title) return obj.title;
    }
  } catch {
    // cuerpo no-JSON: se ignora y se usa el status
  }
  return `HTTP ${res.status}`;
}

async function parseResponse<T>(res: Response): Promise<T> {
  if (res.ok) return (await res.json()) as T;
  throw new Error(await extractError(res));
}

/** POST /api/import/money-manager/preview — dry-run sin escrituras. */
export function previewMoneyManagerImport(file: File): Promise<PreviewImportResponse> {
  return authFetch(PREVIEW_URL, { method: "POST", body: formData(file) })
    .then((res) => parseResponse<PreviewImportResponse>(res));
}

/** POST /api/import/money-manager/commit?overwrite= — commit transaccional. */
export function commitMoneyManagerImport(file: File, overwrite: boolean): Promise<CommitImportResponse> {
  const url = `${COMMIT_URL}?overwrite=${overwrite ? "true" : "false"}`;
  return authFetch(url, { method: "POST", body: formData(file) })
    .then((res) => parseResponse<CommitImportResponse>(res));
}
