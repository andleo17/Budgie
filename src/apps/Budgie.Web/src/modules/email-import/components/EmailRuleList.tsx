import { memo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useCategories } from "@/lib/hooks/useCategories";
import { useAccounts } from "@/lib/hooks/useAccounts";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { PencilIcon, Trash2Icon } from "lucide-react";
import { useUpdateRule, useDeleteRule } from "../hooks/useEmailRules";
import { DeleteRuleDialog } from "./DeleteRuleDialog";
import { GENERIC_REGEX_ADAPTER } from "../types";
import type { EmailImportRule } from "../types";

/**
 * Tabla de reglas de importación: activar/desactivar, editar y borrar
 * (spec §8 — pestaña "Reglas").
 */
/**
 * Tabla de reglas.
 *
 * `memo` con `onEdit` memoizado en el padre: sin las dos cosas, la tabla entera se re-renderiza
 * en CADA tecla del formulario, porque `openEditForm` es una función nueva en cada render de
 * `RulesTab`. Sus demás props (`rules`, `isLoading`) ya son estables, así que con esto la tabla
 * sólo se repinta cuando los datos cambian de verdad.
 */
export const EmailRuleList = memo(function EmailRuleList({
  rules,
  isLoading,
  onEdit,
}: {
  rules: EmailImportRule[];
  isLoading: boolean;
  onEdit: (rule: EmailImportRule) => void;
}) {
  const { t } = useTranslation();
  const { data: categories } = useCategories();
  const { data: accounts } = useAccounts();
  const updateMutation = useUpdateRule();
  const deleteMutation = useDeleteRule();

  const [deleteTarget, setDeleteTarget] = useState<EmailImportRule | null>(null);

  const categoryName = (id: string | null) => {
    if (!id) return "—";
    return categories?.find((c) => c.id === id)?.name ?? "—";
  };

  const accountName = (id: string) => accounts?.find((a) => a.id === id)?.name ?? "—";

  const toggleEnabled = (rule: EmailImportRule, isEnabled: boolean) => {
    const { id, ...body } = rule;
    updateMutation.mutate(
      { id, body: { ...body, isEnabled } },
      {
        onError: (err) =>
          toast.error(t("emailImport.rules.toggleError"), {
            description: err instanceof Error ? err.message : undefined,
          }),
      },
    );
  };

  const confirmDelete = () => {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success(t("emailImport.rules.deleted"));
        setDeleteTarget(null);
      },
      onError: (err) => {
        toast.error(t("emailImport.rules.deleteError"), {
          description: err instanceof Error ? err.message : undefined,
        });
        setDeleteTarget(null);
      },
    });
  };

  if (isLoading) {
    return (
      // role="status": el lector de pantalla anuncia la carga de la tabla (slice 15.5).
      // Skeleton hace spread de props; la primitiva no se toca.
      <div role="status" className="flex flex-col gap-2">
        <span className="sr-only">{t("emailImport.loading.rules")}</span>
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  if (rules.length === 0) {
    return <p className="py-8 text-center text-sm text-muted-foreground">{t("emailImport.rules.empty")}</p>;
  }

  return (
    <>
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("emailImport.rules.name")}</TableHead>
              <TableHead className="tabular-nums">{t("emailImport.rules.priority")}</TableHead>
              <TableHead>{t("emailImport.rules.account")}</TableHead>
              <TableHead>{t("emailImport.rules.category")}</TableHead>
              <TableHead>{t("emailImport.rules.type")}</TableHead>
              <TableHead className="tabular-nums">{t("emailImport.rules.currency")}</TableHead>
              <TableHead>{t("emailImport.rules.autoCommit")}</TableHead>
              <TableHead>{t("emailImport.rules.enabled")}</TableHead>
              <TableHead className="text-right">{t("categories.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rules.map((rule) => {
              const busy = updateMutation.isPending && updateMutation.variables?.id === rule.id;
              return (
                <TableRow key={rule.id}>
                  <TableCell>
                    <div className="flex flex-col gap-1">
                      <span className="font-medium">{rule.name}</span>
                      <div className="flex flex-wrap gap-1">
                        <Badge variant={rule.isEnabled ? "default" : "secondary"}>
                          {rule.isEnabled
                            ? t("emailImport.rules.enabledBadge")
                            : t("emailImport.rules.disabledBadge")}
                        </Badge>
                        {rule.adapterKey !== GENERIC_REGEX_ADAPTER && (
                          <Badge variant="outline">{rule.adapterKey}</Badge>
                        )}
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="tabular-nums">{rule.priority}</TableCell>
                  <TableCell>{accountName(rule.accountId)}</TableCell>
                  <TableCell>{categoryName(rule.categoryId)}</TableCell>
                  <TableCell>
                    {rule.transactionType === 0
                      ? t("transactions.income")
                      : t("transactions.expense")}
                  </TableCell>
                  <TableCell className="tabular-nums">{rule.currency}</TableCell>
                  <TableCell>
                    {rule.autoCommit ? (
                      <Badge variant="outline">{t("emailImport.rules.autoCommitBadge")}</Badge>
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    <Switch
                      checked={rule.isEnabled}
                      disabled={busy}
                      aria-label={t("emailImport.rules.enabled")}
                      onCheckedChange={(v) => toggleEnabled(rule, v)}
                    />
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={t("common.edit")}
                        onClick={() => onEdit(rule)}
                      >
                        <PencilIcon />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={t("emailImport.rules.delete")}
                        onClick={() => setDeleteTarget(rule)}
                      >
                        <Trash2Icon />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      <DeleteRuleDialog
        open={deleteTarget !== null}
        ruleName={deleteTarget?.name ?? null}
        isDeleting={deleteMutation.isPending}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
        onConfirm={confirmDelete}
      />
    </>
  );
});
