import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useState } from "react";
import { Loader2 } from "lucide-react";

interface TransactionSettingsCardProps {
  currentPageSize: number | string;
}

export function TransactionSettingsCard({ currentPageSize }: TransactionSettingsCardProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [pageSize, setPageSize] = useState(() => String(currentPageSize ?? 100));

  const mutation = useMutation({
    mutationFn: async (value: number) => {
      const { error } = await client.PUT("/api/users/settings", {
        body: {
          mainCurrency: "",
          defaultCreditConsumptionMode: 0,
          transactionPageSize: value,
        },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings"] });
    },
  });

  const handleSave = () => {
    const val = parseInt(pageSize, 10);
    if (val > 0 && val !== Number(currentPageSize)) {
      mutation.mutate(val);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("settings.transactions.title")}</CardTitle>
        <CardDescription>{t("settings.transactions.description")}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-end gap-3">
          <div className="space-y-1 flex-1">
            <label className="text-sm font-medium">{t("settings.transactions.pageSize")}</label>
            <p className="text-xs text-muted-foreground">{t("settings.transactions.pageSizeDesc")}</p>
            <Input
              type="number"
              min="10"
              max="1000"
              value={pageSize}
              onChange={(e) => setPageSize(e.target.value)}
            />
          </div>
          <Button
            onClick={handleSave}
            disabled={mutation.isPending || pageSize === String(currentPageSize)}
          >
            {mutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
            {t("settings.save")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
