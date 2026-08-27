import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Loader2, PencilIcon, Trash2, Plus, ChevronRight } from "lucide-react";
import { CategoryFormDialog } from "@/modules/categories/components/CategoryFormDialog";
import { BudgetsWidget, BudgetAlertBanner } from "@/modules/categories/components/BudgetsWidget";
import { useCategories, useCategoryTree } from "@/lib/hooks/useCategories";
import { CATEGORY_TYPE_LABELS } from "@/lib/constants";

type CategoryResponse = components["schemas"]["CategoryResponse"];

export default function Categories() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<CategoryResponse | null>(null);
  const [deletingCategory, setDeletingCategory] = useState<CategoryResponse | null>(null);

  const { data: categories } = useCategories();
  const treeRows = useCategoryTree(categories);

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/finances/categories/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setDeletingCategory(null);
    },
  });

  return (
    <div className="w-full space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{t("categories.title")}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t("categories.description")}</p>
        </div>
        <Button onClick={() => { setEditingCategory(null); setDialogOpen(true); }}>
          <Plus className="mr-2 h-4 w-4" />
          {t("categories.addCategory")}
        </Button>
      </div>

      <BudgetAlertBanner />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
        <div className="xl:col-span-2">
          {!categories || categories.length === 0 ? (
            <div className="flex h-40 items-center justify-center rounded-2xl border border-dashed">
              <p className="text-sm text-muted-foreground">{t("categories.noCategories")}</p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">{t("categories.color")}</TableHead>
                  <TableHead>{t("categories.name")}</TableHead>
                  <TableHead className="hidden sm:table-cell">{t("categories.type")}</TableHead>
                  <TableHead className="hidden sm:table-cell">{t("categories.icon")}</TableHead>
                  <TableHead className="w-20 text-right">{t("categories.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {treeRows.map(({ node: cat, depth }) => (
                  <TableRow key={cat.id}>
                    <TableCell>
                      <div
                        className="h-5 w-5 rounded-full border"
                        style={{ backgroundColor: cat.color ?? "#e5e7eb" }}
                      />
                    </TableCell>
                    <TableCell className="font-medium">
                      <span style={{ paddingLeft: `${depth * 20}px` }} className="inline-flex items-center gap-1">
                        {depth > 0 && <ChevronRight className="size-3 text-muted-foreground" />}
                        {cat.name}
                      </span>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell text-muted-foreground">
                      {t(`transactions.${CATEGORY_TYPE_LABELS[Number(cat.type)] ?? "expense"}`)}
                    </TableCell>
                    <TableCell className="hidden sm:table-cell text-muted-foreground">
                      {cat.icon || "—"}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon-xs" onClick={() => { setEditingCategory(cat); setDialogOpen(true); }} aria-label={t("categories.edit")}>
                          <PencilIcon className="size-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => setDeletingCategory(cat)}
                          aria-label={t("categories.delete")}
                        >
                          <Trash2 className="size-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </div>
        <BudgetsWidget />
      </div>

      <CategoryFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        category={editingCategory}
        categories={categories}
      />

      <AlertDialog open={!!deletingCategory} onOpenChange={(v) => !v && setDeletingCategory(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("categories.deleteConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("categories.deleteConfirmDesc", { name: deletingCategory?.name })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("categories.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => deletingCategory && deleteMutation.mutate(deletingCategory.id)}
              disabled={deleteMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t("categories.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
