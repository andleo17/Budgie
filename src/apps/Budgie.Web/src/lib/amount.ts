/**
 * Normalización de importes tecleados por el usuario (EmailImport, plan deuda §7.16.2).
 *
 * Los separadores varían por locale: `1.234,56` (es-ES), `1,234.56` (en-US) y
 * `1234.56` representan el mismo número. La regla es: los separadores de grupo se
 * eliminan y el ÚLTIMO separador que quede se trata como punto decimal.
 *
 * Un `replace(",", ".")` global NO sirve aquí: convertiría `1,234.56` (en-US) en
 * `1.234.56` → NaN. El replace sin `g` que había (`Number(amount.replace(",", "."))`)
 * solo sustituía la primera coma: `1.234,56` acababa en `1.234.56` → NaN y el
 * botón de aprobar quedaba deshabilitado en silencio.
 *
 * Todo lo que no sea un número escrito a mano devuelve `""` (y por tanto NaN al
 * parsear), nunca un número plausible: el importe que se aprueba mueve dinero, y
 * un error silencioso aquí es peor que un botón deshabilitado.
 *
 * **Ambigüedad que se acepta a propósito:** un separador seguido de exactamente
 * tres dígitos (`1.234`, `1,234`) se interpreta como DECIMAL (1.234), no como
 * miles (1234). Las dos lecturas son defendibles y el navegador no puede saber
 * cuál quería el usuario. Se elige decimal porque es la lectura que no inventa un
 * factor 1000 sobre algo que podría ser un importe pequeño, y porque el campo
 * muestra el texto tal cual se escribió: si el resultado no es el que esperaba,
 * lo ve y lo corrige. Quien prefiera la lectura contraria debería quitar el
 * último separador y escribirlo como decimal explícito (`1234.` o `1234,00`).
 */

/** Signo inicial, un separador decimal inicial opcional, y luego dígitos y separadores. */
const NUMERIC_SHAPE = /^[+-]?[.,]?\d[\d.,]*$/;

/**
 * Devuelve el importe como string con punto decimal (o `""` si no hay nada
 * utilizable). No lanza nunca.
 */
export function normalizeAmountInput(raw: string): string {
  // Se ignoran espacios y NBSP: los usuarios pegan desde hojas de cálculo.
  const cleaned = raw.trim().replace(/[\s\u00A0]/g, "");
  if (!cleaned) return "";

  // Cualquier cosa que no sea un número escrito a mano se descarta aquí. Sin este
  // filtro, `0x10` → 16 y `1e5` → 100000 pasarían `Number.isFinite` y > 0, y se
  // aprobarían como importes.
  if (!NUMERIC_SHAPE.test(cleaned)) return "";

  // Separadores colgantes antes de decidir cuál es el decimal: `1,234,` se
  // normalizaría como 1234 (×1000 respecto a lo que el usuario quiso escribir) si
  // se dejara el punto y coma final como separador decimal de una parte vacía.
  const trimmedSeparators = cleaned.replace(/[.,]+$/, "");
  if (!trimmedSeparators) return "";

  const lastDot = trimmedSeparators.lastIndexOf(".");
  const lastComma = trimmedSeparators.lastIndexOf(",");
  const lastSep = Math.max(lastDot, lastComma);
  if (lastSep === -1) return trimmedSeparators;

  // El último separador (de cualquier tipo) es el decimal; TODOS los separadores
  // restantes —de cualquier tipo, porque `1,234,56` usa comas para las dos cosas—
  // son separadores de grupo y se eliminan.
  const integerPart = trimmedSeparators.slice(0, lastSep).replace(/[.,]/g, "");
  const decimalPart = trimmedSeparators.slice(lastSep + 1).replace(/[.,]/g, "");
  return decimalPart === "" ? integerPart : `${integerPart}.${decimalPart}`;
}

/**
 * Parsea un importe escrito a mano. Devuelve `NaN` cuando la entrada no es un
 * número utilizable (el caller decide qué hacer: en la bandeja, deshabilita el
 * botón y muestra `amountInvalid`).
 *
 * `Number("")` es 0, no NaN — por eso el `""` se comprueba explícitamente: un
 * campo vacío no es un importe de cero.
 */
export function parseAmountInput(raw: string): number {
  const normalized = normalizeAmountInput(raw);
  return normalized === "" ? NaN : Number(normalized);
}