# AskRabbi public website

A standalone explanatory site built with the same React 19, TypeScript 6, Vite 8, Tailwind CSS 4, and Lucide stack as `Frontend`. It introduces the project's purpose, learning experience, sources, and approach to responsible answers.

## Run locally

Use Node.js 22.12+ (24 LTS recommended) and pnpm 11:

```powershell
cd WebsiteFrontend
pnpm install --frozen-lockfile
pnpm dev
```

Open **http://127.0.0.1:5174**. This port is separate from the main application on 5173. The website needs no API, authentication, database, or secrets.

```powershell
pnpm verify
pnpm preview
```

`verify` runs Oxlint, TypeScript checking, and the production build. `preview` serves the result at **http://127.0.0.1:4174**.

## Static publishing

`pnpm build` renders the React components to HTML at build time using [Vite's SSR build](https://vite.dev/guide/ssr.html), then builds the page stylesheet with [Tailwind's Vite integration](https://tailwindcss.com/docs/installation/using-vite). Deploy only `dist/` to any static host. Production contains a complete HTML document, CSS, and local images, with **no client JavaScript or runtime server**. Navigation, links, and responsive layouts work without JavaScript. The development server uses React for hot reload.

The build-only renderer stays under ignored `node_modules/.website-prerender/`. No production hosting or existing deployment settings are changed by this project.

## Links and content

Calls to action open `https://app.askarabbi.ai`. To point at a different main application, copy `.env.example` to `.env.local` and change `VITE_APP_URL` to an absolute HTTP(S) URL, then restart or rebuild. For the local main application, use `http://localhost:5173`. Legal links point to the published policies at `https://askarabbi.ai`; Contact opens an email draft to its documented support address. Never put secrets in `VITE_*` values.

Copy follows the repository's project and frontend documentation. The example question illustrates the learning process; it is not a fabricated answer or live chat. No API calls, analytics, cookies, external font requests, or account state are needed by this website.

## Design and ownership

- `src/components/` contains the page's individual sections, brand, and shared call-to-action link.
- `src/index.css` mirrors the main frontend's light palette and display/body font stacks. The website keeps normal document-scale typography for reading.
- `public/library-manuscript.webp` and `public/favicon.svg` are copies of the existing frontend artwork. Keep their originals in `Frontend` and these copies in sync if the brand changes.
- `src/siteLinks.ts` owns application and footer destinations.

The small brand component and tokens are local to this independent build. A shared package is unnecessary for this page and would add coupling to the authenticated app.
