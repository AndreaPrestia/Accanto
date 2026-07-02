import { test, expect } from '@playwright/test';
import { TOTP } from 'otpauth';
import { newUser, registerViaUi, loginViaUi } from './helpers';

/**
 * Builds a TOTP code from a base32 secret using the same defaults as the backend
 * (SHA1, 6 digits, 30s window).
 */
function totp(secret: string): string {
  return new TOTP({
    secret: secret.replace(/\s+/g, ''),
    algorithm: 'SHA1',
    digits: 6,
    period: 30
  }).generate();
}

test('un caregiver può attivare la 2FA e poi accedere usando il codice TOTP', async ({ browser }) => {
  const setupCtx = await browser.newContext();
  const setupPage = await setupCtx.newPage();

  const user = newUser('twofa');
  await registerViaUi(setupPage, user);

  await setupPage.goto('/account');
  await expect(setupPage.getByRole('heading', { name: /verifica in due passaggi/i })).toBeVisible();

  // Start setup
  await setupPage.getByRole('button', { name: /attiva 2fa/i }).click();

  // The secret is rendered inside a <code> element; read it back.
  const secretLocator = setupPage.locator('code').first();
  await expect(secretLocator).toBeVisible();
  const secret = (await secretLocator.textContent())?.trim() ?? '';
  expect(secret.length).toBeGreaterThan(8);

  // Generate the current TOTP code and confirm.
  // The setup form lacks htmlFor on its label, so target the one-time-code input directly.
  const code = totp(secret);
  await setupPage.locator('input[autocomplete="one-time-code"]').fill(code);
  await setupPage.getByRole('button', { name: /conferma e attiva/i }).click();

  // Recovery codes block appears + status flips to "enabled".
  await expect(setupPage.getByText(/salva i codici di recupero/i)).toBeVisible();
  await expect(setupPage.getByText(/verifica in due passaggi attiva/i)).toBeVisible();

  await setupCtx.close();

  // Now log in from a clean context to confirm the 2FA challenge.
  const loginCtx = await browser.newContext();
  const loginPage = await loginCtx.newPage();

  await loginViaUi(loginPage, user.email, user.password);

  // Should land on the second-factor screen, not on home.
  await expect(loginPage.getByRole('heading', { name: /verifica in due passaggi/i })).toBeVisible();

  const challengeCode = totp(secret);
  await loginPage.locator('input.input').first().fill(challengeCode);
  await loginPage.getByRole('button', { name: /verifica|conferma|entra/i }).click();

  // Successful sign-in leaves /login.
  await expect(loginPage).not.toHaveURL(/\/login/);

  await loginCtx.close();
});
