import { expect, test } from "@playwright/test"
import { seedLoggedIn } from "./fixtures"

// Smoke E2E: the app boots with a mocked API without hitting the real backend.
// Extend with real user flows (login, create account/transaction, split,
// budget, export, i18n) following the patterns here.

test("boots to the app shell with a mocked API", async ({ page }) => {
  await seedLoggedIn(page)

  const errors: string[] = []
  page.on("pageerror", (err) => errors.push(err.message))

  await page.goto("/")

  await expect(page.locator("#root")).toBeAttached()
  await expect(page.locator("#root")).toBeVisible()

  expect(errors, "page errors").toEqual([])
})
