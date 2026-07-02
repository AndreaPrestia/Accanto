import { test, expect } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

test('la sessione corrente è visibile nello storico sessioni di /account', async ({ page }) => {
  const user = newUser('sessions');
  await registerViaUi(page, user);

  await page.goto('/account');
  await expect(page.getByRole('heading', { name: /dispositivi collegati/i })).toBeVisible();

  // The session list should contain at least one row with the "questa sessione" badge.
  await expect(page.getByText(/questa sessione/i)).toBeVisible();
});
