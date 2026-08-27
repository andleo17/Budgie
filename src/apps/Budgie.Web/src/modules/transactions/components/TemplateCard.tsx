import { useTranslation } from "react-i18next";
import { ArrowUp, ArrowDown, ArrowLeftRight, Pen, Trash2, Play, Clock, Loader2 } from "lucide-react";
import { formatCurrency } from "@/lib/format";
import type { components } from "@/lib/api-types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

const typeIcons = {
  0: ArrowUp,
  1: ArrowDown,
  2: ArrowLeftRight,
  3: ArrowDown,
  4: ArrowUp,
} as const;

const typeColors = {
  0: "text-green-500 bg-green-500/10",
  1: "text-red-500 bg-red-500/10",
  2: "text-blue-500 bg-blue-500/10",
  3: "text-orange-500 bg-orange-500/10",
  4: "text-green-500 bg-green-500/10",
} as const;

const applicabilityLabels = ["template", "automatic", "manual"] as const;
const applicabilityVariants = ["outline", "default", "secondary"] as const;

const frequencyLabels = ["daily", "weekly", "biweekly", "monthly", "yearly", "custom"] as const;

interface TemplateCardProps {
  template: RecurringTransactionResponse;
  onEdit: () => void;
  onDelete: () => void;
  onApply: () => void;
  isApplying: boolean;
}

export function TemplateCard({ template, onEdit, onDelete, onApply, isApplying }: TemplateCardProps) {
  const { t } = useTranslation();
  const Icon = typeIcons[template.type as keyof typeof typeIcons] || ArrowDown;
  const color = typeColors[template.type as keyof typeof typeColors] || "text-gray-500 bg-gray-500/10";

  return (
    <div className="rounded-2xl border bg-card p-4 space-y-3 transition-all active:bg-muted/40 sm:hover:shadow-sm sm:hover:border-accent-foreground/20">
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3 min-w-0">
          <div className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${color}`}>
            <Icon className="size-5" />
          </div>
          <div className="min-w-0">
            <p className="font-semibold truncate">{template.name}</p>
            <p className="text-lg font-bold tabular-nums">
              {formatCurrency(Number(template.amount), template.currency)}
            </p>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <Badge variant={applicabilityVariants[template.applicability] || "outline"}>
          {t(`templates.${applicabilityLabels[template.applicability]}`)}
        </Badge>
        {template.frequency != null && (
          <Badge variant="outline" className="gap-1">
            <Clock className="size-3" />
            {t(`templates.${frequencyLabels[template.frequency] || "monthly"}`)}
          </Badge>
        )}
      </div>

      {template.frequency != null && template.nextDueDate && (
        <p className="text-xs text-muted-foreground">
          {t("templates.nextDue")}: {new Date(template.nextDueDate).toLocaleDateString()}
        </p>
      )}

      {Number(template.timesApplied) > 0 && (
        <p className="text-xs text-muted-foreground">
          {t("templates.timesApplied")}: {template.timesApplied}
        </p>
      )}

      <div className="flex items-center gap-2 pt-1">
        {template.applicability !== 0 && (
          <Button size="sm" variant="default" onClick={onApply} disabled={isApplying}>
            {isApplying ? <Loader2 className="size-3 animate-spin mr-1" /> : <Play className="size-3 mr-1" />}
            {t("templates.apply")}
          </Button>
        )}
        <Button size="sm" variant="outline" onClick={onEdit}>
          <Pen className="size-3 mr-1" />
          {t("common.edit")}
        </Button>
        <Button size="sm" variant="outline" onClick={onDelete}>
          <Trash2 className="size-3" />
        </Button>
      </div>
    </div>
  );
}
