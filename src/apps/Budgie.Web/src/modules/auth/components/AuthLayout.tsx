import { Activity } from "lucide-react"

export function AuthLayout({
  children,
  tagline,
}: {
  children: React.ReactNode
  tagline: string
}) {
  return (
    <div className="min-h-screen flex items-center justify-center relative overflow-hidden bg-background">
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
        <div className="size-150 rounded-full bg-primary/5 blur-3xl" />
      </div>
      <div className="relative z-10 w-full max-w-sm px-4">
        <div className="mb-8 flex flex-col items-center gap-2">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 ring-1 ring-primary/20">
            <Activity className="h-6 w-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold tracking-tight [text-shadow:0_0_12px_hsl(var(--primary)/0.6)]">
            Budgie
          </h1>
          <p className="text-sm text-muted-foreground">{tagline}</p>
        </div>
        <div className="rounded-2xl border bg-card/80 backdrop-blur-sm p-6 shadow-2xl shadow-black/50 ring-1 ring-white/5">
          {children}
        </div>
      </div>
    </div>
  )
}
