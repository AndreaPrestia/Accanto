import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { newUser, registerViaUi } from './helpers';

/**
 * Smoke a11y audit on the most-used pages.
 * We check for "serious" and "critical" WCAG 2.1 A/AA violations only — the
 * project doesn't aim for full AAA conformance yet, so noisy minor findings are
 * filtered out to keep the gate practical.
 */
async function runAudit(page: import('@playwright/test').Page, label: string) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const blockers = results.violations.filter(
    (v) => v.impact === 'critical' || v.impact === 'serious'
  );

  if (blockers.length > 0) {
    const summary = blockers
      .map(
        (v) =>
          `- [${v.impact}] ${v.id}: ${v.help}\n    ${v.nodes
            .slice(0, 3)
            .map((n) => n.target.join(' '))
            .join('\n    ')}`
      )
      .join('\n');
    throw new Error(`a11y blockers on ${label}:\n${summary}`);
  }
  expect(blockers).toEqual([]);
}

test.describe('Audit accessibilità WCAG 2.1 AA', () => {
  test('homepage e pagine pubbliche non hanno violazioni serious/critical', async ({ page }) => {
    await page.goto('/');
    await runAudit(page, '/');

    await page.goto('/support');
    await expect(page.getByRole('heading', { name: /trov[ai] supporto/i })).toBeVisible();
    await runAudit(page, '/support');

    await page.goto('/self-care');
    await expect(page.getByRole('heading', { name: /cura di te/i })).toBeVisible();
    await runAudit(page, '/self-care');
  });

  test('login e register non hanno violazioni serious/critical', async ({ page }) => {
    await page.goto('/login');
    await runAudit(page, '/login');

    await page.goto('/register');
    await runAudit(page, '/register');
  });

  test('/account autenticato non ha violazioni serious/critical', async ({ page }) => {
    const user = newUser('a11y');
    await registerViaUi(page, user);
    // Apre la sezione Benessere (accordion default collapsed) via deep-link
    // così l'audit copre anche i contenuti dentro l'accordion, non solo l'header.
    await page.goto('/account#section-wellbeing');
    await expect(page.getByRole('heading', { name: /come stai oggi/i })).toBeVisible();
    await runAudit(page, '/account');
  });
});
