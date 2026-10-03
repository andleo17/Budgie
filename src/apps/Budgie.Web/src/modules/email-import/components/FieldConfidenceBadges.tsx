import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { CONFIDENCE_FIELDS } from "../types";
import type { ConfidenceField } from "../types";
import { confidenceBand, confidencePercent } from "../confidence";

/**
 * Badges de confianza por campo (Importe, Signo, Comercio, Fecha, Moneda).
 * Cada badge lleva etiqueta + porcentaje + banda **en texto**: el color es
 * solo refuerzo, nunca la única señal (accesibilidad).
 *
 * Un campo ausente del diccionario puntúa 0 en el scorer, así que se muestra
 * como 0% — explica de un vistazo por qué un correo bajó a la bandeja.
 */
export function FieldConfidenceBadges({
  scores,
  className,
}: {
  /** `fieldConfidenceJson` ya parseado con parseFieldConfidence(); null = sin datos. */
  scores: Record<string, number> | null;
  className?: string;
}) {
  const { t } = useTranslation();

  if (!scores) {
    return (
      <p className={cn("text-xs text-muted-foreground", className)}>
        {t("emailImport.confidence.noData")}
      </p>
    );
  }

  return (
    <div
      role="list"
      aria-label={t("emailImport.confidence.fieldsLabel")}
      className={cn("flex flex-wrap items-center gap-1", className)}
    >
      {CONFIDENCE_FIELDS.map((field: ConfidenceField) => {
        const score = scores[field] ?? 0;
        const band = confidenceBand(score);
        const variant =
          band === "high" ? "default" : band === "medium" ? "secondary" : "destructive";
        return (
          <Badge
            key={field}
            role="listitem"
            variant={variant}
            className="tabular-nums"
          >
            {t(`emailImport.confidence.fields.${field}`)} {confidencePercent(score)} ·{" "}
            {t(`emailImport.confidence.${band}`)}
          </Badge>
        );
      })}
    </div>
  );
}
