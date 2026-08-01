import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";

type DebtResponse = components["schemas"]["DebtResponse"];

export function usePersonDebts(personId: string | undefined) {
  return useQuery({
    queryKey: ["personDebts", personId],
    queryFn: async () => {
      if (!personId) return [];
      const { data, error } = await client.GET(
        "/api/finances/persons/{personId}/debts",
        { params: { path: { personId } } },
      );
      if (error) throw error;
      return (data ?? []) as DebtResponse[];
    },
    enabled: !!personId,
  });
}

export function usePersonDebtTotal(personId: string | undefined) {
  const { data: debts } = usePersonDebts(personId);
  return debts?.reduce((sum, d) => sum + Number(d.remainingAmount), 0) ?? 0;
}
