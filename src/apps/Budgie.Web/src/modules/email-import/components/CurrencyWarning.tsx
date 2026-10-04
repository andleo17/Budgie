import { useTranslation } from "react-i18next";
import { AlertTriangleIcon } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Aviso cuando el campo de confianza de MONEDA es 0 (plan §2.4).
 *
 * Que la moneda puntúe 0 ya no significa "el símbolo contradice la regla": desde el slice 3 el
 * backend RECHAZA el correo en ese caso (no llega ni a la bandeja). Lo que sí llega es un símbolo
 * que el adaptador no sabe identificar (`¥`, `CHF`, `₿`). Aquí no sabemos que la moneda sea
 * otra — solo que no la pudimos identificar — y eso es exactamente lo que el usuario necesita
 * leer antes de aprobar: "repásala a mano".
 *
 * Sin este componente, el dato viajaba como una línea más del `Explanation` del backend, sólo
 * visible al abrir el probador, y sólo en una de las dos superficies.
 *
 * **Sin `role="status"` a propósito.** Es contenido estático que llega con la fila, no el
 * resultado de una mutación: la fila completa ya se anuncia al pintarse. Además el bloque de
 * resultado del probador es `role="status"`, y anidar regiones vivas produce doble anuncio. El
 * aviso se lee al navegar por la fila, que es lo que corresponde.
 */
export function CurrencyWarning({
  scores,
  className,
}: {
  /** `fieldConfidenceJson` ya parseado con parseFieldConfidence(). */
  scores: Record<string, number> | null;
  className?: string;
}) {
  const { t } = useTranslation();

  // Se distingue "puntúa 0" de "no viene la clave". Un campo ausente del diccionario puntúa 0 en
  // el scorer, pero aquí eso significa "no sabemos", no "el símbolo no se pudo identificar":
  // afirmar lo segundo sobre un dato que no existe sería inventar un diagnóstico.
  if (!scores || !("currency" in scores) || scores.currency !== 0) return null;

  return (
    <p className={cn("flex items-start gap-1.5 text-xs text-destructive", className)}>
      <AlertTriangleIcon data-icon="inline-start" className="mt-0.5 shrink-0" aria-hidden="true" />
      <span>{t("emailImport.confidence.currencyUnidentified")}</span>
    </p>
  );
}