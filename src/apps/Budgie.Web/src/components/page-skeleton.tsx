import { Skeleton } from "@/components/ui/skeleton";

interface PageSkeletonProps {
  header?: boolean;
  cards?: number;
  rows?: number;
}

export function PageSkeleton({ header = true, cards = 3, rows = 4 }: PageSkeletonProps) {
  return (
    <div className="flex w-full flex-col gap-4" aria-busy="true" aria-label="Loading">
      {header && (
        <div className="flex flex-col gap-2">
          <Skeleton className="h-7 w-48" />
          <Skeleton className="h-4 w-72 max-w-full" />
        </div>
      )}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: cards }).map((_, i) => (
          <div key={i} className="rounded-lg border bg-card p-4">
            <div className="flex items-center gap-3">
              <Skeleton className="size-10 rounded-full" />
              <div className="flex flex-1 flex-col gap-2">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-3 w-16" />
              </div>
            </div>
            <Skeleton className="mt-4 h-8 w-32" />
          </div>
        ))}
      </div>
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-14 w-full" />
      ))}
    </div>
  );
}
