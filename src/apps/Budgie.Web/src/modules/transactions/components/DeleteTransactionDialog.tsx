import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { getApiError } from "@/lib/format";
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
import { Loader2 } from "lucide-react";

interface DeleteTransactionDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  transactionId: string;
}

export function DeleteTransactionDialog({ open, onOpenChange, transactionId }: DeleteTransactionDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/finances/transactions/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["transactionSummary"] });
      queryClient.invalidateQueries({ queryKey: ["dashboardSummary"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["personDebts"] });
      queryClient.invalidateQueries({ queryKey: ["personSummary"] });
      onOpenChange(false);
    },
    onError: (err) => setErrorMsg(getApiError(err)),
  });

  return (
    <AlertDialog open={open} onOpenChange={(v) => { if (!v) setErrorMsg(null); onOpenChange(v); }}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("transactions.deleteTransaction")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("transactions.deleteConfirm")}
          </AlertDialogDescription>
        </AlertDialogHeader>
        {errorMsg && (
          <p className="text-sm text-red-400 px-6">{errorMsg}</p>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel>{t("transactions.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            onClick={async () => {
              setErrorMsg(null);
              await deleteMutation.mutateAsync(transactionId);
            }}
            disabled={deleteMutation.isPending}
          >
            {deleteMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
            {t("transactions.deleteTransaction")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
