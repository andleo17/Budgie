/**
 * Utilidades de accesibilidad (ARIA) compartidas, sin JSX ni React.
 *
 * Existe como fichero aparte, y no dentro de un componente, por una razón concreta: exportar
 * una función desde un fichero de componente rompe la regla `react-refresh/only-export-components`
 * de este repo, que es ERROR de lint. Un helper de `aria-describedby` vive, por definición,
 * junto a varios componentes.
 *
 * Viene del módulo email-import (§7.15), que fue el primero del repo en asociar sus errores
 * inline por `aria-describedby`: antes de este diff había CERO apariciones en todo el árbol.
 */

/**
 * Une varios ids en el valor de `aria-describedby`.
 *
 * Devuelve `undefined` cuando no hay ninguno, en vez de una cadena vacía: un
 * `aria-describedby=""` es un atributo presente que apunta a nada, que es exactamente el
 * defecto que se quería evitar al asociar los errores a sus campos.
 */
export function describedBy(...ids: Array<string | undefined>): string | undefined {
  return ids.filter(Boolean).join(" ") || undefined;
}