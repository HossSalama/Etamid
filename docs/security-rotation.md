# Security rotation — cleanup notes

> Firebase code was intentionally left untouched in this cleanup (per owner instruction).

## 1. Gmail App Password — must be revoked by the account owner (Muse cannot do this)

The App Password that appeared in `appsettings.Development.json` in early
commits (and in a backup copy in the initial commit) is still in the public
git history even though the current files are empty. Deleting it from the
files does **not** revoke it.

Steps (Google account that owns the sender mailbox):
1. myaccount.google.com → Security → 2-Step Verification → App passwords.
2. Revoke the App Password used for Etamid.
3. Create a new one only if SMTP is still needed, and store it **only** via
   `dotnet user-secrets` or an environment variable — never in a tracked file.
   See `docs/email-otp.md`.

## 2. Security:DataKey — rotated out of tracked files

`Security:DataKey` (AES-GCM key for IBAN encryption and OTP-code hashing) is
now empty (`""`) in both `appsettings.json` and `appsettings.Development.json`.
The app validates at startup that it is a base64 string of exactly 32 bytes,
so a value **must** be supplied out-of-band:

```bash
# Generate a fresh 32-byte key (do not paste the output into chat, git, or docs)
openssl rand -base64 32

# Local development (from backend/)
dotnet user-secrets set "Security:DataKey" "<generated-key>" --project src/Etamid.Api/Etamid.Api.csproj
# ...or as an environment variable:
# Security__DataKey="<generated-key>"

# Docker Compose already uses this mechanism:
# .env → DATA_PROTECTION_KEY → Security__DataKey (see docker-compose.yml)
```

**Warning — existing encrypted data:** rotating the key invalidates data
encrypted/hashed with the old key (stored IBANs, OTP code hashes). For
development/staging databases with test data, reset or re-enter that data.
If any database holds real IBANs, do a dual-key re-encryption migration
(decrypt with the old key, re-encrypt with the new key) **before** discarding
the old key — do not rotate blindly in that case.

## 3. Files no longer tracked

`.gitignore` now covers `appsettings.Local.json` (note the capital `L` — the
old `appsettings.*.local.json` pattern did not match it) and
`backend/src/Etamid.Api/storage/`. Those files were removed from the git
index only; the local copies on disk are untouched.

## 4. Migrations — one folder

All EF Core migrations now live in
`backend/src/Etamid.Infrastructure/Migrations/` (namespace
`Etamid.Infrastructure.Migrations`), including the former
`Persistence/Migrations/` files (`Cycle3Deals`, `EmailOtpVerification`).
Migration IDs (`<timestamp>_<Name>`) are unchanged, so databases that already
applied them are unaffected — only the source folder/namespace moved.
