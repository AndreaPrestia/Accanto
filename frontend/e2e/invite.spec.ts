import { test, expect, BrowserContext } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

async function createCircle(context: BrowserContext, ownerName: string) {
  const page = await context.newPage();
  const owner = newUser('inv-owner');
  owner.displayName = ownerName;
  await registerViaUi(page, owner);

  await page.getByRole('link', { name: /crea il primo cerchio|\+ nuovo cerchio/i }).first().click();
  await page.locator('input.input').first().fill('Famiglia E2E');
  await page.getByRole('button', { name: /crea cerchio/i }).click();
  await expect(page).toHaveURL(/\/care-circles\/[0-9a-f-]+$/);

  return { page, owner };
}

test('owner crea un link di invito e un altro utente lo accetta', async ({ browser }) => {
  // --- Contesto 1: l'owner ---
  const ownerCtx = await browser.newContext();
  const { page: ownerPage } = await createCircle(ownerCtx, 'Anna Owner');

  // Genera un link di invito con default (Caregiver, 7 giorni, 1 uso)
  await ownerPage.getByRole('button', { name: /crea link di invito/i }).click();

  // Estrai l'url dal box mono che lo mostra
  const linkBox = ownerPage.locator('div.font-mono', { hasText: '/invite/' }).first();
  await expect(linkBox).toBeVisible();
  const inviteUrl = (await linkBox.textContent())?.trim();
  expect(inviteUrl).toBeTruthy();
  const inviteToken = inviteUrl!.split('/invite/')[1];

  // --- Contesto 2: l'invitato (nuovo browser context, no cookie/storage) ---
  const inviteeCtx = await browser.newContext();
  const inviteePage = await inviteeCtx.newPage();
  const invitee = newUser('inv-guest');

  // Visita il link: vede la preview anonima
  await inviteePage.goto(`/invite/${inviteToken}`);
  await expect(inviteePage.getByRole('heading', { name: /sei stato invitata|invitato/i })).toBeVisible();
  await expect(inviteePage.getByText(/famiglia e2e/i)).toBeVisible();
  await expect(inviteePage.getByText(/anna owner/i)).toBeVisible();

  // Si registra dal link (returnTo riporta su /invite/:token)
  await inviteePage.getByRole('link', { name: /non ho un accesso|registrami/i }).click();
  await expect(inviteePage).toHaveURL(/\/register\?returnTo=/);
  const inputs = inviteePage.locator('input.input');
  await inputs.nth(0).fill(invitee.displayName);
  await inputs.nth(1).fill(invitee.email);
  await inputs.nth(2).fill(invitee.password);
  await inviteePage.getByRole('button', { name: 'Crea il mio spazio' }).click();

  // Torna sulla pagina di accettazione, ora con sessione
  await expect(inviteePage).toHaveURL(new RegExp(`/invite/${inviteToken}$`));
  await inviteePage.getByRole('button', { name: /entra nel cerchio/i }).click();

  // Atterra sul cerchio
  await expect(inviteePage).toHaveURL(/\/care-circles\/[0-9a-f-]+$/);
  await expect(inviteePage.getByRole('heading', { name: 'Famiglia E2E' })).toBeVisible();

  await ownerCtx.close();
  await inviteeCtx.close();
});
