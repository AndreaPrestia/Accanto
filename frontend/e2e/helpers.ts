import { expect, Page } from '@playwright/test';

/**
 * Generates a unique email per test run so each test creates a fresh user
 * without colliding with previously-registered ones in the dev DB.
 */
export function uniqueEmail(prefix = 'e2e') {
  const stamp = Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
  return `${prefix}+${stamp}@accanto.test`;
}

export interface TestUser {
  email: string;
  displayName: string;
  password: string;
}

export function newUser(prefix = 'e2e'): TestUser {
  return {
    email: uniqueEmail(prefix),
    displayName: 'Test E2E',
    password: 'TestPassword123!'
  };
}

export async function registerViaUi(page: Page, user: TestUser) {
  // Marca il welcome come visto PRIMA di caricare la pagina, così dopo la
  // registrazione atterriamo direttamente sulla dashboard (che è quello che
  // i test si aspettano). Se qualche test in futuro vuole coprire il flow
  // di welcome, chiami direttamente /welcome dopo register.
  await page.addInitScript(() => {
    try {
      window.localStorage.setItem('accanto.hasSeenWelcome', '1');
    } catch {
      /* localStorage bloccato: pazienza, il test vedrà il welcome */
    }
  });
  await page.goto('/register');
  // The register form lacks htmlFor/id associations; select by input position/attributes.
  const inputs = page.locator('input.input');
  await inputs.nth(0).fill(user.displayName);
  await inputs.nth(1).fill(user.email);
  await inputs.nth(2).fill(user.password);
  await page.getByRole('button', { name: 'Crea il mio spazio' }).click();
  await expect(page).not.toHaveURL(/\/register/);
}

export async function loginViaUi(page: Page, email: string, password: string) {
  await page.goto('/login');
  const inputs = page.locator('input.input');
  await inputs.nth(0).fill(email);
  await inputs.nth(1).fill(password);
  await page.getByRole('button', { name: /accedi|entra/i }).click();
}
