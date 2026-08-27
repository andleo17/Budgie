import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Loader2 } from "lucide-react";

interface SummaryStatCardProps {
  label: string;
  value: string;
  subLabel: string;
  icon: React.ElementType;
  iconColor: string;
  isLoading: boolean;
}

export function SummaryStatCard({
  label,
  value,
  subLabel,
  icon: Icon,
  iconColor,
  isLoading,
}: SummaryStatCardProps) {
  return (
    <Card className="gap-2 py-3 sm:py-4" size="sm">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-0">
        <CardTitle className="text-xs font-medium sm:text-sm">{label}</CardTitle>
        <Icon className={`size-3.5 sm:size-4 ${iconColor}`} />
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Loader2 className="size-5 animate-spin text-muted-foreground sm:size-6" />
        ) : (
          <>
            <div className="truncate text-lg font-bold tabular-nums sm:text-2xl">{value}</div>
            <p className="mt-1 hidden text-xs text-muted-foreground sm:block">{subLabel}</p>
          </>
        )}
      </CardContent>
    </Card>
  );
}
