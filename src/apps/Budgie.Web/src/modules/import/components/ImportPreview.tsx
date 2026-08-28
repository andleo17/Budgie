import { useTranslation } from "react-i18next";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Loader2, TriangleAlert, CheckCircle2, FileUp } from "lucide-react";
import type { PreviewImportResponse } from "@/modules/import/api/importClient";
import { SummaryCards } from "@/modules/import/components/import-preview/SummaryCards";
import { AccountsTable } from "@/modules/import/components/import-preview/AccountsTable";
import { CategoriesTable } from "@/modules/import/components/import-preview/CategoriesTable";

interface ImportPreviewProps {
  data: PreviewImportResponse | undefined;
  loading: boolean;
  committing: boolean;
  overwrite: boolean;
  onOverwriteChange: (v: boolean) => void;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Resumen del preview del .mmbak: cuentas, categorías y desglose de transacciones. */
export function ImportPreview({
  data,
  loading,
  committing,
  overwrite,
  onOverwriteChange,
  onConfirm,
  onCancel,
}: ImportPreviewProps) {
  const { t } = useTranslation();

  if (loading || !data) {
    return (
      <Card aria-busy="true" aria-label={t("import.preview.loading")}>
        <CardHeader>
          <CardTitle>
            <Skeleton className="h-5 w-44" />
          </CardTitle>
          <CardDescription>
            <Skeleton className="h-4 w-72 max-w-full" />
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-20 w-full" />
            ))}
          </div>
          <Skeleton className="h-48 w-full" />
        </CardContent>
      </Card>
    );
  }

  const hasErrors = data.errors.length > 0;

  const txRows = [
    { label: t("import.preview.income"), value: data.summary.income },
    { label: t("import.preview.expense"), value: data.summary.expense },
    { label: t("import.preview.transfer"), value: data.summary.transfer },
    { label: t("import.preview.initialBalance"), value: data.summary.initialBalance },
    { label: t("import.preview.dedupedTransfers"), value: data.summary.dedupedTransfers },
    { label: t("import.preview.total"), value: data.summary.total },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileUp className="size-4 text-primary" />
          {t("import.preview.title")}
        </CardTitle>
        <CardDescription>
          {t("import.preview.description")}
          {data.summary.dateRange && (
            <span className="mt-1 block text-xs text-muted-foreground">
              {t("import.preview.dateRange")}: {data.summary.dateRange}
            </span>
          )}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {hasErrors && (
          <Alert variant="destructive">
            <TriangleAlert />
            <AlertTitle>{t("import.preview.errors")}</AlertTitle>
            <AlertDescription>
              <ul className="list-disc space-y-1 pl-5">
                {data.errors.map((err, i) => (
                  <li key={i}>{err}</li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}

        <SummaryCards data={data} />

        {data.skippedPlaceholders > 0 && (
          <p className="text-sm text-muted-foreground">
            {t("import.preview.skippedPlaceholders", { count: data.skippedPlaceholders })}
          </p>
        )}

        <div className="grid gap-6 lg:grid-cols-2">
          <AccountsTable accounts={data.accounts} />
          <CategoriesTable categories={data.categories} />
        </div>

        <div className="rounded-lg border bg-card">
          <div className="flex items-center justify-between border-b px-4 py-2">
            <h3 className="text-sm font-semibold">{t("import.preview.transactions")}</h3>
            <Badge variant="secondary">{data.summary.total}</Badge>
          </div>
          <Table>
            <caption className="sr-only">{t("import.preview.transactions")}</caption>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead>{t("import.preview.type")}</TableHead>
                <TableHead className="text-right">{t("import.preview.count")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {txRows.map((row) => (
                <TableRow key={row.label}>
                  <TableCell className="text-muted-foreground">{row.label}</TableCell>
                  <TableCell className="text-right font-medium tabular-nums">{row.value}</TableCell>
                </TableRow>
              ))}
              <TableRow className="hover:bg-transparent">
                <TableCell className="font-semibold">{t("import.preview.total")}</TableCell>
                <TableCell className="text-right font-bold tabular-nums">
                  {data.summary.total}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>

        {data.warnings.length > 0 && (
          <Alert>
            <TriangleAlert />
            <AlertTitle className="flex items-center gap-2">
              {t("import.preview.warnings")}
              <Badge variant="outline">{data.warnings.length}</Badge>
            </AlertTitle>
            <AlertDescription>
              <ul className="list-disc space-y-1 pl-5">
                {data.warnings.map((warn, i) => (
                  <li key={i}>{warn}</li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}

        {data.alreadyImported && (
          <Alert>
            <CheckCircle2 />
            <AlertTitle>{t("import.commit.alreadyImportedTitle")}</AlertTitle>
            <AlertDescription>{t("import.commit.alreadyImported")}</AlertDescription>
          </Alert>
        )}

        <div className="flex flex-col gap-4 border-t pt-4">
          <label className="flex cursor-pointer items-start gap-3">
            <Checkbox
              checked={overwrite}
              onCheckedChange={(v) => onOverwriteChange(v === true)}
              className="mt-0.5"
            />
            <span className="text-sm">{t("import.commit.overwrite")}</span>
          </label>

          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <Button
              onClick={onConfirm}
              disabled={committing || hasErrors}
              className="h-11 sm:h-10 sm:min-w-44"
            >
              {committing && <Loader2 className="mr-1 size-4 animate-spin" />}
              {t("import.commit.confirm")}
            </Button>
            <Button variant="ghost" onClick={onCancel} disabled={committing}>
              {t("import.commit.cancel")}
            </Button>
          </div>
          {hasErrors && (
            <p className="text-sm text-destructive">{t("import.commit.errorsBlock")}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
