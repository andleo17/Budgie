import { expect, test } from "@playwright/test"
import { seedLoggedIn } from "./fixtures"
import { mockApi } from "./mocks/api"

test.describe("accounts", () => {
  test.beforeEach(async ({ page }) => {
    await seedLoggedIn(page)
  })

  test("lista las cuentas y permite filtrar por búsqueda", async ({ page }) => {
    await page.goto("/accounts")

    await expect(page.getByRole("heading", { name: /cuentas|accounts/i }).first()).toBeVisible()

    await expect(page.getByText("Main Checking").first()).toBeVisible()
    await expect(page.getByText("Savings").first()).toBeVisible()

    const search = page.getByPlaceholder(/search|buscar/i)
    await search.fill("Savings")
    await expect(page.getByText("Main Checking")).toBeHidden()
    await expect(page.getByText("Savings").first()).toBeVisible()

    await search.fill("")
    await expect(page.getByText("Main Checking").first()).toBeVisible()
  })

  test("estado vacío muestra mensaje sin cuentas", async ({ page }) => {
    await mockApi(page).handle("GET", "/api/finances/accounts", [])

    await page.goto("/accounts")
    await expect(page.getByText(/no accounts|sin cuentas|no se encontraron/i).first()).toBeVisible()
  })

  test("el diálogo de crear cuenta se abre", async ({ page }) => {
    await page.goto("/accounts")

    await page.getByRole("button", { name: /agregar cuenta|add account/i }).click()

    const dialog = page.getByRole("dialog")
    await expect(dialog).toBeVisible({ timeout: 10_000 })
    // Dentro del diálogo debe haber un formulario (al menos un input)
    await expect(dialog.locator("input").first()).toBeVisible()
  })

  test("responsive: en viewport móvil, la grilla sigue siendo accesible", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 })
    await page.goto("/accounts")

    await expect(page.getByRole("heading", { name: /cuentas|accounts/i }).first()).toBeVisible()
    await expect(page.getByText("Main Checking").first()).toBeVisible()
    await expect(page.getByRole("button", { name: /agregar cuenta|add account/i }).first()).toBeVisible()
  })
})
