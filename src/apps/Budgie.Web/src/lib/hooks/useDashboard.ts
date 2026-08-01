import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboardSummary"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/reports/dashboard");
      if (error) throw error;
      return data;
    },
  });
}

export function useGoalsStatus() {
  return useQuery({
    queryKey: ["goalsStatus"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/reports/goals");
      if (error) throw error;
      return data;
    },
  });
}

export function useCategorySpending() {
  return useQuery({
    queryKey: ["categorySpending"],
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/finances/reports/spending-by-category",
      );
      if (error) throw error;
      return data;
    },
  });
}
