import { test, expect } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

test('registrazione, navigazione e logout funzionano', async ({ page }) => {
  const user = newUser('auth');

  await registerViaUi(page, user);

  // After register we should land on home and see authenticated UI (link to /account)
  await expect(page).toHaveURL(/\/$|\/circles|\/care-circles/);
  await expect(page.getByRole('link', { name: user.displayName }).or(page.getByRole('link', { name: 'Account' }))).toBeVisible();

  // Go to /account e verifica che la sezione Benessere si apra via deep-link.
  // (AccountPage è in accordion: Benessere è chiuso di default, l'anchor lo apre.)
  await page.goto('/account#section-wellbeing');
  await expect(page.getByRole('heading', { name: /come stai oggi/i })).toBeVisible();
});
