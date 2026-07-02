import { test, expect } from '@playwright/test';
import { newUser, registerViaUi } from './helpers';

test('il caregiver salva un check-in di benessere e lo ritrova nello storico', async ({ page }) => {
  const user = newUser('wellbeing');
  await registerViaUi(page, user);

  // Deep-link apre l'accordion "Benessere" (default collapsed).
  await page.goto('/account#section-wellbeing');

  // Pick value 4 on each scale (Umore / Energia / Stress).
  // Each scale is a fieldset with a legend; scope the "4" button to that fieldset.
  for (const legend of [/^Umore$/i, /^Energia$/i, /^Stress$/i]) {
    const fieldset = page.locator('fieldset').filter({ has: page.getByText(legend) }).first();
    await fieldset.getByRole('button', { name: '4', exact: true }).click();
  }

  await page.getByPlaceholder(/come è andata oggi/i).fill('Giornata stanca ma serena.');
  await page.getByRole('button', { name: /salva check-in/i }).click();

  // Success message
  await expect(page.getByText(/check-in salvato/i)).toBeVisible();

  // Expand history and confirm the note we just saved is shown
  const details = page.locator('details').first();
  if (!(await details.evaluate((el: HTMLDetailsElement) => el.open))) {
    await details.locator('summary').click();
  }
  await expect(page.getByText('Giornata stanca ma serena.')).toBeVisible();
});
