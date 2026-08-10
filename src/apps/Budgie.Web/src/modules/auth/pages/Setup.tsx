import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { setupSchema, type SetupFormValues } from "@/modules/auth/schemas";
import { useMutation } from "@tanstack/react-query";
import client from "@/lib/api-client";
import { useAuth } from "@/modules/auth/useAuth";
import { Button } from "@/components/ui/button";
import { Loader2, Activity, Languages, Wallet, CreditCard, Settings2 } from "lucide-react";
import { SetupStepIndicator } from "../components/SetupStepIndicator";
import { SetupLanguageStep } from "../components/SetupLanguageStep";
import { SetupCurrencyStep } from "../components/SetupCurrencyStep";
import { SetupCreditStep } from "../components/SetupCreditStep";
import { SetupPreferencesStep } from "../components/SetupPreferencesStep";
import { AuthErrorBanner } from "../components/AuthErrorBanner";

export default function Setup() {
  const { t } = useTranslation();
  const [step, setStep] = useState(0);
  const navigate = useNavigate();
  const { checkSettings } = useAuth();

  const form = useForm<SetupFormValues>({
    resolver: zodResolver(setupSchema),
    defaultValues: {
      language: "en",
      mainCurrency: "USD",
      defaultCreditConsumptionMode: "0",
      transactionPageSize: 100,
      defaultRecurringApplicability: "1",
      autoApplyOnLoad: true,
    },
  });

  const {
    control,
    trigger,
    getValues,
    formState: { errors },
  } = form;

  const updateSettingMutation = useMutation({
    mutationFn: async (body: {
      mainCurrency: string;
      defaultCreditConsumptionMode: number;
      supportedCurrencies: string[];
      language: string;
      transactionPageSize: number;
      defaultRecurringApplicability: number;
      autoApplyOnLoad: boolean;
    }) => {
      const { error } = await client.POST("/api/users/settings/setup", {
        body,
      });
      if (error) throw error;
    },
    onSuccess: async () => {
      await checkSettings();
      navigate("/dashboard");
    },
  });

  const steps = [
    {
      icon: Languages,
      title: t("setup.language.title"),
      description: t("setup.language.description"),
    },
    {
      icon: Wallet,
      title: t("setup.currency.title"),
      description: t("setup.currency.description"),
    },
    {
      icon: CreditCard,
      title: t("setup.credit.title"),
      description: t("setup.credit.description"),
    },
    {
      icon: Settings2,
      title: t("setup.preferences.title"),
      description: t("setup.preferences.description"),
    },
  ];

  const isLastStep = step === steps.length - 1;
  const currentStep = steps[step];
  const StepIcon = currentStep.icon;

  const handleNext = async () => {
    const valid = await (() => {
      switch (step) {
        case 0:
          return trigger("language");
        case 1:
          return trigger("mainCurrency");
        case 2:
          return trigger("defaultCreditConsumptionMode");
        default:
          return trigger([
            "transactionPageSize",
            "defaultRecurringApplicability",
            "autoApplyOnLoad",
          ]);
      }
    })();
    if (!valid) return;

    if (!isLastStep) {
      setStep((s) => s + 1);
      return;
    }

    const {
      mainCurrency,
      defaultCreditConsumptionMode,
      language,
      transactionPageSize,
      defaultRecurringApplicability,
      autoApplyOnLoad,
    } = getValues();
    updateSettingMutation.mutate({
      mainCurrency,
      defaultCreditConsumptionMode: parseInt(defaultCreditConsumptionMode, 10),
      supportedCurrencies: [mainCurrency],
      language,
      transactionPageSize,
      defaultRecurringApplicability: parseInt(defaultRecurringApplicability, 10),
      autoApplyOnLoad,
    });
  };

  return (
    <div className="min-h-screen flex items-center justify-center relative overflow-hidden bg-background">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute left-1/2 top-1/4 -translate-x-1/2 size-125 rounded-full bg-primary/6 blur-3xl" />
        <div className="absolute left-1/4 bottom-1/4 size-75 rounded-full bg-accent/5 blur-3xl" />
      </div>

      <div className="relative z-10 w-full max-w-md px-4">
        <div className="mb-8 flex flex-col items-center gap-2">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 ring-1 ring-primary/20">
            <Activity className="h-6 w-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold tracking-tight [text-shadow:0_0_12px_hsl(var(--primary)/0.6)]">
            {t("setup.title")}
          </h1>
          <p className="text-sm text-muted-foreground">{t("setup.subtitle")}</p>
        </div>

        <SetupStepIndicator steps={steps} currentStep={step} />

        <div className="rounded-2xl border bg-card/80 backdrop-blur-sm p-6 shadow-2xl shadow-black/50 ring-1 ring-white/5">
          <div className="mb-6 flex items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
              <StepIcon className="h-4.5 w-4.5 text-primary" />
            </div>
            <div>
              <h2 className="font-semibold text-lg leading-tight">
                {currentStep.title}
              </h2>
              <p className="text-sm text-muted-foreground mt-0.5">
                {currentStep.description}
              </p>
            </div>
          </div>

          <AuthErrorBanner
            error={
              updateSettingMutation.error
                ? t("setup.failed")
                : ""
            }
          />

          {step === 0 && (
            <SetupLanguageStep control={control} t={t} />
          )}

          {step === 1 && (
            <SetupCurrencyStep control={control} errors={errors} t={t} />
          )}

          {step === 2 && (
            <SetupCreditStep control={control} errors={errors} t={t} />
          )}

          {step === 3 && (
            <SetupPreferencesStep control={control} errors={errors} t={t} />
          )}

          <div className="mt-8 flex items-center gap-3">
            {step > 0 && (
              <Button
                variant="outline"
                className="flex-1"
                onClick={() => setStep((s) => s - 1)}
              >
                {t("setup.back")}
              </Button>
            )}
            <Button
              className="flex-1"
              onClick={handleNext}
              disabled={updateSettingMutation.isPending}
            >
              {updateSettingMutation.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              {isLastStep ? t("setup.complete") : t("setup.continue")}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
