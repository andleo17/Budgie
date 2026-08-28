import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { PreviewCategory } from "@/modules/import/api/importClient";

interface CategoriesTableProps {
  categories: PreviewCategory[];
}

/** Tabla de categorías detectadas en el preview del .mmbak. */
export function CategoriesTable({ categories }: CategoriesTableProps) {
  const { t } = useTranslation();

  return (
    <div className="rounded-lg border bg-card">
      <div className="flex items-center justify-between border-b px-4 py-2">
        <h3 className="text-sm font-semibold">{t("import.preview.categories")}</h3>
        <Badge variant="secondary">{categories.length}</Badge>
      </div>
      <div className="max-h-64 overflow-auto">
        <Table>
          <caption className="sr-only">{t("import.preview.categories")}</caption>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>{t("import.preview.categoryName")}</TableHead>
              <TableHead>{t("import.preview.categoryType")}</TableHead>
              <TableHead className="text-right">{t("import.preview.monthlyBudget")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {categories.map((category) => (
              <TableRow key={category.externalUid}>
                <TableCell className="font-medium">{category.name}</TableCell>
                <TableCell className="text-muted-foreground">{category.type}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {category.monthlyBudget ?? "—"}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
