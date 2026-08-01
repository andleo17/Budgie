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
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{label}</CardTitle>
        <Icon className={`h-4 w-4 ${iconColor}`} />
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        ) : (
          <>
            <div className="text-2xl font-bold">{value}</div>
            <p className="text-xs text-muted-foreground mt-1">{subLabel}</p>
          </>
        )}
      </CardContent>
    </Card>
  );
}
