import { z } from "zod"

export const profileSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
})

export type ProfileFormValues = z.infer<typeof profileSchema>
