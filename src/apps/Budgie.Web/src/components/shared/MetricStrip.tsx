import { Fragment } from "react";
import { cn } from "@/lib/utils";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";

export type MetricTone = "positive" | "negative" | "neutral";

export interface MetricItem {
  label: string;
  /** Preformatted display value (e.g. "+$1.240,50") */
  value?: string;
  tone?: MetricTone;
}

const toneClasses: Record<MetricTone, string> = {
  positive: "amount-positive",
  negative: "amount-negative",
  neutral: "text-foreground",
};

interface MetricStripProps {
  metrics: MetricItem[];
  isLoading?: boolean;
  className?: string;
}

/**
 * Single-row compact metric strip (native-app style).
 * Always one row: N equal columns, ~44px tall, never reflows
 * when the number of metrics is known upfront.
 */
export function MetricStrip({ metrics, isLoading = false, className }: MetricStripProps) {
  return (
    <div
      className={cn("flex items-stretch rounded-2xl border bg-card", className)}
      aria-busy={isLoading || undefined}
    >
      {metrics.map((metric, i) => (
        <Fragment key={`${metric.label}-${i}`}>
          {i > 0 && <Separator orientation="vertical" />}
          <div className="flex min-w-0 flex-1 flex-col items-center justify-center gap-1 px-2 py-2">
            {isLoading ? (
              <>
                <Skeleton className="h-2.5 w-12" />
                <Skeleton className="h-3.5 w-16 max-w-full" />
              </>
            ) : (
              <>
                <span className="text-[11px] leading-none text-muted-foreground">
                  {metric.label}
                </span>
                <span
                  className={cn(
                    "truncate text-sm leading-none font-semibold tabular-nums",
                    toneClasses[metric.tone ?? "neutral"],
                  )}
                >
                  {metric.value}
                </span>
              </>
            )}
          </div>
        </Fragment>
      ))}
    </div>
  );
}
