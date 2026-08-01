import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";

export function usePersons() {
  return useQuery({
    queryKey: ["persons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/persons");
      if (error) throw error;
      return data;
    },
  });
}

export function useTags() {
  return useQuery({
    queryKey: ["tags"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/relationships/tags");
      if (error) throw error;
      return data;
    },
  });
}

export function usePersonSummary(personId: string) {
  return useQuery({
    queryKey: ["personSummary", personId],
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/finances/persons/{personId}/summary",
        { params: { path: { personId } } },
      );
      if (error) return null;
      return data;
    },
  });
}
