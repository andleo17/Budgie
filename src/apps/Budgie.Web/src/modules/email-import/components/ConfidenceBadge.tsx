import { useTranslation } from "react-i18next";
import { AlertTriangleIcon, CheckIcon, XCircleIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { confidenceBand, confidencePercent } from "../confidence";

/**
 * Badge de confianza total. La banda se comunica con **texto además de color**
 * (nunca solo color): banda + porcentaje siempre visibles.
 */
export function ConfidenceBadge({
  confidence,
  className,
}: {
  confidence: number;
  className?: string;
}) {
  const { t } = useTranslation();
  const band = confidenceBand(confidence);
  const Icon =
    band === "high" ? CheckIcon : band === "medium" ? AlertTriangleIcon : XCircleIcon;
  const variant = band === "high" ? "default" : band === "medium" ? "secondary" : "destructive";

  return (
    <Badge variant={variant} className={cn("tabular-nums", className)}>
      <Icon data-icon="inline-start" />
      {t(`emailImport.confidence.${band}`)} {confidencePercent(confidence)}
    </Badge>
  );
}
