# Etamid Admin Dashboard

Angular 22 standalone application for the Etamid platform back-office. Lives in `web/projects/admin` inside the shared Angular workspace.

## Running locally

```bash
# Start the .NET backend first (must listen on :5080)
cd backend && dotnet run

# In another terminal, start the admin dev server
cd web
npx ng serve --project admin
# → http://localhost:4200
```

The dev server proxies `/admin/api` and `/api/v1/admin` to `http://localhost:5080` via `proxy.conf.json`.

## Production build

```bash
cd web
npx ng build admin --configuration production
```

Outputs to `web/dist/admin/`.

## Architecture

| Layer | Description |
|-------|-------------|
| `src/app/app.ts` | Root component (router-outlet only) |
| `src/app/app.config.ts` | Provides router, `HttpClient`, admin interceptor |
| `src/app/app.routes.ts` | Lazy-loaded routes, all protected by `adminAuthGuard` except `/login` |
| `src/app/core/services/admin-auth.service.ts` | Signal-based auth, localStorage keys prefixed `etamid_admin_*` |
| `src/app/core/services/admin-language.service.ts` | Signal-based ar/en i18n, shares `etamid_lang` key with website |
| `src/app/core/services/admin-api.service.ts` | All backend calls |
| `src/app/core/interceptors/admin-auth.interceptor.ts` | Bearer token, Accept-Language, 401 refresh + dead-session redirect |
| `src/app/core/guards/admin-auth.guard.ts` | Validates token via `GET /admin/api/auth/me` |
| `src/app/features/shell/` | Sidebar + topbar layout |

## Pages and endpoints

| Page | Route | Endpoints used |
|------|-------|----------------|
| Login | `/login` | `POST /admin/api/auth/login` |
| 2FA Verify | `/login/2fa` | `POST /admin/api/auth/2fa/verify` |
| Dashboard | `/dashboard` | `GET /api/v1/admin/withdrawals`, `GET /api/v1/admin/disputes`, `GET /api/v1/admin/company-deals/penalties`, `GET /admin/api/audit-logs` |
| Withdrawals | `/withdrawals` | `GET /api/v1/admin/withdrawals`, approve/reject/complete |
| Penalties | `/penalties` | `GET /api/v1/admin/company-deals/penalties`, waive |
| Disputes | `/disputes` | `GET /api/v1/admin/disputes` |
| Dispute Detail | `/disputes/:id` | `GET /api/v1/admin/disputes` (filtered), `POST …/request-info`, `POST …/decide` |
| Admins | `/admins` | `GET /admin/api/admins`, `POST /admin/api/admins` |
| Admin Detail | `/admins/:id` | `GET /admin/api/admins/:id`, disable/enable/reset-2fa/roles |
| Roles | `/roles` | `GET /admin/api/roles`, `POST /admin/api/roles` |
| Role Detail | `/roles/:id` | `GET /admin/api/roles/:id`, patch, delete, permissions |
| Permissions | `/permissions` | `GET /admin/api/permissions` (read-only) |
| Audit Log | `/audit-log` | `GET /admin/api/audit-logs` |
| Profile / 2FA | `/profile` | `GET /admin/api/auth/me`, `POST /admin/api/auth/2fa/*` |

## Blocked pages (no matching endpoint in `docs/web-admin.yml`)

The following were NOT built because the API contract does not expose a suitable endpoint:

| Planned Feature | Missing endpoint |
|----------------|-----------------|
| Single-dispute GET | No `GET /api/v1/admin/disputes/{id}` — detail page loads from list |
| Withdrawal detail | No `GET /api/v1/admin/withdrawals/{id}` |
| Penalty detail | No `GET /api/v1/admin/company-deals/penalties/{id}` |

## localStorage keys

| Key | Value |
|-----|-------|
| `etamid_admin_access_token` | JWT access token |
| `etamid_admin_refresh_token` | Refresh token |
| `etamid_admin_user` | Serialised `Admin` object |
| `etamid_lang` | `'ar'` or `'en'` (shared with website) |

## Permission codes

| Code | Grants |
|------|--------|
| `admin.withdrawals.view` | See withdrawals list |
| `admin.withdrawals.approve` | Approve / reject withdrawals |
| `admin.withdrawals.complete` | Mark withdrawal as completed |
| `admin.penalties.view` | See penalties list |
| `admin.penalties.waive` | Waive a penalty |
| `admin.disputes.view` | See disputes list |
| `admin.disputes.decide` | Request info / decide a dispute |
| `admin.roles.manage` | Manage admins, roles, permissions |
| `admin.audit.view` | View audit log |
