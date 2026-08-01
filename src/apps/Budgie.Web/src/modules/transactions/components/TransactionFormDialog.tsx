import { useTranslation } from "react-i18next";
import { format } from "date-fns";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import type { components } from "@/lib/api-types";
import type { FormType } from "./TransactionTypeDropdown";
import { IncomeExpenseForm } from "./IncomeExpenseForm";
import { TransferForm } from "./TransferForm";
import { LoanForm } from "./LoanForm";
import { CollectionPaymentForm } from "./CollectionPaymentForm";
import { Button } from "@/components/ui/button";
import { formatCurrency } from "@/lib/format";
import { parseDbDate, getDateLocale } from "@/lib/date-locale";

type TransactionResponse = components["schemas"]["TransactionResponse"];
type RecurringTransactionResponse = components["schemas"]["RecurringTransactionResponse"];

function templateToFormType(template: RecurringTransactionResponse): FormType {
  switch (template.type) {
    case 2: return "transfer";
    case 3: return "loan";
    case 4: return "collection";
    default: return "income-expense";
  }
}

interface TransactionFormDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  transaction?: TransactionResponse | null;
  template?: RecurringTransactionResponse | null;
  defaultAccountId?: string;
  formType?: FormType;
  onDelete?: (tx: TransactionResponse) => void;
}

function getFormType(transaction?: TransactionResponse | null): FormType {
  if (!transaction) return "income-expense";
  switch (transaction.type) {
    case 2: return "transfer";
    case 3: return "loan";
    case 4: return "collection";
    default: return "income-expense";
  }
}

export function TransactionFormDialog({ open, onOpenChange, transaction, template, defaultAccountId, formType, onDelete }: TransactionFormDialogProps) {
  const { t, i18n } = useTranslation();
  const dateLocale = getDateLocale(i18n.language);
  const type = formType ?? getFormType(transaction) ?? (template ? templateToFormType(template) : "income-expense");
  const isEdit = !!transaction;

  const handleSuccess = () => {
    onOpenChange(false);
  };

  const handleCancel = () => {
    onOpenChange(false);
  };

  const titles: Record<FormType, string> = {
    "income-expense": isEdit ? "transactions.editTransaction" : "transactions.newTransaction",
    "transfer": "transactions.newTransfer",
    "loan": isEdit ? "transactions.editTransaction" : "transactions.newLoan",
    "collection": isEdit ? "transactions.editTransaction" : "transactions.newCollection",
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t(titles[type])}</DialogTitle>
          <DialogDescription>
            {isEdit ? t("transactions.editDesc") : t("transactions.addDesc")}
          </DialogDescription>
        </DialogHeader>
        {type === "income-expense" && (
          <IncomeExpenseForm
            transaction={transaction}
            template={template}
            defaultAccountId={defaultAccountId}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
            onDelete={onDelete}
          />
        )}
        {type === "transfer" && (
          isEdit && transaction ? (
            <div className="grid gap-3 py-4 text-sm">
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.sourceAccount")}</span><span className="font-medium">{transaction.accountName}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.destinationAccount")}</span><span className="font-medium">{transaction.transferDestinationAccountName ?? "—"}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.amount")}</span><span className="font-medium">{formatCurrency(Number(transaction.amount), transaction.currency)}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.date")}</span><span className="font-medium">{format(parseDbDate(transaction.date), "MMM d, yyyy HH:mm", { locale: dateLocale })}</span></div>
              {transaction.notes && <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.notes")}</span><span className="font-medium">{transaction.notes}</span></div>}
              <div className="flex justify-end pt-2">
                <Button type="button" variant="outline" size="sm" onClick={handleCancel}>{t("transactions.cancel")}</Button>
              </div>
            </div>
          ) : (
            <TransferForm
              defaultAccountId={defaultAccountId}
              onSuccess={handleSuccess}
              onCancel={handleCancel}
            />
          )
        )}
        {type === "loan" && (
          <LoanForm
            transaction={transaction}
            defaultAccountId={defaultAccountId}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
            onDelete={onDelete}
          />
        )}
        {type === "collection" && (
          isEdit && transaction ? (
            <div className="grid gap-3 py-4 text-sm">
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.amount")}</span><span className="font-medium">{formatCurrency(Number(transaction.amount), transaction.currency)}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.account")}</span><span className="font-medium">{transaction.accountName}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.date")}</span><span className="font-medium">{format(parseDbDate(transaction.date), "MMM d, yyyy HH:mm", { locale: dateLocale })}</span></div>
              {transaction.notes && <div className="flex justify-between"><span className="text-muted-foreground">{t("transactions.notes")}</span><span className="font-medium">{transaction.notes}</span></div>}
              {transaction.participants && transaction.participants.length > 0 && (
                <div>
                  <span className="text-muted-foreground text-xs">{t("transactions.participants")}</span>
                  {transaction.participants.map((p) => (
                    <div key={p.personId} className="flex justify-between text-sm mt-1">
                      <span>{p.personId.slice(0, 8)}</span>
                      <span>{formatCurrency(Number(p.contributedAmount), transaction.currency)}</span>
                    </div>
                  ))}
                </div>
              )}
              <div className="flex justify-end pt-2">
                <Button type="button" variant="outline" size="sm" onClick={handleCancel}>{t("transactions.cancel")}</Button>
              </div>
            </div>
          ) : (
            <CollectionPaymentForm
              defaultAccountId={defaultAccountId}
              onSuccess={handleSuccess}
              onCancel={handleCancel}
            />
          )
        )}

      </DialogContent>
    </Dialog>
  );
}
