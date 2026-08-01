import { useCallback } from "react";
import i18n from "./config";
import client from "@/lib/api-client";

export function useLanguage() {
  const currentLanguage = i18n.language;

  const changeLanguage = useCallback((lng: string) => {
    i18n.changeLanguage(lng);
    const token = localStorage.getItem("token");
    if (!token) return;
    client
      .GET("/api/users/settings")
      .then(({ data }) => {
        client
          .PUT("/api/users/settings", {
            body: {
              mainCurrency: data?.mainCurrency ?? "",
              defaultCreditConsumptionMode:
                data?.defaultCreditConsumptionMode === 1 ? 1 : 0,
              language: lng,
            },
          })
          .catch(() => {});
      })
      .catch(() => {});
  }, []);

  return { changeLanguage, currentLanguage };
}
