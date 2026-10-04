/**
 * Cliente HTTP del módulo EmailImport.
 *
 * Usa `authFetch` de `src/lib/api-client.ts` (openapi-fetch + refresh automático del JWT),
 * igual que `modules/import/api/importClient.ts`: `api-types.ts` aún no contiene estas
 * rutas, así que las peticiones se declaran aquí con tipos locales (`types.ts`).
 *
 * Contrato de error del backend (ErrorOr): el cuerpo es `{ [errorCode]: string[] }`.
 * `parseError` extrae el primer código + descripción para que los handlers puedan
 * distinguir casos como `EmailImport.SyncAlreadyScheduled` de un error genérico.
 */
import { authFetch } from "@/lib/api-client";
import type {
  ApproveBatchBody,
  ApproveBatchResponse,
  ApprovePendingBody,
  EmailAuthorizationUri,
  EmailConnectionStatus,
  EmailImportRule,
  EmailRuleBody,
  EmailSyncResult,
  PagedPendingEmails,
  PendingEmailStatusValue,
  PreviewRuleRequest,
  PreviewRuleResponse,
} from "./types";

const BASE = "/api/email-import";

/** Error de API con el código ErrorOr cuando el backend lo devuelve. */
export class EmailImportApiError extends Error {
  readonly code: string | null;
  readonly status: number;

  constructor(message: string, code: string | null, status: number) {
    super(message);
    this.name = "EmailImportApiError";
    this.code = code;
    this.status = status;
  }
}

async function parseError(res: Response): Promise<EmailImportApiError> {
  let code: string | null = null;
  let message = `HTTP ${res.status}`;
  try {
    const body: unknown = await res.json();
    if (body && typeof body === "object" && !Array.isArray(body)) {
      const obj = body as Record<string, unknown>;
      // Contrato ErrorOr: { "EmailImport.Xxx": ["descripción"] }. Un solo error por
      // respuesta en estos endpoints, así que la primera entrada es la canónica.
      for (const [key, value] of Object.entries(obj)) {
        if (Array.isArray(value) && typeof value[0] === "string") {
          code = key;
          message = value[0];
          break;
        }
        if (typeof value === "string" && value) {
          code = key;
          message = value;
          break;
        }
      }
      if (!code) {
        if (typeof obj.detail === "string" && obj.detail) message = obj.detail;
        else if (typeof obj.title === "string" && obj.title) message = obj.title;
      }
    }
  } catch {
    // cuerpo no-JSON: se conserva el mensaje por defecto con el status
  }
  return new EmailImportApiError(message, code, res.status);
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  const res = await authFetch(input, init);
  if (!res.ok) throw await parseError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

// ── Conexiones ────────────────────────────────────────────────────────────────

export function listConnections(): Promise<EmailConnectionStatus[]> {
  return request<EmailConnectionStatus[]>(`${BASE}/connections`);
}

/** POST /connections/authorize?provider=gmail → { uri, state }. El caller hace window.location.href = uri. */
export function authorizeEmailConnection(provider: string): Promise<EmailAuthorizationUri> {
  return request<EmailAuthorizationUri>(
    `${BASE}/connections/authorize?provider=${encodeURIComponent(provider)}`,
    { method: "POST" },
  );
}

export function disconnectEmailConnection(provider: string): Promise<void> {
  return request<void>(`${BASE}/connections/${encodeURIComponent(provider)}`, {
    method: "DELETE",
  });
}

/**
 * PUT /connections/{provider}/threshold → 204, o 400 `EmailImport.InvalidThreshold`
 * si el valor sale de [0, 1].
 */
export function updateAutoCommitThreshold(
  provider: string,
  threshold: number,
): Promise<void> {
  return request<void>(`${BASE}/connections/${encodeURIComponent(provider)}/threshold`, {
    method: "PUT",
    // Content-Type obligatorio: `fetch` con un body string y sin cabecera lo envía como
    // `text/plain;charset=UTF-8`, y el binder JSON de Minimal API responde 415. Ni `request`
    // ni `authFetch` lo añaden por su cuenta, así que cada llamada con body lo declara.
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ threshold }),
  });
}

/** POST /sync → EmailSyncResult | 400 EmailImport.SyncAlreadyScheduled (worker activo). */
export function syncNow(): Promise<EmailSyncResult> {
  return request<EmailSyncResult>(`${BASE}/sync`, { method: "POST" });
}

// ── Reglas ────────────────────────────────────────────────────────────────────

export function listRules(): Promise<EmailImportRule[]> {
  return request<EmailImportRule[]>(`${BASE}/rules`);
}

export function createRule(body: EmailRuleBody): Promise<{ id: string }> {
  return request<{ id: string }>(`${BASE}/rules`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

/** PUT /rules/{id} → 204 (no devuelve body; el plan mencionaba { id } pero el backend emite NoContent). */
export function updateRule(id: string, body: EmailRuleBody): Promise<void> {
  return request<void>(`${BASE}/rules/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export function deleteRule(id: string): Promise<void> {
  return request<void>(`${BASE}/rules/${encodeURIComponent(id)}`, { method: "DELETE" });
}

/** POST /rules/preview — no escribe nada en la base de datos. */
export function previewRule(body: PreviewRuleRequest): Promise<PreviewRuleResponse> {
  return request<PreviewRuleResponse>(`${BASE}/rules/preview`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

// ── Bandeja ───────────────────────────────────────────────────────────────────

export function getInbox(params: {
  page: number;
  pageSize: number;
  status: PendingEmailStatusValue;
}): Promise<PagedPendingEmails> {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
    status: String(params.status),
  });
  return request<PagedPendingEmails>(`${BASE}/inbox?${query.toString()}`);
}

export function approvePending(id: string, body: ApprovePendingBody): Promise<{ id: string }> {
  return request<{ id: string }>(`${BASE}/inbox/${encodeURIComponent(id)}/approve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export function approveBatch(body: ApproveBatchBody): Promise<ApproveBatchResponse> {
  return request<ApproveBatchResponse>(`${BASE}/inbox/approve-batch`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export function dismissPending(id: string): Promise<void> {
  return request<void>(`${BASE}/inbox/${encodeURIComponent(id)}/dismiss`, { method: "POST" });
}
