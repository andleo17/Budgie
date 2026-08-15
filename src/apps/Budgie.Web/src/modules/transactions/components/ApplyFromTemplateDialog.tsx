import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { ArrowUp, ArrowDown, ArrowLeftRight, Loader2 } from "lucide-react";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { formatCurrency } from "@/lib/format";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useState } from "react";

const typeIcons: Record<number, typeof ArrowUp> = {
  0: ArrowUp,
  1: ArrowDown,
  2: ArrowLeftRight,
  3: ArrowDown,
  4: ArrowUp,
};
const typeColors: Record<number, string> = {
  0: "text-green-500 bg-green-500/10",
  1: "text-red-500 bg-red-500/10",
  2: "text-blue-500 bg-blue-500/10",
  3: "text-orange-500 bg-orange-500/10",
  4: "text-green-500 bg-green-500/10",
};

type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

interface ApplyFromTemplateDialogProps {
  open: boolean;
  onClose: () => void;
  onApply: (template: RecurringTransactionResponse) => void;
}

export function ApplyFromTemplateDialog({ open, onClose, onApply }: ApplyFromTemplateDialogProps) {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");

  const { data: templates, isLoading } = useQuery({
    queryKey: ["recurring-transactions"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/transactions/templates");
      if (error) throw error;
      return data ?? [];
    },
    enabled: open,
  });

  const filtered = (templates ?? []).filter((tmpl) =>
    tmpl.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { onClose(); setSearch(""); } }}>
      <DialogContent className="max-h-[70vh] overflow-y-auto sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("templates.selectTemplate")}</DialogTitle>
          <DialogDescription>{t("templates.description")}</DialogDescription>
        </DialogHeader>
        <Input
          placeholder={t("accounts.searchPlaceholder")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        {isLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : filtered.length === 0 ? (
          <p className="text-center text-sm text-muted-foreground py-8">{t("templates.noTemplates")}</p>
        ) : (
          <div className="space-y-2">
            {filtered.map((tmpl) => {
              const Icon = typeIcons[tmpl.type] || ArrowDown;
              const color = typeColors[tmpl.type] || "text-gray-500 bg-gray-500/10";
              return (
                <button
                  key={tmpl.id}
                  type="button"
                  className="w-full flex items-center gap-3 rounded-lg border bg-card p-3 text-left hover:bg-accent transition-colors"
                  onClick={() => {
                    onApply(tmpl);
                    onClose();
                  }}
                >
                  <div className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${color}`}>
                    <Icon className="size-4" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium truncate">{tmpl.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {formatCurrency(Number(tmpl.amount), tmpl.currency)}
                      {tmpl.frequency != null && ` · ${t("templates." + ["daily", "weekly", "biweekly", "monthly", "yearly", "custom"][tmpl.frequency] || "monthly")}`}
                    </p>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
