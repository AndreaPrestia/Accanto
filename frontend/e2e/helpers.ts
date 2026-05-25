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
  await page.goto('/register');
  // The register form lacks htmlFor/id associations; select by input position/attributes.
  const inputs = page.locator('input.input');
  await inputs.nth(0).fill(user.displayName);
  await inputs.nth(1).fill(user.email);
  await inputs.nth(2).fill(user.password);
  await page.getByRole('button', { name: 'Crea il mio spazio' }).click();
  await expect(page).not.toHaveURL(/\/register/);
}
