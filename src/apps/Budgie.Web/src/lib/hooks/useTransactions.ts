import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { TransactionType } from "@/lib/constants";

type TransactionResponse = components["schemas"]["TransactionResponse"];

export function useSettings() {
  return useQuery({
    queryKey: ["settings"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/users/settings");
      if (error) throw error;
      return data;
    },
  });
}

export function useTransactions(params: {
  accounts?: string;
  categories?: string;
  types?: string;
  from?: string;
  to?: string;
  search?: string;
  personIds?: string;
  tagIds?: string;
  hasSingleAccount?: boolean;
  page?: number;
  pageSize?: number;
  enabled?: boolean;
}) {
  const { accounts, categories, types, from, to, search, personIds, tagIds, hasSingleAccount, page, pageSize, enabled } = params;

  return useQuery({
    queryKey: ["transactions", accounts, categories, types, from, to, search, personIds, tagIds, hasSingleAccount, page],
    queryFn: async () => {
      const selectedPersonIds = personIds?.split(",").filter(Boolean) ?? [];
      const { data, error } = await client.GET("/api/finances/transactions", {
        params: {
          query: {
            accountIds: accounts || undefined,
            categoryIds: categories || undefined,
            transactionTypes: types || undefined,
            startDate: from || undefined,
            endDate: to || undefined,
            search: search || undefined,
            includeRunningBalance: hasSingleAccount || undefined,
            personId: selectedPersonIds[0] || undefined,
            tagIds: tagIds || undefined,
            page,
            pageSize,
          },
        },
      });
      if (error) throw error;
      return data;
    },
    enabled,
  });
}

export function useTransactionSummary(params: {
  from?: string;
  to?: string;
  accountIds?: string;
}) {
  const { from, to, accountIds } = params;

  return useQuery({
    queryKey: ["transactionSummary", from, to, accountIds],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/transactions/summary", {
        params: {
          query: {
            startDate: from || undefined,
            endDate: to || undefined,
            accountIds: accountIds || undefined,
          },
        },
      });
      if (error) throw error;
      return data;
    },
  });
}

export function useTemplates() {
  return useQuery({
    queryKey: ["recurring-transactions"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/transactions/templates");
      if (error) throw error;
      return data ?? [];
    },
  });
}

export interface CurrencyBalance {
  currency: string;
  balance: number;
}

export function useGroupBalances(transactions: TransactionResponse[]): CurrencyBalance[] {
  return useMemo(() => {
    const byCurrency = new Map<string, number>();
    for (const tx of transactions) {
      if (tx.parentTransactionId && tx.type === TransactionType.Loan) continue;
      const delta = tx.type === TransactionType.Income || tx.type === TransactionType.Collection ? Number(tx.amount) : -Number(tx.amount);
      byCurrency.set(tx.currency, (byCurrency.get(tx.currency) ?? 0) + delta);
    }
    return Array.from(byCurrency.entries())
      .map(([currency, balance]) => ({ currency, balance }))
      .filter((c) => c.balance !== 0)
      .sort((a, b) => Math.abs(b.balance) - Math.abs(a.balance));
  }, [transactions]);
}
