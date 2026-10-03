import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import * as api from "@/modules/email-import/api";
import { emailImportKeys } from "./useEmailConnections";
import type { EmailRuleBody, PreviewRuleRequest } from "@/modules/email-import/types";

/** GET /api/email-import/rules — ordenadas por priority asc en el backend. */
export function useEmailRules() {
  return useQuery({
    queryKey: emailImportKeys.rules,
    queryFn: () => api.listRules(),
  });
}

export function useCreateRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: EmailRuleBody) => api.createRule(body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailImportKeys.rules });
    },
  });
}

export function useUpdateRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: EmailRuleBody }) => api.updateRule(id, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailImportKeys.rules });
    },
  });
}

export function useDeleteRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: emailImportKeys.rules });
    },
  });
}

/**
 * POST /rules/preview — probador de reglas (spec §7). No persiste nada: sirve para
 * reglas guardadas (ruleId) y para borradores en edición (draft).
 */
export function usePreviewRule() {
  return useMutation({
    mutationFn: (body: PreviewRuleRequest) => api.previewRule(body),
  });
}
