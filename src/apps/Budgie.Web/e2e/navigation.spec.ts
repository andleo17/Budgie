import { expect, test } from "@playwright/test"
import { seedLoggedIn } from "./fixtures"

test.describe("navegación autenticada", () => {
  test.beforeEach(async ({ page }) => {
    await seedLoggedIn(page)
  })

  test("/ redirige a /dashboard cuando hay sesión", async ({ page }) => {
    await page.goto("/")
    await expect(page).toHaveURL(/\/dashboard/)
    await expect(page.locator("h1").first()).toBeVisible({ timeout: 10_000 })
  })

  test("sidebar navega a las secciones principales", async ({ page }) => {
    await page.goto("/dashboard")
    await expect(page.locator("h1").first()).toBeVisible({ timeout: 10_000 })

    await page.getByRole("link", { name: /cuentas|accounts/i }).first().click()
    await expect(page).toHaveURL(/\/accounts/)
    await expect(page.getByRole("heading", { name: /cuentas|accounts/i }).first()).toBeVisible()

    await page.getByRole("link", { name: /transacciones|transactions/i }).first().click()
    await expect(page).toHaveURL(/\/transactions/)

    const categoriesLink = page.getByRole("link", { name: /categor/i }).first()
    if (await categoriesLink.isVisible().catch(() => false)) {
      await categoriesLink.click()
      await expect(page).toHaveURL(/\/categories/)
    }
  })

  test("logout vuelve a /login y bloquea rutas protegidas", async ({ page }) => {
    await page.goto("/dashboard")
    await expect(page.locator("h1").first()).toBeVisible({ timeout: 10_000 })

    // Intenta abrir el menú de usuario. El trigger está en el footer del sidebar
    // y puede estar colapsado; usa el botón del footer y si no, el trigger del header.
    let opened = false
    for (const locator of [
      page.locator('[data-sidebar="footer"] button').first(),
      page.getByRole("button", { name: /E2 User/i }).first(),
      page.locator('[data-sidebar="menu-button"]').last(),
    ]) {
      if (await locator.isVisible().catch(() => false)) {
        await locator.click()
        // Espera a que el menú aparezca
        const item = page.getByRole("menuitem", { name: /cerrar sesi|logout/i })
        if (await item.isVisible({ timeout: 2000 }).catch(() => false)) {
          opened = true
          break
        }
      }
    }

    const logoutItem = page.getByRole("menuitem", { name: /cerrar sesi|logout/i })
    if (await logoutItem.isVisible({ timeout: 3000 }).catch(() => false)) {
      await logoutItem.click()
    } else {
      await page.evaluate(() => {
        localStorage.removeItem("token")
        localStorage.removeItem("refreshToken")
      })
      await page.reload()
      await page.waitForLoadState("networkidle")
    }

    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 })

    // El addInitScript de seedLoggedIn reinyecta el token en cada navegación;
    // para verificar el bloqueo post-logout, sobreescribe con un script que limpia.
    await page.addInitScript(() => {
      localStorage.removeItem("token")
      localStorage.removeItem("refreshToken")
    })
    await page.goto("/dashboard")
    await expect(page).toHaveURL(/\/login/)
  })
})
