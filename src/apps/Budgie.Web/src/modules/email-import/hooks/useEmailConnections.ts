import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import * as api from "@/modules/email-import/api";

export const emailImportKeys = {
  connections: ["email-import", "connections"] as const,
  rules: ["email-import", "rules"] as const,
  inbox: (page: number, status: number) => ["email-import", "inbox", page, status] as const,
  inboxCount: ["email-import", "inbox", "count"] as const,
};

/** GET /api/email-import/connections — lista vacía = nada conectado aún. */
export function useEmailConnections() {
  return useQuery({
    queryKey: emailImportKeys.connections,
    queryFn: () => api.listConnections(),
  });
}

/**
 * POST /connections/authorize → { uri, state } y salto a la URL del proveedor.
 * El callback del backend redirige de vuelta a /settings o /email-import con el
 * resultado (ver useEmailImportNotice).
 */
export function useAuthorizeEmailConnection() {
  return useMutation({
    mutationFn: (provider: string) => api.authorizeEmailConnection(provider),
    onSuccess: (data) => {
      window.location.href = data.uri;
    },
  });
}

/** DELETE /connections/{provider} — CASCADE borra reglas y staging asociados. */
export function useDisconnectEmailConnection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (provider: string) => api.disconnectEmailConnection(provider),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailImportKeys.connections });
      queryClient.invalidateQueries({ queryKey: emailImportKeys.rules });
      queryClient.invalidateQueries({ queryKey: ["email-import", "inbox"] });
    },
  });
}

/**
 * POST /api/email-import/sync. Con el worker activo el backend responde
 * 400 `EmailImport.SyncAlreadyScheduled`; el caller distingue ese código
 * (`EmailImportApiError.code`) para mostrar un aviso en vez de un error genérico.
 */
export function useSyncNow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.syncNow(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailImportKeys.connections });
      queryClient.invalidateQueries({ queryKey: ["email-import", "inbox"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
  });
}
