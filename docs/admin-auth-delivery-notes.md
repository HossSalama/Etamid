# Cycle 9 / Task 1 - Admin auth + RBAC: delivery notes

Base: main @ 44ac43d. Backend + docs only. Nothing committed, nothing pushed.

## NOT done in the build environment (do these before merging)
- No `dotnet` SDK and no SQL Server were available where this patch was written. **Nothing was compiled, no test was run,
  and the migration was NOT generated.** Build, fix any compile slip, and run the full suite locally.
- The migration must be generated and tested on an empty SQL Server (commands below).
- The patch hunks for existing files were written by hand against the file contents; run `git apply --check` first.

## What changed
New files
- Domain/Admin/AdminAuth.cs - role/permission constants (18 codes), AdminPasswordPolicy, AdminRefreshToken, AdminTwoFactorChallenge
- Application/Common/Interfaces/AdminInterfaces.cs - ICurrentAdmin, IAdminAccessTokenGenerator, ITotpService
- Application/AdminAuth/AdminErrors.cs, AdminDtos.cs, AdminServices.cs (audit, session, profile, access/RBAC)
- Application/AdminAuth/AdminAuthCommands.cs (login, 2fa verify, refresh, logout, logout-all, me, 2fa setup/enable/disable)
- Application/AdminAuth/AdminManagement.cs (admins list/get/create/patch/disable/enable/reset-2fa/roles)
- Application/AdminAuth/AdminRoles.cs (roles CRUD, role permissions, permissions list, audit logs)
- Infrastructure/Identity/AdminJwt.cs, AdminTotp.cs (Otp.NET); Services/CurrentAdmin.cs
- Infrastructure/Persistence/Configurations/AdminAuthConfigurations.cs (new tables + seed of 18 permissions, super-admin role, grants)
- Infrastructure/Persistence/AdminBootstrapSeeder.cs (Development-only first super-admin)
- Api/AdminApi/AdminApiController.cs, AdminControllers.cs (routes under /admin/api, not BaseApiController)
- tests/Etamid.Api.IntegrationTests/AdminAuthTests.cs
- docs/web-admin.yml, this file
Modified files
- IApplicationDbContext (admin DbSets), ApplicationDbContext (2 new DbSets), Domain/Admin/Entities.cs (AdminUser lockout methods)
- appsettings.json (AdminJwt, RateLimiting:AdminAuthPerMinute), Directory.Packages.props + Infrastructure csproj + test csproj (Otp.NET 1.4.0)
- Api ServiceCollectionExtensions (AdminJwt scheme, admin-auth rate limit), Infrastructure + Application DependencyInjection, Program.cs (seeder call)
- messages.en.json / messages.ar.json (38 keys each, inserted after SUCCESS)
- EtamidFactory (test env vars), docker-compose.yml + .env.example (ADMIN_JWT_SECRET, otherwise the API refuses to start)

## Apply and test
1. `git apply --check --ignore-whitespace --recount etamid-admin-auth.patch`, then the same without --check.
2. `cd backend` and `dotnet build Etamid.slnx -c Release -p:TreatWarningsAsErrors=true`.
3. Generate the migration (needs `AdminJwt__Secret` set, any 32+ chars):
   `dotnet ef migrations add AdminAuthRbac -p src/Etamid.Infrastructure -s src/Etamid.Api -o Migrations`
4. Empty SQL Server test (Docker): start `mcr.microsoft.com/mssql/server:2022-latest`, then
   `dotnet ef database update -p src/Etamid.Infrastructure -s src/Etamid.Api --connection "Server=localhost,1433;Database=EtamidAdminMigTest;User Id=sa;Password=<pw>;TrustServerCertificate=True"`
   Check: 18 rows in AdminPermissions, 1 in AdminRoles, 18 in AdminRolePermissions, new tables AdminRefreshTokens and AdminTwoFactorChallenges. Drop the test DB afterwards. Never run against production.
5. `dotnet test Etamid.slnx -c Release`.

## First super-admin (local Development only)
```
cd backend/src/Etamid.Api
dotnet user-secrets set "AdminJwt:Secret" "<32+ random chars, different from Jwt:Secret>"
dotnet user-secrets set "AdminSeed:Email" "you@example.com"
dotnet user-secrets set "AdminSeed:Password" "<12-64 chars, letter+digit+symbol, no spaces>"
dotnet run
```
The seeder runs only in Development, only when no admin exists, and writes nothing to tracked files. Docker: set ADMIN_JWT_SECRET in `.env`; the seeder is not wired into compose.

## Staging (never `git add .`)
`git add -u backend docs docker-compose.yml .env.example`, then one `git add <path>` for each new file above, plus the generated migration files, then `git status --short`.

## Owner questions / assumptions
1. Typo `admin.users.nfath-review` treated as `admin.users.nafath-review`; only that spelling is seeded. Please confirm.
2. Which endpoints are "super-admin only" was not specified. Chosen: every admin-account write and every role write (create/patch/disable/enable/reset-2fa/assign roles/role CRUD/role permissions), and each also needs `admin.roles.manage`. Reads of admins, roles and permissions need only `admin.roles.manage`. Audit logs need `admin.audit.view`.
3. `POST /roles/{id}/permissions` and `POST /admins/{id}/roles` REPLACE the whole set (idempotent). Say if you want add-only.
4. Disabled admin with the right password returns the same generic 401 as a wrong password. A locked account returns 429 ADMIN_ACCOUNT_LOCKED (same as the app login, which also reveals existence).
5. A wrong 2FA code counts toward the 5-attempt lockout; a challenge dies after 5 wrong codes.
6. Added table AdminTwoFactorChallenges (single-use 5-minute challenge needs server state) in addition to AdminRefreshTokens.
7. Access tokens are also rejected once their session family is revoked (logout, logout-all, reuse detection, disable): one extra DB read per request.
8. Super-admin role is protected; you cannot disable yourself or the last active super-admin, or remove that role from them.
9. No change-password or reset-password endpoint exists in this task, so a super-admin sets a new admin's initial password and nobody can change it afterwards. Needs a follow-up.
10. 2FA is optional (not forced on first login). No recovery codes, per the brief. TOTP replay inside the same 30 s window is not blocked.
11. Admin password policy is stricter than the app (12-64 chars, letter+digit+symbol).
12. Audit rows for unknown-email logins store the attempted email in NewValues; they use EntityId 0, actor type System.
13. The `admin-auth` rate limit (10/min/IP) is not covered by an automated test (the test host sets it to 10000).
14. `AdminJwt:Secret` is validated at startup and must differ from `Jwt:Secret`; an environment without it will not start.
