import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { Loader2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

type UserSettingsResponse = components["schemas"]["UserSettingsResponse"];

export function RecurringSettingsCard() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const { data: settingsData } = useQuery({
    queryKey: ["settings"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/users/settings");
      if (error) throw error;
      return data as UserSettingsResponse;
    },
  });

  const [applicability, setApplicability] = useState(() => String(settingsData?.defaultRecurringApplicability ?? 1));
  const [autoApply, setAutoApply] = useState(() => settingsData?.autoApplyOnLoad ?? true);

  const mutation = useMutation({
    mutationFn: async (body: Record<string, unknown>) => {
      const { error } = await client.PUT("/api/users/settings", { body: body as never });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings"] });
    },
    onError: (err) => {
      toast.error(t("accounts.saveError"), { description: err instanceof Error ? err.message : undefined });
    },
  });

  const handleApplicabilityChange = (value: string | null) => {
    if (!value) return;
    setApplicability(value);
    mutation.mutate({
      mainCurrency: settingsData?.mainCurrency ?? "USD",
      defaultCreditConsumptionMode: settingsData?.defaultCreditConsumptionMode ?? 0,
      defaultRecurringApplicability: parseInt(value, 10),
      autoApplyOnLoad: autoApply,
    });
  };

  const handleAutoApplyChange = (value: boolean) => {
    setAutoApply(value);
    mutation.mutate({
      mainCurrency: settingsData?.mainCurrency ?? "USD",
      defaultCreditConsumptionMode: settingsData?.defaultCreditConsumptionMode ?? 0,
      defaultRecurringApplicability: parseInt(applicability, 10),
      autoApplyOnLoad: value,
    });
  };

  const applicabilityItems = [
    { label: t("templates.automatic"), value: "1" },
    { label: t("templates.manual"), value: "2" },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("settings.recurring.title")}</CardTitle>
        <CardDescription>{t("settings.recurring.description")}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Label className="text-sm font-medium">{t("settings.recurring.defaultApplicability")}</Label>
          <Select items={applicabilityItems} value={applicability} onValueChange={handleApplicabilityChange}>
            <SelectTrigger className="w-full sm:w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {applicabilityItems.map((item) => (
                <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-0.5 min-w-0">
            <Label className="text-sm font-medium">{t("settings.recurring.autoApply")}</Label>
            <p className="text-xs text-muted-foreground">{t("settings.recurring.autoApplyDesc")}</p>
          </div>
          <Switch
            checked={autoApply}
            onCheckedChange={handleAutoApplyChange}
            disabled={mutation.isPending}
          />
        </div>
        {mutation.isPending && (
          <div className="flex justify-center">
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
