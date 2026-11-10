# Cycle 9 / Task 2 (phase 2) - operator points moved to admin identity + audit
Includes the fix round (dead wallet/company operator code removed).

Base: branch cycle9_Admin (Task 1, fd96a0e). main is at least one commit ahead (d1fc8cd), so this branch needs a rebase before merging.

## Honesty statement
- The author had NO .NET SDK, NO SQL Server and NO checkout of the repo. Nothing in phase 2 or in the fix round was compiled
  or run by the author, and zero-warnings is NOT verified by the author. Build and test results come from the owner's machine only.
- Deliveries are scripts (`apply_phase2.py`, `apply_fix.py`) that edit the real working tree with exact anchors (they abort without
  writing anything if an anchor is missing); the patch is the owner's own `git diff`, so `git apply --check` needs no `--recount`.
- No migration: the *ByAdminId columns and AuditLogs already existed.

## Phase 2 - what changed
- AdminAccessService.RequireOperatorAsync (new): an authenticated caller that is not an admin gets 403.
- Withdrawals (list / approve / reject / complete), company penalties (list / waive), disputes (list / request-info / decide):
  gated by AdminJwt identity + database-checked permission; ApprovedByAdminId / WaivedByAdminId / ResolvedByAdminId filled;
  one AuditLog row per write with before/after values. Business logic, routes, shapes and docs/etamid-openapi.yaml unchanged.
- AdminController accepts the app scheme AND AdminJwt so an app-user token gets 403 on /api/v1/admin (Task 1's /admin/api routes answer 401).
- docs/web-admin.yml documents the 9 operator paths.
- Tests: WalletTests, CompanyDealTests, DisputeTests moved to admin tokens; new AdminTestKit.cs and AdminOperatorTests.cs
  (7 test methods: 3 success + audit, 3 refusal matrices, 1 reject).

## Fix round - what changed
- Removed the now-unused wallet and company operator policy classes (and their IsOperatorAsync methods) from Wallet.cs and CompanyMaintenance.cs.
- Removed WalletErrors.NotOperator and CompanyDealErrors.NotOperator and their message keys from messages.ar.json and messages.en.json.
- Removed the wallet and company operator keys from the settings files where they appeared (the script reports how many lines it removed;
  they may not have been present in the JSON settings at all - they were AppSettings database rows read at request time).
- Removed the dead test helpers MakeOperator (WalletTests) and MakeCompanyOperator (CompanyDealTests). MakeOperator in DisputeTests stays (still used).
- Comments that still named the removed classes were updated.
- Corrected the test count above (7, not 6).

## Deliberately NOT done
- The dispute operator setting, DisputeOperatorPolicy, DisputeErrors.NotOperator and DISPUTE_NOT_OPERATOR stay: the shared party
  endpoints (GET /api/v1/deals/{id}/dispute and POST .../dispute/messages) still use them. Deferred to phase 3 (dashboard design question).
- Accepted as-is: dual-scheme [Authorize] on AdminController; Complete fills ApprovedByAdminId only when empty; dispute messages use
  SenderAdminId; LedgerTransaction.CreatedById is the admin id; list reads are not audited; the /api/v1/admin vs /admin/api route question.
- No route/shape changes, no change to docs/etamid-openapi.yaml, no migration, no new message keys, no business-logic change.
- Rows that may still exist in a developer's local database for the removed settings keys are harmless (nothing reads them) and are not deleted.
- Unused private test helpers left behind (for example UserIdOf in some test classes) were not touched: the brief only names the two helpers.

## Apply and test
1. On branch cycle9_Admin, clean tree: `python apply_fix.py` from the repo root. It prints the grep-style check at the end.
2. `cd backend` then `dotnet build Etamid.slnx -c Release -p:TreatWarningsAsErrors=true`
3. `dotnet test Etamid.slnx -c Release`
4. `git diff` for the patch. Stage with `git add -u backend docs` (no new files except the replaced notes, which `-u` covers). No commit/push without the owner's approval.

## Open owner questions
1. Dispute operator setting and the shared party endpoints: how should phase 3 handle operators who still use the user-facing dispute routes?
2. Route question for phase 3: /admin/api only, or an exception for the 9 operator routes under /api/v1/admin.
3. docs/etamid-openapi.yaml is frozen; if it still mentions the removed error codes in its 403 descriptions, that is left for a contract update the owner approves.
