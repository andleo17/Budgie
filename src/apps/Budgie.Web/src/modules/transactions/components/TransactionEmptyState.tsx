import { ArrowLeftRight } from "lucide-react";
import { useTranslation } from "react-i18next";

export function TransactionEmptyState() {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col items-center justify-center gap-2 py-16 text-muted-foreground">
      <ArrowLeftRight className="h-8 w-8 opacity-30" />
      <p className="text-sm">{t("transactions.noTransactions")}</p>
    </div>
  );
}
