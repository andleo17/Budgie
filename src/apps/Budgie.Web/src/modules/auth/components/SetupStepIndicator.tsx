export function SetupStepIndicator({
  steps,
  currentStep,
}: {
  steps: { icon: React.ElementType; title: string; description: string }[]
  currentStep: number
}) {
  return (
    <div className="mb-6 flex items-center gap-2 justify-center">
      {steps.map((_, i) => (
        <div
          key={i}
          className={`h-1.5 rounded-full transition-all duration-300 ${i === currentStep ? "w-8 bg-primary" : i < currentStep ? "w-4 bg-primary/40" : "w-4 bg-border"}`}
        />
      ))}
    </div>
  )
}
