import { z } from "zod"

export const createGoalSchema = z.object({
  accountId: z.string().optional(),
  targetAmount: z.string().optional(),
  targetDate: z.string().optional(),
})

export const editGoalSchema = z.object({
  targetAmount: z.string().optional(),
  targetDate: z.string().optional(),
})

export type CreateGoalFormValues = z.infer<typeof createGoalSchema>
export type EditGoalFormValues = z.infer<typeof editGoalSchema>
