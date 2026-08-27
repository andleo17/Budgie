import type { Page } from "@playwright/test"
import { mockApi } from "./mocks/api"

export const authFixture = {
  token:
    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJlbWFpbCI6ImUydUBlZXUudGVzdCIsIm5hbWUiOiJFMiBVc2VyIiwiZXhwIjo5OTk5OTk5OTk5fQ.sig",
  refreshToken: "e2e-refresh-token",
  userId: "11111111-1111-1111-1111-111111111111",
}

export const mockSettings = {
  isConfigured: true,
  language: "en",
  mainCurrency: "USD",
  supportedCurrencies: ["USD", "EUR"],
  defaultCreditConsumptionMode: "Total",
  transactionPageSize: 50,
  defaultRecurringApplicability: "Manual",
  autoApplyOnLoad: false,
}

export const mockDashboard = {
  totalBalance: 5230.5,
  monthlyIncome: 3200,
  monthlyExpenses: 1450.75,
  netFlow: 1749.25,
  currency: "USD",
}

export const mockAccounts = [
  {
    id: "a1-1111-1111-1111-111111111111",
    name: "Main Checking",
    balance: 3200.5,
    currency: "USD",
    type: 0,
    color: "#4f46e5",
    isArchived: false,
  },
  {
    id: "a2-2222-2222-2222-222222222222",
    name: "Savings",
    balance: 2100,
    currency: "USD",
    type: 1,
    color: "#10b981",
    isArchived: false,
  },
]

export const mockTransactions = {
  items: [
    {
      id: "t1",
      accountId: mockAccounts[0].id,
      amount: -42.5,
      currency: "USD",
      date: new Date().toISOString(),
      description: "Groceries",
      categoryId: null,
      type: 1,
    },
  ],
  total: 1,
}

export async function seedLoggedIn(page: Page): Promise<void> {
  await page.addInitScript(
    ({ token, refreshToken }) => {
      localStorage.setItem("token", token)
      localStorage.setItem("refreshToken", refreshToken)
    },
    { token: authFixture.token, refreshToken: authFixture.refreshToken },
  )

  const m = mockApi(page)
  m.handle("GET", "/api/users/registration-status", {
    initialized: true,
    allowRegistration: false,
  })
  m.handle("GET", "/api/users/settings", mockSettings)
  m.handle("GET", "/api/finances/reports/dashboard", mockDashboard)
  m.handle("GET", "/api/finances/reports/goals", [])
  m.handle("GET", "/api/finances/reports/spending-by-category", [])
  m.handle("GET", "/api/finances/accounts", mockAccounts)
  m.handle("GET", "/api/finances/categories", [])
  m.handle("GET", "/api/finances/goals", [])
  m.handle("GET", "/api/relationships/persons", [])
  m.handle("GET", "/api/relationships/tags", [])
  // Transactions list — queried by Accounts sparkline + other pages.
  // Use wildcard handler for query-string variants by matching pathname only
  // (ApiMock matches pathname, so any query string is fine).
  m.handle("GET", "/api/finances/transactions", mockTransactions)
  m.handle("GET", "/api/finances/transactions/summary", {
    income: 3200,
    expenses: 1450.75,
    net: 1749.25,
  })
}

export async function mockRegistrationOpen(page: Page): Promise<void> {
  await mockApi(page).handle("GET", "/api/users/registration-status", {
    initialized: false,
    allowRegistration: true,
  })
}

export async function mockRegistrationClosed(page: Page): Promise<void> {
  await mockApi(page).handle("GET", "/api/users/registration-status", {
    initialized: true,
    allowRegistration: false,
  })
}
