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
import type { PreviewAccount } from "@/modules/import/api/importClient";

interface AccountsTableProps {
  accounts: PreviewAccount[];
}

/** Tabla de cuentas detectadas en el preview del .mmbak. */
export function AccountsTable({ accounts }: AccountsTableProps) {
  const { t } = useTranslation();

  return (
    <div className="rounded-lg border bg-card">
      <div className="flex items-center justify-between border-b px-4 py-2">
        <h3 className="text-sm font-semibold">{t("import.preview.accounts")}</h3>
        <Badge variant="secondary">{accounts.length}</Badge>
      </div>
      <div className="max-h-64 overflow-auto">
        <Table>
          <caption className="sr-only">{t("import.preview.accounts")}</caption>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>{t("import.preview.accountName")}</TableHead>
              <TableHead>{t("import.preview.accountType")}</TableHead>
              <TableHead className="text-right">{t("import.preview.currency")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {accounts.map((account) => (
              <TableRow key={account.externalUid}>
                <TableCell className="font-medium">{account.name}</TableCell>
                <TableCell className="text-muted-foreground">{account.type}</TableCell>
                <TableCell className="text-right tabular-nums">{account.currency}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
