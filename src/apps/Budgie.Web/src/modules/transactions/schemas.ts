import { z } from "zod"

export const incomeExpenseSchema = z.object({
  type: z.string().min(1, "Type is required"),
  date: z.string().min(1, "Date is required"),
  accountId: z.string().min(1, "Account is required"),
  amount: z.string().min(1, "Amount is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  exchangeRate: z.string().optional(),
  convertedAmount: z.string().optional(),
  categoryId: z.string().optional(),
  notes: z.string().optional(),
  installments: z.string().optional(),
  isInterestFree: z.boolean().optional(),
})

export const transferSchema = z.object({
  date: z.string().min(1, "Date is required"),
  sourceAccountId: z.string().min(1, "Source account is required"),
  destinationAccountId: z.string().min(1, "Destination account is required"),
  amount: z.string().min(1, "Amount is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  exchangeRate: z.string().optional(),
  convertedAmount: z.string().optional(),
  notes: z.string().optional(),
})

export const loanSchema = z.object({
  personId: z.string().min(1, "Person is required"),
  accountId: z.string().min(1, "Account is required"),
  amount: z.string().min(1, "Amount is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  exchangeRate: z.string().optional(),
  convertedAmount: z.string().optional(),
  date: z.string().min(1, "Date is required"),
  dueDate: z.string().optional(),
  notes: z.string().optional(),
})

export const collectionSchema = z.object({
  personId: z.string().min(1, "Person is required"),
  accountId: z.string().min(1, "Account is required"),
  amount: z.string().min(1, "Amount is required"),
  currency: z.string().regex(/^[A-Z]{3}$/, "Must be a 3-letter currency code"),
  exchangeRate: z.string().optional(),
  convertedAmount: z.string().optional(),
  date: z.string().min(1, "Date is required"),
  notes: z.string().optional(),
  selectedDebtIds: z.array(z.string()).optional(),
})

export type IncomeExpenseFormValues = z.infer<typeof incomeExpenseSchema>
export type TransferFormValues = z.infer<typeof transferSchema>
export type LoanFormValues = z.infer<typeof loanSchema>
export type CollectionFormValues = z.infer<typeof collectionSchema>

export interface TransactionParticipantForm {
  personId: string
  personName: string
  contributedAmount: string
  paymentCondition: "tag" | "cash" | "credit"
  destinationAccountId?: string
}
