/**
 * Early feedback de sintaxis de regex para los campos de patrón de EmailImport.
 *
 * ── Por qué este aviso NO bloquea el guardado (decisión, no descuido) ──────────
 * La sintaxis de `RegExp` de JavaScript NO es la de .NET: hay patrones válidos en
 * .NET que JS rechaza (los modificadores inline `(?n)`, por ejemplo) y patrones que
 * JS acepta y .NET no. Si este aviso bloqueara el guardado, se rechazarían reglas
 * que el servidor habría aceptado. Por eso el cliente da *early feedback* mientras
 * se escribe y el servidor sigue siendo el juez: cuando devuelve 400
 * `EmailImport.InvalidRule`, el formulario pinta el mensaje junto al campo de
 * patrones (ver `EmailRuleForm.handleSave`). No "arregles" esto poniendo un
 * `disabled` en el botón de guardar: sería el mismo error de juicio en la dirección
 * contraria. El helper es puro a propósito: sin React, sin i18n (el caller
 * traduce), y comprobable en el futuro cuando el frontend tenga runner de unit
 * tests (pendiente consciente del plan, §7/16.2).
 */

/**
 * Intenta compilar `pattern` como regex de JavaScript.
 *
 * @returns `null` si el patrón está vacío (sin patrón: lo cubre la validación de
 * "obligatorio", no este aviso) o si compila; el mensaje del error del motor en
 * caso contrario.
 */
export function getRegexError(pattern: string): string | null {
  if (!pattern) return null;
  try {
    // Compilar y descartar: solo importa si la sintaxis es válida en JS.
    new RegExp(pattern);
    return null;
  } catch (err) {
    return err instanceof Error ? err.message : String(err);
  }
}
