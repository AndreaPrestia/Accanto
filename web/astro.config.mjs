import { defineConfig } from 'astro/config';
import tailwind from '@astrojs/tailwind';
import mdx from '@astrojs/mdx';
import sitemap from '@astrojs/sitemap';

// URL pubblico del sito vetrina. Sovrascrivibile a build time con SITE_URL.
const SITE_URL = process.env.SITE_URL || 'https://accanto.example';

// URL dell'applicazione SPA (usato per le CTA "Accedi" / "Registrati").
// Esposto a runtime via import.meta.env.PUBLIC_APP_URL.
process.env.PUBLIC_APP_URL = process.env.PUBLIC_APP_URL || 'https://app.accanto.example';

export default defineConfig({
  site: SITE_URL,
  trailingSlash: 'never',
  i18n: {
    defaultLocale: 'it',
    locales: ['it', 'en', 'es'],
    routing: {
      prefixDefaultLocale: true,
      // Il redirect / -> /it lo fa Caddy in edge (deploy/Caddyfile) con un vero
      // HTTP 302. Disabilitato qui per evitare la pagina meta-refresh che Astro
      // genera in modalità statica (e che richiederebbe un index.astro root).
      redirectToDefaultLocale: false
    }
  },
  integrations: [
    tailwind({ applyBaseStyles: false }),
    mdx(),
    sitemap({
      // Esclude la pagina root (redirect a /it) — il plugin sitemap i18n richiede
      // che ogni URL abbia un prefisso di locale, mentre "/" non lo ha.
      filter: (page) => !/^https?:\/\/[^/]+\/?$/.test(page),
      i18n: {
        defaultLocale: 'it',
        locales: {
          it: 'it-IT',
          en: 'en-US',
          es: 'es-ES'
        }
      }
    })
  ]
});
