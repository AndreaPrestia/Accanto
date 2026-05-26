import { test, expect } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

// Spec base: il modulo AI è opt-in. Per default lo stack non ha Ollama attivo,
// quindi le sezioni AI mostrano "non attivo su questo server" e il toggle non compare.
// Lo spec esteso (toggle + chiamata reale) gira solo con env E2E_AI=1 e profilo
// `ai` di docker-compose già attivo + modello scaricato.

test('AI section è visibile e segnala correttamente lo stato OFF di default', async ({ page }) => {
  const user = newUser('ai-off');
  await registerViaUi(page, user);

  // Crea cerchio
  await page.getByRole('link', { name: /crea il primo cerchio|\+ nuovo cerchio/i }).first().click();
  await expect(page).toHaveURL(/\/care-circles\/new/);
  const inputs = page.locator('input.input');
  await inputs.first().fill('Nonna AI');
  await page.locator('textarea.input').fill('Cerchio per E2E AI.');
  await page.getByRole('button', { name: /crea cerchio/i }).click();
  await expect(page).toHaveURL(/\/care-circles\/[0-9a-f-]+$/);

  // La card "Assistente" è presente
  await expect(page.getByRole('heading', { name: /assistente/i })).toBeVisible();

  // Senza Ai__Provider configurato, deve apparire il messaggio "non sono attive"
  await expect(page.getByText(/non sono attive su questo server/i)).toBeVisible();

  // Il toggle "Abilita assistente AI" non deve essere visibile
  await expect(page.getByText(/abilita assistente ai per questo cerchio/i)).toHaveCount(0);
});

const aiEnabled = process.env.E2E_AI === '1';

test.describe('AI completa (richiede profilo ai e modello scaricato)', () => {
  test.skip(!aiEnabled, 'E2E_AI=1 non impostato; spec saltata.');
  // Timeout esteso: la generazione può richiedere diversi secondi su CPU.
  test.setTimeout(120_000);

  test('owner abilita AI sul cerchio e genera un riassunto del diario', async ({ page }) => {
    const user = newUser('ai-on');
    await registerViaUi(page, user);

    // Crea cerchio
    await page.getByRole('link', { name: /crea il primo cerchio|\+ nuovo cerchio/i }).first().click();
    const inputs = page.locator('input.input');
    await inputs.first().fill('Mamma AI');
    await page.locator('textarea.input').fill('E2E con Ollama attivo.');
    await page.getByRole('button', { name: /crea cerchio/i }).click();
    await expect(page).toHaveURL(/\/care-circles\/[0-9a-f-]+$/);

    // Aggiungi una voce di diario così c'è qualcosa da riassumere
    await page.getByRole('link', { name: /^Diario$/ }).click();
    await page.getByRole('button', { name: /\+ nuova voce/i }).click();
    const form = page.locator('form.card');
    await form.locator('input:not([type="datetime-local"]):not([type="date"])').first().fill('Visita controllo');
    await form.locator('textarea').fill('Pressione ok. Riconsegnare ricetta tra 2 settimane.');
    await form.getByRole('button', { name: /salva voce/i }).click();

    // Torna al cerchio e abilita AI
    await page.getByRole('link', { name: /← cerchio/i }).click();
    const toggle = page.getByLabel(/abilita assistente ai per questo cerchio/i);
    await expect(toggle).toBeVisible();
    await toggle.check();

    // Vai al diario e genera
    await page.getByRole('link', { name: /^Diario$/ }).click();
    await page.getByRole('button', { name: /^riassumi$/i }).click();

    // Attesa risposta + disclaimer
    await expect(page.getByText(/testo generato da ai/i)).toBeVisible({ timeout: 90_000 });
    await expect(page.getByRole('button', { name: /^copia$/i })).toBeVisible();
  });
});
