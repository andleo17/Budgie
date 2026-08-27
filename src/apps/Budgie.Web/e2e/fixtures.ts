import type { Page } from "@playwright/test"
import { mockApi } from "./mocks/api"

export interface AuthFixture {
  token: string
  userId: string
}

export const authFixture: AuthFixture = {
  token: "e2e-test-token",
  userId: "11111111-1111-1111-1111-111111111111",
}

/** Seeds a logged-in session and the minimal API mocks shared by most specs. */
export async function seedLoggedIn(page: Page): Promise<void> {
  await page.addInitScript(({ token, userId }) => {
    localStorage.setItem("token", token)
    localStorage.setItem("userId", userId)
  }, authFixture)

  await mockApi(page)
    .handle("GET", "/api/auth/status", { initialized: true, allowRegistration: false })
    .handle("GET", "/api/accounts", {
      data: [{ id: authFixture.userId, name: "Pocket", balance: 1200.5, currency: "USD", type: 0 }],
    })
}
