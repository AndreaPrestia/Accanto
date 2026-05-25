import { test, expect } from '@playwright/test';
import { newUser, registerViaUi, loginViaUi } from './helpers';

test('dopo 5 tentativi falliti consecutivi il login viene temporaneamente bloccato', async ({ browser }) => {
  // Register a real user in one context.
  const owner = newUser('lockout');
  const ownerCtx = await browser.newContext();
  const ownerPage = await ownerCtx.newPage();
  await registerViaUi(ownerPage, owner);
  await ownerCtx.close();

  // From a fresh anonymous context, fail the login 5 times.
  const attackerCtx = await browser.newContext();
  const attackerPage = await attackerCtx.newPage();

  for (let i = 0; i < 4; i++) {
    await loginViaUi(attackerPage, owner.email, 'WrongPassword!!!');
    // Generic credenziali non valide message (not lockout yet).
    await expect(attackerPage).toHaveURL(/\/login/);
    // Wait for the error region to appear (any red text).
    await expect(attackerPage.locator('div.text-red-700').first()).toBeVisible();
  }

  // 5th wrong attempt should trigger the lockout message.
  await loginViaUi(attackerPage, owner.email, 'WrongPassword!!!');
  await expect(attackerPage.locator('div.text-red-700').first()).toContainText(/bloccat/i);

  // Even a correct password is refused while locked out.
  await loginViaUi(attackerPage, owner.email, owner.password);
  await expect(attackerPage).toHaveURL(/\/login/);
  await expect(attackerPage.locator('div.text-red-700').first()).toContainText(/bloccat/i);

  await attackerCtx.close();
});
