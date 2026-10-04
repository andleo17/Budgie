/**
 * Utilidades puras de confianza (sin JSX) para los badges de email-import.
 * Separadas de los componentes para no romper `react-refresh/only-export-components`.
 */

export type ConfidenceBand = "high" | "medium" | "low";

/**
 * Banda ALTA de los badges. Coincide en número con `ConfidenceWeights.DefaultAutoCommitThreshold`
 * (0.85) pero **no es el mismo valor que la política de auto-commit**: éste decide qué color
 * lleva un badge, aquél decide qué se commitea sin revisión. Mantenerlos unidos haría que
 * cambiar uno cambiara el otro por accidente, así que están desacoplados a propósito
 * (deuda §6.1): el backend manda el umbral real por conexión y esta banda es sólo presentación.
 */
const HIGH_THRESHOLD = 0.85;
const MEDIUM_THRESHOLD = 0.5;

export function confidenceBand(score: number): ConfidenceBand {
  if (score >= HIGH_THRESHOLD) return "high";
  if (score >= MEDIUM_THRESHOLD) return "medium";
  return "low";
}

/** Porcentaje legible con una cifra decimal, p.ej. "92%" o "30%". */
export function confidencePercent(score: number): string {
  return `${Math.round(Math.min(Math.max(score, 0), 1) * 100)}%`;
}

/**
 * Parsea `fieldConfidenceJson` — el JSONB que PostgreSQL devuelve como **string**
 * (jsonb normaliza claves y escaping al escribir, así que nunca se comparan strings
 * crudos). Devuelve null si el dato viene vacío o mal formado: el caller muestra un
 * fallback en vez de romper la fila.
 */
export function parseFieldConfidence(json: string | null | undefined): Record<string, number> | null {
  if (!json) return null;
  try {
    const parsed: unknown = JSON.parse(json);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return null;
    const out: Record<string, number> = {};
    for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
      const num = typeof value === "number" ? value : Number(value);
      if (Number.isFinite(num)) out[key] = num;
    }
    return Object.keys(out).length > 0 ? out : null;
  } catch {
    return null;
  }
}
