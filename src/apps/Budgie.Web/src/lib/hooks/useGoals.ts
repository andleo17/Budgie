import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";

export function useGoals() {
  return useQuery({
    queryKey: ["goals"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/goals");
      if (error) throw error;
      return data;
    },
  });
}
