import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { differenceInDays, getDate, startOfMonth, endOfMonth } from "date-fns";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import { TransactionType, ACCOUNT_TYPE_ORDER } from "@/lib/constants";

type AccountResponse = components["schemas"]["AccountResponse"];
type TransactionResponse = components["schemas"]["TransactionResponse"];

export function useAccounts() {
  return useQuery({
    queryKey: ["accounts"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts");
      if (error) throw error;
      return data;
    },
  });
}

export function useAccount(id: string | null) {
  return useQuery({
    queryKey: ["accounts", id],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/accounts/{id}", {
        params: { path: { id: id! } },
      });
      if (error) throw error;
      return data;
    },
    enabled: !!id,
  });
}

export function useGroupedAccounts(accounts: AccountResponse[] | undefined) {
  return useMemo(() => {
    if (!accounts) return new Map<number, AccountResponse[]>();
    const groups = new Map<number, AccountResponse[]>();
    for (const type of ACCOUNT_TYPE_ORDER) {
      const filtered = accounts.filter((a) => a.type === type);
      if (filtered.length > 0) groups.set(type, filtered);
    }
    return groups;
  }, [accounts]);
}

export function useSparklineData(
  accounts: AccountResponse[] | undefined,
  transactions: TransactionResponse[] | undefined,
) {
  return useMemo(() => {
    if (!accounts || !transactions) return new Map<string, number[]>();

    const now = new Date();
    const monthStart = startOfMonth(now);
    const monthEnd = endOfMonth(now);
    const totalDays = differenceInDays(monthEnd, monthStart) + 1;
    const todayOffset = getDate(now);

    const map = new Map<string, number[]>();
    const txByAccount = new Map<string, TransactionResponse[]>();

    for (const tx of transactions) {
      const list = txByAccount.get(tx.accountId) ?? [];
      list.push(tx);
      txByAccount.set(tx.accountId, list);
    }

    for (const account of accounts) {
      const txs = txByAccount.get(account.id) ?? [];
      const dailyNets = new Float64Array(totalDays);

      for (const tx of txs) {
        if (tx.isInitialBalance) continue;
        if (tx.parentTransactionId && tx.type === 3) continue;
        const txDate = new Date(tx.date);
        const day = differenceInDays(txDate, monthStart);
        if (day < 0 || day >= totalDays) continue;
        const sign = tx.type === TransactionType.Income || tx.type === TransactionType.Collection ? 1 : -1;
        dailyNets[day] += Number(tx.amount) * sign;
      }

      const monthNet = dailyNets.reduce((a, b) => a + b, 0);
      const accountBalance = Number(account.balance);
      let running = accountBalance - monthNet;

      const balances: number[] = [];
      const limit = Math.min(totalDays, todayOffset);
      for (let d = 0; d < limit; d++) {
        running += dailyNets[d];
        balances.push(running);
      }
      for (let d = limit; d < totalDays; d++) {
        balances.push(running);
      }

      map.set(account.id, balances);
    }

    return map;
  }, [accounts, transactions]);
}
