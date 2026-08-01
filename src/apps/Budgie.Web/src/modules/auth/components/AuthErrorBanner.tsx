export function AuthErrorBanner({ error }: { error: string }) {
  if (!error) return null
  return (
    <div className="mb-4 rounded-lg bg-destructive/10 border border-destructive/20 px-3 py-2.5 text-sm text-destructive">
      {error}
    </div>
  )
}
