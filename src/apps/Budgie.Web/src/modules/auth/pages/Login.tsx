import { useTranslation } from "react-i18next";
import { useMutation } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, Link, Navigate } from "react-router-dom";
import client from "@/lib/api-client";
import { useAuth } from "@/modules/auth/useAuth";
import { useRegistrationStatus } from "@/modules/auth/useRegistrationStatus";
import { loginSchema, type LoginFormValues } from "@/modules/auth/schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldLabel, FieldError } from "@/components/ui/field";
import { Loader2 } from "lucide-react";
import { AuthLayout } from "../components/AuthLayout";
import { AuthErrorBanner } from "../components/AuthErrorBanner";

export default function Login() {
  const { t } = useTranslation();
  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
  });
  const { setToken } = useAuth();
  const navigate = useNavigate();
  const { data: registrationStatus } = useRegistrationStatus();
  const mutation = useMutation({
    mutationFn: async (body: { email: string; password: string }) => {
      const { data, error } = await client.POST("/api/users/login", { body });
      if (error) throw error;
      return data;
    },
    onSuccess: (data) => {
      localStorage.setItem("refreshToken", data.refreshToken);
      setToken(data.token);
      navigate("/dashboard");
    },
  });

  const onSubmit = (values: LoginFormValues) => {
    mutation.mutate({ email: values.email, password: values.password });
  };

  const error =
    mutation.error &&
    !form.formState.errors.email &&
    !form.formState.errors.password
      ? mutation.error?.message || t("auth.invalidCredentials")
      : "";

  if (registrationStatus?.enabled && !registrationStatus.hasUsers) {
    return <Navigate to="/register" replace />;
  }

  const registrationClosed = registrationStatus?.enabled === false;

  return (
    <AuthLayout tagline={t("auth.tagline")}>
      <div className="mb-6">
        <h2 className="text-xl font-semibold">{t("auth.welcomeBack")}</h2>
        <p className="text-sm text-muted-foreground mt-1">
          {t("auth.signInToAccount")}
        </p>
      </div>

      <AuthErrorBanner error={error} />

      <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
        <Controller
          name="email"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={field.name}>{t("auth.email")}</FieldLabel>
              <Input
                {...field}
                id={field.name}
                type="email"
                placeholder={t("auth.emailPlaceholder")}
                autoComplete="email"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid && (
                <FieldError errors={[fieldState.error]} />
              )}
            </Field>
          )}
        />
        <Controller
          name="password"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={field.name}>{t("auth.password")}</FieldLabel>
              <Input
                {...field}
                id={field.name}
                type="password"
                autoComplete="current-password"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid && (
                <FieldError errors={[fieldState.error]} />
              )}
            </Field>
          )}
        />
        <Button
          type="submit"
          className="mt-2 w-full"
          disabled={mutation.isPending}
        >
          {mutation.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : null}
          {t("auth.signIn")}
        </Button>
      </form>

      {registrationClosed ? (
        <p className="mt-5 text-center text-sm text-muted-foreground">
          {t("auth.registrationClosed")}
        </p>
      ) : (
        <p className="mt-5 text-center text-sm text-muted-foreground">
          {t("auth.noAccount")}{" "}
          <Link
            to="/register"
            className="text-primary hover:underline underline-offset-4 font-medium"
          >
            {t("auth.createOne")}
          </Link>
        </p>
      )}
    </AuthLayout>
  );
}
