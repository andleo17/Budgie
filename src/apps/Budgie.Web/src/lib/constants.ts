export const AccountType = {
  Checking: 0,
  Savings: 1,
  Credit: 2,
  Cash: 3,
  Loan: 4,
} as const;

export const TransactionType = {
  Income: 0,
  Expense: 1,
  Transfer: 2,
  Loan: 3,
  Collection: 4,
} as const;

export const Applicability = {
  Template: 0,
  Automatic: 1,
  Manual: 2,
} as const;

export const ACCOUNT_TYPE_ORDER = [
  AccountType.Checking,
  AccountType.Savings,
  AccountType.Credit,
  AccountType.Cash,
  AccountType.Loan,
] as const;

export const ACCOUNT_TYPE_SECTION_KEY: Record<number, string> = {
  [AccountType.Checking]: "checkingAccounts",
  [AccountType.Savings]: "savingsAccounts",
  [AccountType.Credit]: "creditCards",
  [AccountType.Cash]: "cashAccounts",
  [AccountType.Loan]: "loanAccounts",
};

export const TRANSACTION_TYPE_LABELS = ["income", "expense", "transfer", "loan", "collection"] as const;

export const CATEGORY_TYPE_LABELS = ["income", "expense", "transfer", "loan", "repayment"] as const;

export const ACCOUNT_TYPE_LABELS: Record<number, string> = {
  [AccountType.Checking]: "checking",
  [AccountType.Savings]: "savings",
  [AccountType.Credit]: "credit",
  [AccountType.Cash]: "cash",
  [AccountType.Loan]: "loan",
};

export const VIEW_MODES = ["daily", "weekly", "monthly", "yearly"] as const;

export type ViewMode = (typeof VIEW_MODES)[number];

export function parseViewMode(value: string | null): ViewMode {
  if (VIEW_MODES.includes(value as ViewMode)) return value as ViewMode;
  return "daily";
}
