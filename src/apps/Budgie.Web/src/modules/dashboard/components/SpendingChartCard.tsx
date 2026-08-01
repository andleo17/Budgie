import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { formatCurrency } from "@/lib/format";
import type { components } from "@/lib/api-types";
import { Activity, Loader2 } from "lucide-react";
import {
  Bar,
  BarChart,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

interface SpendingChartCardProps {
  spending: components["schemas"]["CategorySpending"][];
  currency?: string;
  isLoading: boolean;
  t: (key: string) => string;
}

export function SpendingChartCard({
  spending,
  currency,
  isLoading,
  t,
}: SpendingChartCardProps) {
  return (
    <Card className="xl:col-span-2">
      <CardHeader>
        <CardTitle>{t("dashboard.spendingByCategory")}</CardTitle>
        <CardDescription>{t("dashboard.spendingDesc")}</CardDescription>
      </CardHeader>
      <CardContent className="pl-2">
        {isLoading ? (
          <div className="h-87.5 flex items-center justify-center">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : spending && spending.length > 0 ? (
          <div className="h-87.5">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={spending}
                margin={{ top: 20, right: 30, left: 20, bottom: 5 }}
              >
                <XAxis
                  dataKey="categoryName"
                  stroke="#888888"
                  fontSize={12}
                  tickLine={false}
                  axisLine={false}
                />
                <YAxis
                  stroke="#888888"
                  fontSize={12}
                  tickLine={false}
                  axisLine={false}
                  tickFormatter={(value) => formatCurrency(value, currency)}
                />
                <Tooltip
                  formatter={(value) => [
                    formatCurrency(Number(value), currency),
                    t("dashboard.amount"),
                  ]}
                  cursor={{ fill: "rgba(255,255,255,0.04)" }}
                  contentStyle={{
                    borderRadius: "8px",
                    backgroundColor: "hsl(222 40% 11%)",
                    border: "1px solid hsl(220 13% 20%)",
                    boxShadow: "0 8px 24px rgb(0 0 0 / 0.4)",
                    color: "hsl(210 40% 98%)",
                    fontSize: "13px",
                  }}
                />
                <Bar dataKey="totalAmount" radius={[4, 4, 0, 0]}>
                  {spending.map((entry, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={entry.color || "hsl(var(--primary))"}
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : (
          <div className="h-87.5 flex items-center justify-center text-muted-foreground flex-col gap-2">
            <Activity className="h-10 w-10 opacity-20" />
            <p>{t("dashboard.noSpending")}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
