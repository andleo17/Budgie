import { z } from "zod"

export const createAccountSchema = z.object({
  name: z.string().min(1, "Account name is required"),
  type: z.string().min(1, "Type is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  initialBalance: z.string().min(1, "Initial balance is required"),
})

export type CreateAccountFormValues = z.infer<typeof createAccountSchema>
