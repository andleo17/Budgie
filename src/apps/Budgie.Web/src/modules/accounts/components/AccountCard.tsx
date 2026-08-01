import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AreaChart, Area, Tooltip, ResponsiveContainer } from "recharts";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";
import {
  Card,
  CardContent,
  CardHeader,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { MoreHorizontal, Pencil, Trash2, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatCurrency } from "@/lib/format";
import { ACCOUNT_TYPE_LABELS, AccountType } from "@/lib/constants";

type AccountResponse = components["schemas"]["AccountResponse"];

interface AccountCardProps {
  account: AccountResponse;
  dailyBalances: number[];
  onEdit: (a: AccountResponse) => void;
}

function badgeClass(typeCode: number) {
  switch (typeCode) {
    case AccountType.Checking:
      return "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 border-transparent";
    case AccountType.Savings:
      return "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 border-transparent";
    case AccountType.Credit:
      return "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400 border-transparent";
    default:
      return "";
  }
}

export function AccountCard({ account, dailyBalances, onEdit }: AccountCardProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/api/finances/accounts/{id}", {
        params: { path: { id } },
      });
      if (error) throw error;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
  });

  const isCredit = account.type === AccountType.Credit;
  const balance = Number(account.balance);
  const creditLimit = account.creditLimit != null ? Number(account.creditLimit) : null;

  const sparklineData = dailyBalances.map((value, index) => ({ day: index, value }));

  return (
    <>
      <Card
        className="group cursor-pointer transition-all hover:shadow-md"
        onClick={(e) => {
          if ((e.target as HTMLElement).closest('[data-slot="dropdown-menu-trigger"], [data-slot="dropdown-menu-content"]')) return;
          const now = new Date();
          if (account.type === AccountType.Credit && account.billingCycleDay != null) {
            const periodKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
            navigate(`/transactions?accounts=${account.id}&period=${periodKey}`);
          } else {
            const periodKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`;
            navigate(`/transactions?accounts=${account.id}&period=${periodKey}`);
          }
        }}
      >
        <CardHeader className="flex flex-row items-start justify-between gap-2 p-4 pb-0">
          <div className="flex items-center gap-2 min-w-0">
            <span className="font-semibold truncate">{account.name}</span>
            <Badge variant="outline" className={cn("shrink-0", badgeClass(account.type))}>
              {t(`accounts.${ACCOUNT_TYPE_LABELS[account.type] ?? "unknown"}`)}
            </Badge>
          </div>
            <DropdownMenu>
            <DropdownMenuTrigger
              render={<Button variant="ghost" size="icon-xs" className="size-6" />}
            >
              <MoreHorizontal className="size-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="min-w-40" onClick={(e) => e.stopPropagation()}>
              <DropdownMenuItem onClick={() => onEdit(account)}>
                <Pencil className="mr-2 size-4" />
                <span className="whitespace-nowrap">{t("accounts.edit")}</span>
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => setDeleteOpen(true)}>
                <Trash2 className="mr-2 size-4" />
                <span className="whitespace-nowrap">{t("accounts.delete")}</span>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </CardHeader>
        <CardContent className="p-4 pt-2">
          {isCredit && creditLimit !== null ? (
            <div className="mb-1">
              <span className="text-2xl font-bold">{formatCurrency(balance, account.currency)}</span>
              <span className="text-muted-foreground text-sm ml-1">
                {t("accounts.of")} {formatCurrency(creditLimit, account.currency)}
              </span>
              <div className="text-xs text-muted-foreground">{t("accounts.availableCredit")}</div>
            </div>
          ) : (
            <div className="mb-1">
              <span className="text-2xl font-bold">{formatCurrency(balance, account.currency)}</span>
            </div>
          )}
          <div className="h-10 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={sparklineData} margin={{ top: 0, right: 0, bottom: 0, left: 0 }}>
                <defs>
                  <linearGradient id={`sf-${account.id}`} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="hsl(var(--primary))" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="hsl(var(--primary))" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <Tooltip
                  content={({ active, payload }) => {
                    if (!active || !payload?.length) return null;
                    const val = Number(payload[0].value);
                    return (
                      <span className="rounded-md bg-popover px-2 py-1 text-xs font-medium text-popover-foreground shadow-sm ring-1 ring-foreground/10">
                        {formatCurrency(val, account.currency)}
                      </span>
                    );
                  }}
                  cursor={false}
                  isAnimationActive={false}
                />
                <Area
                  type="monotone"
                  dataKey="value"
                  stroke="hsl(var(--primary))"
                  strokeWidth={1.5}
                  fill={`url(#sf-${account.id})`}
                  isAnimationActive={false}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </CardContent>
      </Card>

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent onClick={(e) => e.stopPropagation()}>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("accounts.deleteConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("accounts.deleteConfirmDesc")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("accounts.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={async () => {
                await deleteMutation.mutateAsync(account.id);
                queryClient.invalidateQueries({ queryKey: ["accounts"] });
                setDeleteOpen(false);
              }}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending && <Loader2 className="mr-1 size-4 animate-spin" />}
              {t("accounts.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
