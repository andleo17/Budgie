import { z } from "zod"

export const loginSchema = z.object({
  email: z.string().min(1).email(),
  password: z.string().min(6),
})

export const registerSchema = z.object({
  name: z.string().min(1).max(100),
  email: z.string().min(1).email(),
  password: z.string().min(6),
})

export const setupSchema = z.object({
  mainCurrency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  defaultCreditConsumptionMode: z.string().min(1),
})

export type LoginFormValues = z.infer<typeof loginSchema>
export type RegisterFormValues = z.infer<typeof registerSchema>
export type SetupFormValues = z.infer<typeof setupSchema>
