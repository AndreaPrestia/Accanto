# Accanto · sito vetrina

Sito statico Astro per presentare Accanto a chi non ha ancora un account.
Trilingua (it / en / es), zero JavaScript di default, output puramente statico.

## Comandi

```bash
npm install
npm run dev        # http://localhost:4321
npm run build      # genera ./dist
npm run preview    # serve ./dist
npm run check      # astro check (type-check + diagnostica)
```

## Variabili d'ambiente

Copia `.env.example` in `.env` (in locale) o impostale al momento della build:

| Variabile         | Default                          | Uso                                                          |
|-------------------|----------------------------------|--------------------------------------------------------------|
| `SITE_URL`        | `https://accanto.care`           | URL pubblico del sito vetrina, usato per canonical e sitemap |
| `PUBLIC_APP_URL`  | `https://app.accanto.care`       | URL della SPA, usato dalle CTA "Accedi" / "Registrati"       |

`PUBLIC_APP_URL` deve iniziare per `PUBLIC_` per essere esposto al codice client (`import.meta.env.PUBLIC_APP_URL`).

## Struttura

- `src/pages/{it,en,es}/...` — pagine localizzate (slug diversi per lingua)
- `src/layouts/BaseLayout.astro` — head SEO, hreflang, OG, JSON-LD
- `src/components/` — Header, Footer, LangSwitcher, Hero, FeatureCard, FaqItem
- `src/i18n/` — file JSON di traduzione + helper `useT`, `routeUrl`, `routeKeyFromPath`
- `src/styles/global.css` — Tailwind + componenti base (`btn-primary`, `btn-ghost`, `card`, `container-prose`, ...)
- `public/` — favicon, OG image, `robots.txt`

## Linee guida

- Tono empatico, concreto. Niente claim medici (Accanto non è un dispositivo medico).
- Italiano come lingua canonica: si scrive prima in italiano e poi si traduce.
- Nessun analytics di default. Se servirà, va aggiunto come opt-in nella head di `BaseLayout.astro`.
- Form contatti via `mailto:` (nessun backend lato vetrina).

## Deploy

Per ora il deploy non è automatizzato. `npm run build` produce una cartella `dist/` con HTML/CSS/SVG statici, distribuibile su qualunque hosting statico (IONOS, Netlify, Nginx, ...).

### Docker

In locale il sito vetrina è incluso nello stack principale:

```bash
docker compose up -d web
# → http://localhost:4321
```

In produzione (via `docker-compose.prod.yml`) il container `web` viene servito da Caddy sull'**apex** `https://${ACCANTO_DOMAIN}`, mentre la SPA vive su `https://${ACCANTO_APP_DOMAIN}` (default `app.${ACCANTO_DOMAIN}`). Il sito vetrina non chiama mai il backend: è completamente statico.
