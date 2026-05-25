import { test, expect } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

test('crea un cerchio e aggiunge una voce di diario', async ({ page }) => {
  const user = newUser('circle');
  await registerViaUi(page, user);

  // Dashboard: vai al form "nuovo cerchio"
  await page.getByRole('link', { name: /crea il primo cerchio|\+ nuovo cerchio/i }).first().click();
  await expect(page).toHaveURL(/\/care-circles\/new/);

  // Compila e crea
  const inputs = page.locator('input.input');
  await inputs.first().fill('Mamma');
  await page.locator('textarea.input').fill('Cerchio di prova E2E.');
  await page.getByRole('button', { name: /crea cerchio/i }).click();

  // Atterra sulla pagina del cerchio
  await expect(page).toHaveURL(/\/care-circles\/[0-9a-f-]+$/);
  await expect(page.getByRole('heading', { name: 'Mamma' })).toBeVisible();

  // Vai al diario
  await page.getByRole('link', { name: /^Diario$/ }).click();
  await expect(page.getByRole('heading', { name: 'Diario' })).toBeVisible();

  // Apri il form nuova voce
  await page.getByRole('button', { name: /\+ nuova voce/i }).click();

  // I campi non hanno htmlFor; li seleziono per attributo.
  // Titolo: primo input testuale all'interno del form della card
  const form = page.locator('form.card');
  await form.locator('input:not([type="datetime-local"]):not([type="date"])').first().fill('Visita di controllo');
  await form.locator('textarea').fill('Tutto regolare, prossimo appuntamento fra 3 mesi.');
  await form.getByRole('button', { name: /salva voce/i }).click();

  // La voce appare nella lista
  await expect(page.getByText('Visita di controllo')).toBeVisible();
});
