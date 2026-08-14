import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, FileText } from "lucide-react";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { TemplateCard } from "@/modules/transactions/components/TemplateCard";
import { TemplateForm } from "@/modules/transactions/components/TemplateForm";
import { toast } from "sonner";
import { useTemplates } from "@/lib/hooks/useTransactions";

type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

type FilterType = "all" | "template" | "automatic" | "manual";

const FILTER_LABEL_KEYS: Record<FilterType, string> = {
  all: "templates.filterAll",
  template: "templates.filterTemplates",
  automatic: "templates.filterAutomatic",
  manual: "templates.filterManual",
};

export default function Templates() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<FilterType>("all");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<RecurringTransactionResponse | null>(null);

  const { data: templates, isLoading } = useTemplates();

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/finances/transactions/templates/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recurring-transactions"] });
    },
  });

  const applyMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.POST("/api/finances/transactions/templates/{id}/apply", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recurring-transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      toast.success(t("templates.applied"), { description: t("templates.applySuccess") });
    },
  });

  const filtered = (templates ?? []).filter((t) => {
    if (filter === "template" && t.applicability !== 0) return false;
    if (filter === "automatic" && t.applicability !== 1) return false;
    if (filter === "manual" && t.applicability !== 2) return false;
    if (search && !t.name.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  return (
    <div className="w-full space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{t("templates.title")}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t("templates.description")}</p>
        </div>
        <Button onClick={() => setDialogOpen(true)}>
          <Plus className="mr-1 size-4" />
          {t("templates.createTemplate")}
        </Button>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <Input
          placeholder={t("accounts.searchPlaceholder")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full sm:max-w-xs"
        />
        <div className="flex flex-wrap gap-1">
          {(["all", "template", "automatic", "manual"] as const).map((key) => (
            <Button
              key={key}
              variant={filter === key ? "secondary" : "ghost"}
              size="sm"
              onClick={() => setFilter(key)}
            >
              {t(FILTER_LABEL_KEYS[key])}
            </Button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-muted-foreground">
          <FileText className="h-12 w-12 mb-3" />
          <p>{t("templates.noTemplates")}</p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((template) => (
            <TemplateCard
              key={template.id}
              template={template}
              onEdit={() => { setEditingTemplate(template); setDialogOpen(true); }}
              onDelete={() => {
                if (confirm(t("templates.deleteConfirm"))) {
                  deleteMutation.mutate(template.id);
                }
              }}
              onApply={() => applyMutation.mutate(template.id)}
              isApplying={applyMutation.isPending}
            />
          ))}
        </div>
      )}

      <TemplateForm
        open={dialogOpen}
        onClose={() => { setDialogOpen(false); setEditingTemplate(null); }}
        template={editingTemplate}
      />
    </div>
  );
}
