import { test, expect } from '@playwright/test';

test.describe('Pagine pubbliche di supporto', () => {
  test('/support mostra le risorse italiane e filtra per categoria', async ({ page }) => {
    await page.goto('/support');

    // Title
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    // At least one well-known resource (Telefono Amico) appears with default "tutti"
    await expect(page.getByText('Telefono Amico Italia', { exact: false })).toBeVisible();

    // Click "Emergenza" chip and verify 112 resource is shown; non-emergency items vanish
    await page.getByRole('button', { name: 'Emergenza', exact: true }).click();
    await expect(page.getByRole('heading', { name: /Numero unico emergenze/i })).toBeVisible();
    await expect(page.getByText('Telefono Amico Italia', { exact: false })).toHaveCount(0);

    // Back to all
    await page.getByRole('button', { name: 'Tutte', exact: true }).click();
    await expect(page.getByText('Telefono Amico Italia', { exact: false })).toBeVisible();
  });

  test('/self-care mostra il suggerimento del giorno e i segnali di burnout', async ({ page }) => {
    await page.goto('/self-care');

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    // "Un piccolo gesto per oggi" section heading or its content
    await expect(page.getByRole('heading', { name: /piccolo gesto|gesto per oggi/i })).toBeVisible();
    // Burnout section heading is "Segnali da non ignorare"
    await expect(page.getByRole('heading', { name: /segnali da non ignorare/i })).toBeVisible();
    // Link to /support
    await expect(page.getByRole('link', { name: /supporto|contatti/i })).toBeVisible();
  });
});
