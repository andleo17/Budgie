import { useTranslation } from "react-i18next";
import type { PreviewImportResponse } from "@/modules/import/api/importClient";

interface SummaryCardsProps {
  data: PreviewImportResponse;
}

/** Cuadrícula de resumen del preview (cuentas, categorías, presupuestos, recurrentes). */
export function SummaryCards({ data }: SummaryCardsProps) {
  const { t } = useTranslation();

  const summaryCards = [
    { label: t("import.preview.accounts"), value: data.accounts.length },
    { label: t("import.preview.categories"), value: data.categories.length },
    { label: t("import.preview.budgets"), value: data.budgets.length },
    { label: t("import.preview.recurring"), value: data.recurrings.length },
  ];

  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
      {summaryCards.map((card) => (
        <div key={card.label} className="flex flex-col gap-1 rounded-lg border bg-card p-4">
          <span className="text-sm text-muted-foreground">{card.label}</span>
          <span className="text-2xl font-bold tabular-nums tracking-tight">{card.value}</span>
        </div>
      ))}
    </div>
  );
}
