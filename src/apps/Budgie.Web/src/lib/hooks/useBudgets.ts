import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";

type CategoryBudget = components["schemas"]["CategoryBudget"];

export function useCategoryBudgets(month?: string) {
  return useQuery({
    queryKey: ["categoryBudgets", month],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/reports/budgets", {
        params: { query: month ? { month } : undefined },
      });
      if (error) throw error;
      return (data ?? []) as CategoryBudget[];
    },
  });
}
