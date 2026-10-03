/**
 * Tipos locales del módulo EmailImport.
 *
 * `src/lib/api-types.ts` se genera desde OpenAPI con `bun run api:gen` (requiere la API
 * corriendo en :5057) y todavía no contiene las rutas de `/api/email-import`. Estos tipos
 * están alineados con los DTOs reales de `Budgie.Modules.EmailImport` (serializados en
 * camelCase por `BudgieJsonContext`). Sustituir por los tipos generados en cuanto
 * `api:gen` esté disponible — mismo precedente que `modules/import/api/importClient.ts`.
 *
 * Nota de serialización: los enums viajan como **números** (no hay
 * `JsonStringEnumConverter` en el backend): `TransactionType` (0=Income, 1=Expense),
 * `EmailProvider` (0=Gmail), `PendingEmailStatus` (0=Pending, 3=Failed).
 */

export const EmailProvider = {
  Gmail: 0,
} as const;

export const PendingEmailStatus = {
  Pending: 0,
  Committed: 1,
  Dismissed: 2,
  Failed: 3,
} as const;

/** Clave del único adaptador disponible en v1 (spec §13: fuera de alcance los bank.*). */
export const GENERIC_REGEX_ADAPTER = "generic.regex";

/** Conexión de correo, solo metadatos — la API nunca devuelve credenciales. */
export interface EmailConnectionStatus {
  provider: number;
  email: string;
  syncEnabled: boolean;
  autoCommitMinConfidence: number;
  lastSyncAt: string | null;
  lastError: string | null;
  lastSkippedCount: number;
}

export interface EmailAuthorizationUri {
  uri: string;
  state: string;
}

/** Regla de importación tal y como la devuelve GET /rules. */
export interface EmailImportRule {
  id: string;
  name: string;
  adapterKey: string;
  priority: number;
  isEnabled: boolean;
  accountId: string;
  categoryId: string | null;
  transactionType: number;
  currency: string;
  exchangeRate: number | null;
  fromAddress: string | null;
  subjectContains: string | null;
  bodyRegex: string | null;
  fieldPatternsJson: string | null;
  autoCommit: boolean;
}

/** Cuerpo de POST /rules y PUT /rules/{id} (sin id ni isEnabled en el draft del tester). */
export interface EmailRuleBody {
  name: string;
  adapterKey: string;
  priority: number;
  isEnabled: boolean;
  accountId: string;
  categoryId: string | null;
  transactionType: number;
  currency: string;
  exchangeRate: number | null;
  fromAddress: string | null;
  subjectContains: string | null;
  bodyRegex: string | null;
  fieldPatternsJson: string | null;
  autoCommit: boolean;
}

/** Borrador de regla para el probador (POST /rules/preview → draft). */
export interface EmailRuleDraftBody {
  name: string;
  adapterKey: string;
  priority: number;
  accountId: string;
  categoryId: string | null;
  transactionType: number;
  currency: string;
  exchangeRate: number | null;
  fromAddress: string | null;
  subjectContains: string | null;
  bodyRegex: string | null;
  fieldPatternsJson: string | null;
  autoCommit: boolean;
}

export interface PreviewRuleRequest {
  ruleId?: string | null;
  draft?: EmailRuleDraftBody | null;
  rawEmail: string;
  fromAddress?: string | null;
  subject?: string | null;
}

export interface PreviewRuleResponse {
  plainText: string;
  fields: Record<string, string | null>;
  fieldConfidence: Record<string, number>;
  confidence: number;
  explanation: string[];
  skipReason: string | null;
}

export interface PendingEmailTransaction {
  id: string;
  connectionId: string;
  ruleId: string | null;
  subject: string | null;
  merchant: string | null;
  notes: string | null;
  amount: number;
  currency: string;
  exchangeRate: number;
  date: string;
  transactionType: number;
  confidence: number;
  /** JSONB devuelto por PostgreSQL como string. Parsear con parseFieldConfidence(). */
  fieldConfidenceJson: string | null;
  rawBody: string | null;
  failureReason: string | null;
  createdAt: string;
}

export interface PagedPendingEmails {
  items: PendingEmailTransaction[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ApprovePendingBody {
  amount?: number | null;
  date?: string | null;
  merchant?: string | null;
  notes?: string | null;
  accountId?: string | null;
  categoryId?: string | null;
  type?: number | null;
}

export interface ApproveBatchBody {
  minConfidence: number;
  take: number;
}

export interface ApproveBatchResponse {
  approved: number;
  failed: number;
  failures: string[];
}

export interface EmailSyncResult {
  connectionsProcessed: number;
  messagesFetched: number;
  autoCommitted: number;
  staged: number;
  skipped: number;
  failed: number;
  connectionErrors: string[];
}

/** Claves de confianza por campo que devuelve el scorer (ConfidenceScorer.*Field). */
export const CONFIDENCE_FIELDS = ["amount", "direction", "merchant", "date", "currency"] as const;
export type ConfidenceField = (typeof CONFIDENCE_FIELDS)[number];

/** Tipo de `PendingEmailStatus` (el valor viaja como número en la API). */
export type PendingEmailStatusValue = (typeof PendingEmailStatus)[keyof typeof PendingEmailStatus];
