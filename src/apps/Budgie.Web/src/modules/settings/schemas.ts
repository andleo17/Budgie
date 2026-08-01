import { z } from "zod"
export const settingsSchema = z.object({
  mainCurrency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
})
export type SettingsFormValues = z.infer<typeof settingsSchema>
