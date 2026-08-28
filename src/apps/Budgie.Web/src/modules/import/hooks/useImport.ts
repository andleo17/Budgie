import { useCallback, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  previewMoneyManagerImport,
  commitMoneyManagerImport,
  type PreviewImportResponse,
  type CommitImportResponse,
} from "@/modules/import/api/importClient";

/**
 * Orquesta el flujo preview → commit del import de Money Manager.
 * Mantiene el archivo seleccionado y expone los estados de carga para que la
 * página derive idle/uploading/preview/committing/success sin duplicar lógica.
 */
export function useImport() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [file, setFile] = useState<File | null>(null);
  const [previewData, setPreviewData] = useState<PreviewImportResponse | null>(null);
  const [commitData, setCommitData] = useState<CommitImportResponse | null>(null);

  const previewMutation = useMutation({
    mutationFn: (next: File) => previewMoneyManagerImport(next),
    onSuccess: (data) => setPreviewData(data),
    onError: (err) =>
      toast.error(t("import.preview.error"), {
        description: err instanceof Error ? err.message : undefined,
      }),
  });

  const commitMutation = useMutation({
    mutationFn: ({ next, overwrite }: { next: File; overwrite: boolean }) =>
      commitMoneyManagerImport(next, overwrite),
    onSuccess: (data) => {
      setCommitData(data);
      // El commit crea cuentas, categorías, transacciones, recurrentes y
      // presupuestos: invalidar las listas afectadas para refrescar el cache.
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["recurring-transactions"] });
      queryClient.invalidateQueries({ queryKey: ["categoryBudgets"] });
    },
    onError: (err) =>
      toast.error(t("import.commit.error"), {
        description: err instanceof Error ? err.message : undefined,
      }),
  });

  const startPreview = useCallback(
    (next: File) => {
      setCommitData(null);
      setPreviewData(null);
      setFile(next);
      previewMutation.mutate(next);
    },
    [previewMutation],
  );

  const confirmCommit = useCallback(
    (overwrite: boolean) => {
      if (!file) return;
      commitMutation.mutate({ next: file, overwrite });
    },
    [file, commitMutation],
  );

  const reset = useCallback(() => {
    setFile(null);
    setPreviewData(null);
    setCommitData(null);
    previewMutation.reset();
    commitMutation.reset();
  }, [previewMutation, commitMutation]);

  return {
    file,
    previewData,
    commitData,
    isPreviewing: previewMutation.isPending,
    isPreviewError: previewMutation.isError,
    previewError: previewMutation.error,
    isCommitting: commitMutation.isPending,
    startPreview,
    confirmCommit,
    reset,
  };
}
