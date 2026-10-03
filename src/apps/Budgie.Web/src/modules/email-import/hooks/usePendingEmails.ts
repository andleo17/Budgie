import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import * as api from "@/modules/email-import/api";
import { emailImportKeys } from "./useEmailConnections";
import { PendingEmailStatus } from "@/modules/email-import/types";
import type { PendingEmailStatusValue } from "@/modules/email-import/types";
import type { ApproveBatchBody, ApprovePendingBody } from "@/modules/email-import/types";

/**
 * Página de la bandeja de revisión. `status=0` (Pending) es lo accionable;
 * `status=3` (Failed) permite mostrar por qué falló un correo.
 */
export function usePendingEmails(page: number, status: PendingEmailStatusValue, pageSize = 50) {
  return useQuery({
    queryKey: emailImportKeys.inbox(page, status),
    queryFn: () => api.getInbox({ page, pageSize, status }),
    placeholderData: (previous) => previous,
  });
}

/** Total de pendientes para la etiqueta "Pendientes (N)" — pageSize=1, solo se lee `total`. */
export function usePendingCount() {
  return useQuery({
    queryKey: emailImportKeys.inboxCount,
    queryFn: () => api.getInbox({ page: 1, pageSize: 1, status: PendingEmailStatus.Pending }),
    select: (data) => data.total,
  });
}

function invalidateInbox(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ["email-import", "inbox"] });
  queryClient.invalidateQueries({ queryKey: ["transactions"] });
  queryClient.invalidateQueries({ queryKey: ["accounts"] });
}

/**
 * POST /inbox/{id}/approve — crea la transacción con los valores (posiblemente
 * corregidos) de la fila. El caller deshabilita el botón mientras `isPending`
 * para que un doble clic no reciba el NotFound de la segunda aprobación.
 */
export function useApprovePending() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: ApprovePendingBody }) =>
      api.approvePending(id, body),
    onSuccess: () => invalidateInbox(queryClient),
  });
}

/** POST /inbox/approve-batch — aprueba en lote los pendientes sobre el umbral dado. */
export function useApproveBatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ApproveBatchBody) => api.approveBatch(body),
    onSuccess: () => invalidateInbox(queryClient),
  });
}

/**
 * POST /inbox/{id}/dismiss — la fila se conserva (auditoría) y su MessageId
 * sigue bloqueando la re-importación del mismo correo.
 */
export function useDismissPending() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.dismissPending(id),
    onSuccess: () => invalidateInbox(queryClient),
  });
}
