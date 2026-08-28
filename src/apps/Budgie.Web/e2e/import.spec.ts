import { expect, test } from "@playwright/test"
import { seedLoggedIn, mockImportPreview, mockImportCommit } from "./fixtures"
import { mockApi } from "./mocks/api"

test.describe("money manager import", () => {
  test.beforeEach(async ({ page }) => {
    await seedLoggedIn(page)
  })

  test("sube archivo → preview → confirm → success", async ({ page }) => {
    const m = mockApi(page)
    m.handle("POST", "/api/import/money-manager/preview", mockImportPreview)
    m.handle("POST", "/api/import/money-manager/commit", mockImportCommit)

    await page.goto("/import")
    await expect(
      page.getByRole("heading", { name: /import.*money manager|importar desde money manager/i }).first(),
    ).toBeVisible()

    // Subir un .mmbak mediante el input de archivo (dropzone accesible).
    await page.locator('input[type="file"]').setInputFiles({
      name: "backup.mmbak",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("SQLite format 3\u0000"),
    })

    // Preview: resumen de cuentas + desglose de transacciones.
    await expect(page.getByText("Main Checking").first()).toBeVisible()
    await expect(page.getByText("229").first()).toBeVisible()

    // Confirmar el commit.
    await page.getByRole("button", { name: /confirm import|confirmar importación/i }).click()

    // Pantalla de éxito con resumen del commit.
    await expect(page.getByText(/import completed|importación completada/i).first()).toBeVisible()
    await expect(page.getByText("17").first()).toBeVisible()
  })

  test("muestra warning de archivo ya importado y permite overwrite", async ({ page }) => {
    const m = mockApi(page)
    m.handle("POST", "/api/import/money-manager/preview", {
      ...mockImportPreview,
      alreadyImported: true,
    })
    m.handle("POST", "/api/import/money-manager/commit", mockImportCommit)

    await page.goto("/import")
    await page.locator('input[type="file"]').setInputFiles({
      name: "backup.mmbak",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("SQLite format 3\u0000"),
    })

    await expect(page.getByText(/already imported|ya importado/i).first()).toBeVisible()

    // El checkbox de overwrite está presente y es marcable.
    const overwrite = page.getByRole("checkbox").first()
    await overwrite.check()
    await expect(overwrite).toBeChecked()
  })

  test("errores del preview bloquean el commit", async ({ page }) => {
    const m = mockApi(page)
    m.handle("POST", "/api/import/money-manager/preview", {
      ...mockImportPreview,
      errors: ["Invalid file"],
    })

    await page.goto("/import")
    await page.locator('input[type="file"]').setInputFiles({
      name: "backup.mmbak",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("SQLite format 3\u0000"),
    })

    await expect(page.getByText(/invalid file/i).first()).toBeVisible()
    await expect(page.getByRole("button", { name: /confirm import|confirmar importación/i })).toBeDisabled()
  })

  test("startOver: importar otro archivo resetea el flujo y permite re-subir", async ({ page }) => {
    const m = mockApi(page)
    let previewCalls = 0
    m.handle("POST", "/api/import/money-manager/preview", () => {
      previewCalls += 1
      return { body: mockImportPreview }
    })
    m.handle("POST", "/api/import/money-manager/commit", mockImportCommit)

    await page.goto("/import")

    // Primer import completo: upload → preview → confirm → success.
    await page.locator('input[type="file"]').setInputFiles({
      name: "backup.mmbak",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("SQLite format 3\u0000"),
    })
    await expect(page.getByText("Main Checking").first()).toBeVisible()
    await page.getByRole("button", { name: /confirm import|confirmar importación/i }).click()
    await expect(page.getByText(/import completed|importación completada/i).first()).toBeVisible()
    expect(previewCalls).toBe(1)

    // startOver vuelve a la pantalla de subida con el dropzone vacío.
    await page.getByRole("button", { name: /import another file|importar otro archivo/i }).click()
    await expect(page.getByRole("button", { name: /choose file|elegir archivo/i })).toBeVisible()

    // Re-subir otro archivo: el flujo preview → commit funciona de nuevo.
    await page.locator('input[type="file"]').setInputFiles({
      name: "backup2.mmbak",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("SQLite format 3\u0000"),
    })
    await expect(page.getByText("Main Checking").first()).toBeVisible()
    await page.getByRole("button", { name: /confirm import|confirmar importación/i }).click()
    await expect(page.getByText(/import completed|importación completada/i).first()).toBeVisible()
    expect(previewCalls).toBe(2)
  })
})
