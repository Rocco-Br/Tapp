# Tapp

Ordering app for parties and restaurants.

## Stack

Sized for home use: max ~50 guests per event. Keep it small, add nothing without a concrete need.

| Part | Choice | Why |
|---|---|---|
| Backend | ASP.NET Core minimal API (net10) | Already scaffolded |
| Database | SQLite via EF Core | One file, no server, enough for 50 guests. Postgres is a later EF migration |
| Live updates | SignalR | Built into ASP.NET Core |
| Auth and roles | ASP.NET Core Identity, cookies | Admin, employee, guest. Admin creates users, no sign-up flow |
| Rate limiting | Built-in ASP.NET Core rate limiter | Order spam prevention |
| 18+ and stock rules | Enforced server-side only | Never trust the UI |
| Frontend | SvelteKit (adapter-static, SPA mode, no SSR), TypeScript | Small bundles, routing included |
| Frontend extras | Tailwind, `@microsoft/signalr`, `@vite-pwa/sveltekit` | One job each |
| Hosting | Single Docker container, the API serves the built frontend | Same origin: no CORS, simple cookies. Product pictures on a volume |
| Tests | xUnit (API) | Stock and 18+ logic |

Left out on purpose: microservices, separate auth server, Redis, message queue, i18n library (add when English arrives), state library, separate frontend hosting.

See [docs/FEATURES.md](docs/FEATURES.md) for scope.

## First admin

On startup, if `Admin__Password` is set and no admin exists, an admin is created (`Admin__Username`, default `admin`). Other users are created by an admin via `POST /api/users`.
