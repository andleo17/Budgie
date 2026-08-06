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
  language: z.string().min(1),
  mainCurrency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  defaultCreditConsumptionMode: z.string().min(1),
  transactionPageSize: z.number().int().positive().max(1000),
  defaultRecurringApplicability: z.string().min(1),
  autoApplyOnLoad: z.boolean(),
})

export type LoginFormValues = z.infer<typeof loginSchema>
export type RegisterFormValues = z.infer<typeof registerSchema>
export type SetupFormValues = z.infer<typeof setupSchema>
