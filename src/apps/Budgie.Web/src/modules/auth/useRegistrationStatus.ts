import { useQuery } from "@tanstack/react-query";

export interface RegistrationStatus {
  enabled: boolean;
  hasUsers: boolean;
}

export function useRegistrationStatus() {
  return useQuery({
    queryKey: ["registration-status"],
    queryFn: async () => {
      const response = await fetch("/api/users/registration-status");
      if (!response.ok) {
        throw new Error("Failed to fetch registration status");
      }
      return (await response.json()) as RegistrationStatus;
    },
    staleTime: 60_000,
    retry: 1,
  });
}
