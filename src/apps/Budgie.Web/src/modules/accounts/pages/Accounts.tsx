import { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import client from "@/lib/api-client";
import { PlusCircle, Loader2, Search, Filter, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccountFormDialog } from "@/modules/accounts/components/AccountFormDialog";
import { AccountCard } from "@/modules/accounts/components/AccountCard";
import { useAccounts, useSparklineData, useGroupedAccounts } from "@/lib/hooks/useAccounts";
import { ACCOUNT_TYPE_ORDER, ACCOUNT_TYPE_SECTION_KEY } from "@/lib/constants";
import type { components } from "@/lib/api-types";

type AccountResponse = components["schemas"]["AccountResponse"];

export default function Accounts() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [editAccount, setEditAccount] = useState<AccountResponse | null>(null);
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get("search") ?? "";
  const typeFilter = searchParams.get("type");
  const typeFilterNum = typeFilter !== null ? Number(typeFilter) : null;

  const prevCreateOpen = useRef(isCreateOpen);
  const prevEditAccount = useRef(editAccount);

  useEffect(() => {
    if (prevCreateOpen.current && !isCreateOpen) {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    }
    prevCreateOpen.current = isCreateOpen;
  }, [isCreateOpen, queryClient]);

  useEffect(() => {
    if (prevEditAccount.current && !editAccount) {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    }
    prevEditAccount.current = editAccount;
  }, [editAccount, queryClient]);

  const { data: accountsData, isLoading: accountsLoading } = useAccounts();

  const { data: monthlyTxs } = useQuery({
    queryKey: ["transactions", "sparklines"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/transactions", {
        params: {
          query: {
            startDate: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString(),
            endDate: new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).toISOString(),
          },
        },
      });
      if (error) throw error;
      return data;
    },
    enabled: !!accountsData,
  });

  const sparklineMap = useSparklineData(accountsData, monthlyTxs?.items);
  const groupedAccounts = useGroupedAccounts(accountsData);

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold tracking-tight md:text-3xl">
            {t("accounts.title")}
          </h1>
          <p className="text-muted-foreground">{t("accounts.description")}</p>
        </div>
        <button
          onClick={() => setIsCreateOpen(true)}
          className="inline-flex h-8 shrink-0 items-center justify-center gap-1.5 rounded-lg border border-transparent bg-primary px-2.5 text-sm font-medium text-primary-foreground transition-all hover:bg-primary/80 active:translate-y-px disabled:pointer-events-none disabled:opacity-50 select-none whitespace-nowrap outline-none"
        >
          <PlusCircle className="mr-1 h-4 w-4" />
          {t("accounts.addAccount")}
        </button>
      </div>

      {accountsLoading ? (
        <div className="flex h-48 items-center justify-center">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : !accountsData?.length ? (
        <div className="flex h-48 items-center justify-center rounded-xl border bg-card text-muted-foreground">
          {t("accounts.noAccounts")}
        </div>
      ) : (
        <>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="relative flex-1">
              <Search className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder={t("accounts.searchPlaceholder")}
                value={search}
                onChange={(e) => {
                  const next = e.target.value;
                  setSearchParams((prev) => {
                    if (next) prev.set("search", next);
                    else prev.delete("search");
                    return prev;
                  });
                }}
                className="pl-8"
              />
              {search && (
                <button
                  onClick={() =>
                    setSearchParams((prev) => {
                      prev.delete("search");
                      return prev;
                    })
                  }
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                >
                  <X className="size-4" />
                </button>
              )}
            </div>
            <Select
              value={typeFilter ?? "all"}
              onValueChange={(v) => {
                setSearchParams((prev) => {
                  if (v === "all" || v === null) prev.delete("type");
                  else prev.set("type", v);
                  return prev;
                });
              }}
              items={[
                { label: t("accounts.allTypes"), value: "all" },
                { label: t("accounts.checking"), value: "0" },
                { label: t("accounts.savings"), value: "1" },
                { label: t("accounts.credit"), value: "2" },
              ]}
            >
              <SelectTrigger className="w-full sm:w-44">
                <Filter className="mr-2 size-4" />
                <SelectValue />
              </SelectTrigger>
              <SelectContent alignItemWithTrigger={false}>
                <SelectGroup>
                  <SelectItem value="all">{t("accounts.allTypes")}</SelectItem>
                  <SelectItem value="0">{t("accounts.checking")}</SelectItem>
                  <SelectItem value="1">{t("accounts.savings")}</SelectItem>
                  <SelectItem value="2">{t("accounts.credit")}</SelectItem>
                </SelectGroup>
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-8">
            {ACCOUNT_TYPE_ORDER.map((type) => {
              if (typeFilterNum !== null && type !== typeFilterNum) return null;
              const accountsOfType = groupedAccounts.get(type);
              if (!accountsOfType) return null;
              const filtered = search
                ? accountsOfType.filter((a) => a.name.toLowerCase().includes(search.toLowerCase()))
                : accountsOfType;
              if (!filtered.length) return null;
              return (
                <section key={type}>
                  <h2 className="mb-3 text-lg font-semibold">{t(`accounts.${ACCOUNT_TYPE_SECTION_KEY[type]}`)}</h2>
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
                    {filtered.map((account) => (
                      <AccountCard
                        key={account.id}
                        account={account}
                        dailyBalances={sparklineMap.get(account.id) ?? []}
                        onEdit={setEditAccount}
                      />
                    ))}
                  </div>
                </section>
              );
            })}
          </div>
        </>
      )}

      <AccountFormDialog open={isCreateOpen} onOpenChange={setIsCreateOpen} />
      <AccountFormDialog
        open={!!editAccount}
        onOpenChange={(v) => { if (!v) setEditAccount(null) }}
        account={editAccount}
      />
    </>
  );
}
