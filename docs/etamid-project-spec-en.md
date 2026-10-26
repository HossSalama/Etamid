# Etamid (اعتمد): Complete Project Specification

**Stack:** .NET backend · Angular (website + admin dashboard) · Mobile app (separate team) consuming the REST API
**Document date:** Tuesday, 20 October 2026
**Source:** Review of ~50 Zeplin screens + the OpenAPI contract (`etamid-openapi.yaml`, 78 operations)

Legend: 🔓 public (no token) · 🔒 requires `Authorization: Bearer <accessToken>`
All paths are prefixed with `/api/v1`. Every response uses the envelope `{ success, message, data, errors[], meta }`.

---

## Table of contents
1. Product overview and roles
2. Repository structure
3. Conventions shared with the mobile team
4. Features and endpoints (28 features)
5. State machines
6. Data model
7. Background jobs
8. Admin dashboard (Angular)
9. External integrations
10. Non-functional requirements
11. Timeline and API delivery dates
12. Mobile handoff checklist (what you deliver)
13. Open questions and design gaps
14. Risks
15. Appendix: all endpoints in one table

---

## 1) Product overview and roles

Etamid is an **escrow platform** between a buyer and a seller. The buyer's money is held by the platform and is released to the seller only after the buyer confirms receipt. If something goes wrong, the buyer opens a **dispute** that the platform's admins resolve.

There are two account types:
- **Individual:** buy/sell deals (for example an iPhone), with optional shipping, ratings, and disputes.
- **Company:** large deals with a **payment schedule**. Each installment needs the other party's approval, and a **penalty clause** applies automatically if the company is late.

Currency: SAR. Languages: Arabic (RTL) and English. Identity verification: **Nafath**. Payment: Apple Pay (shown in design), likely also mada/cards. Shipping: **Aramex**.

| Role | Description |
|---|---|
| Guest | Taps "Skip"; sees the home screen with an inactive wallet |
| Individual (buyer) | Creates deals, approves edits, pays, confirms receipt, opens disputes, rates |
| Individual (seller) | Receives requests; accepts, rejects, or proposes edits; ships; gets paid |
| Company | Creates multi-installment deals, tracks and approves payments |
| Admin | Angular dashboard: users, deals, disputes, finance, content, settings |

An individual account can act as both buyer and seller (home-seller and home-2 screens).

---

## 2) Repository structure

Recommendation: **one monorepo for backend + website + admin dashboard**, and a **separate repo for the mobile app**.

```
etamid/
├── backend/                       # .NET
│   ├── src/
│   │   ├── Etamid.Domain/
│   │   ├── Etamid.Application/
│   │   ├── Etamid.Infrastructure/
│   │   └── Etamid.Api/            # /api/v1 (mobile + website) and /admin/api (dashboard)
│   └── tests/
├── web/                           # Angular workspace
│   ├── projects/
│   │   ├── website/               # public website (SSR for SEO)
│   │   └── admin/                 # admin dashboard
│   └── libs/
│       ├── api-client/            # generated from openapi.yaml
│       ├── shared-ui/             # RTL, colors, fonts, components
│       └── shared-utils/
├── contracts/
│   └── openapi.yaml               # single source of truth for the API
├── docs/
├── docker-compose.yml             # SQL + Redis + API locally
└── .github/workflows/             # backend.yml · web-website.yml · web-admin.yml
```

Rules:
- Change `contracts/openapi.yaml` first, then generate the TypeScript client (NSwag or openapi-generator), so contract breaks fail at build time.
- Keep the admin API (`/admin/api`) out of the mobile contract; it uses different auth and permissions.
- Use path-filtered CI so a change in `web/projects/admin` builds only the dashboard.
- The mobile repo (`etamid-mobile`) depends only on the contract and the mock server.
- Split the contract into its own small repo only if backend and web teams become separate.

---

## 3) Conventions shared with the mobile team

- **Envelope:** `{ success, message, data, errors:[{code, field?, message}], meta:{page,pageSize,total,totalPages} }`.
- **Language:** `Accept-Language: ar|en`. `message` is localized; `errors[].code` is stable and used by the app logic.
- **Dates:** ISO 8601 in UTC. **Money:** number in SAR with two decimals. **Phone:** `05XXXXXXXX`.
- **Pagination:** `page` (default 1) and `pageSize` (default 20, max 100).
- **Tokens:** short-lived access token (about 1 hour) plus refresh token. On `401 TOKEN_EXPIRED` the app refreshes once and retries.
- **Idempotency:** `Idempotency-Key` (UUID) header is required on payment and withdrawal calls.
- **Server-driven UI:** deal responses include `availableActions[]` (buttons to show to the current user), `banner` (colored notice), and `badge` (time-left label and color). The mobile app does not compute permissions or time labels itself.
- **Versioning:** `/v1`. Never break existing endpoints; add `/v2` for breaking changes. Keep a changelog.
- **Standard errors:** `401 UNAUTHORIZED/TOKEN_EXPIRED`, `403`, `404 NOT_FOUND`, `409` (conflict, e.g. `INVALID_DEAL_STATE`, `PHONE_EXISTS`), `422` (validation), `429 TOO_MANY_REQUESTS` with `Retry-After`.

---

## 4) Features and endpoints

### Feature 1: App configuration, lookups, and terms
**What it does:** On launch (Splash), the app asks the API for supported languages, minimum app version (forced update), support WhatsApp number, and currency. Fixed lists (reject reasons, dispute reasons, shipping companies, banks...) come from the API so admins can change them without an app release. Terms and conditions come as versioned text so the system records exactly which version a user accepted.

| Auth | Method | Path | Purpose | Details |
|---|---|---|---|---|
| 🔓 | GET | `/config` | App settings | Returns `languages`, `minVersion{android,ios}`, `forceUpdate`, `supportWhatsapp`, `currency` |
| 🔓 | GET | `/lookups/{type}` | Fixed lists | `type`: `reject-reasons`, `dispute-reasons`, `shipping-companies`, `banks`, `duration-units`, `payment-methods`, `company-reject-reasons`. Item: `id, code, name, iconUrl` (localized) |
| 🔓 | GET | `/terms/{type}` | Terms and conditions | `type`: `platform`, `deal`, `company`. Returns `version, title, items[]` |

### Feature 2: Account registration (individual and company)
**What it does:** The user picks an individual or company account on Splash2. Registration creates an **unverified** account and sends an OTP by SMS. Phone and email are unique. Passwords are hashed. Accepting terms is required and the accepted version is stored. Company accounts may sit in "pending review" if the commercial registration is verified manually.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/register/individual` | Register an individual | `username, phone, email, password (min 8), acceptTerms=true` | `201` + `phone, resendAfterSeconds, expiresInSeconds`; `409 PHONE_EXISTS/EMAIL_EXISTS`; `422` |
| 🔓 | POST | `/auth/register/company` | Register a company | `companyName, ownerPhone, commercialRegNo, taxNo?, email, password, acceptTerms` | Same, plus `409 COMMERCIAL_REG_EXISTS` |

### Feature 3: Account verification (OTP)
**What it does:** The "verify account" screen has 6 digit boxes and a resend countdown (for example 00:56). The code is valid for a few minutes with a cap on wrong attempts. Resend has a cooldown (rate limit) to prevent abuse and SMS cost. On success the user is signed in and receives tokens.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/otp/verify` | Activate account | `phone, code` | `200` tokens + `user`; `400 OTP_INVALID/OTP_EXPIRED/OTP_ATTEMPTS_EXCEEDED` |
| 🔓 | POST | `/auth/otp/resend` | Resend code | `phone, purpose` (`register` or `password_reset`) | `200` + `resendAfterSeconds`; `429` with `Retry-After` |

### Feature 4: Login and session management
**What it does:** Login by phone and password. Returns an access token and a refresh token (rotated on each refresh). Logout revokes the refresh token and the device's push token.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/login` | Sign in | `phone, password` | `200` tokens + `user`; `401 INVALID_CREDENTIALS`; `403 ACCOUNT_NOT_VERIFIED` (send to OTP screen) or `ACCOUNT_SUSPENDED` |
| 🔓 | POST | `/auth/refresh` | Renew tokens | `refreshToken` | `200` new tokens; `401` if expired or reused |
| 🔒 | POST | `/auth/logout` | Sign out | `refreshToken?, deviceToken?` | `200` |

### Feature 5: Password recovery (3 steps)
**What it does:** Matches the forget-password screens: (1) enter phone, (2) enter code, (3) set a new password, then the success screen. Between steps 2 and 3 the API issues a short-lived, single-use `resetToken`, so step 2 cannot be skipped.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/password/forgot` | Step 1: send code | `phone` | `200` + resend timing; `404` if unknown (or always `200` to avoid revealing registered numbers; a security decision) |
| 🔓 | POST | `/auth/password/verify-otp` | Step 2: verify code | `phone, code` | `200` + `resetToken`; `400` |
| 🔓 | POST | `/auth/password/reset` | Step 3: new password | `resetToken, newPassword, confirmPassword` | `200`; `422` mismatch or weak password |

### Feature 6: Guest mode ("Skip")
**What it does:** "Skip" enters the app without an account and shows a "your wallet is not activated, sign in" card. There is no dedicated endpoint. Public endpoints work without a token (`/config`, `/lookups`, `/terms`, `/support/faqs`), and `/me/home` called without a token returns `accountType: "guest"` and `wallet.activated=false`. Any other action returns `401`, and the app routes the user to login.

### Feature 7: Home screen
**What it does:** One call fills the home screen by account type:
- **Individual:** wallet card (balance, activated or not), active deals with the **time badge** (yellow: 3 days left, red: 6 hours left), and the notification dot.
- **Company:** active deals count, total dues, upcoming payments with status (on time / near / upcoming).
- **Guest:** login card only.

| Auth | Method | Path | Purpose | Response |
|---|---|---|---|---|
| 🔓/🔒 | GET | `/me/home` | Home data | `accountType, unreadNotifications, wallet{activated,balance,heldAmount,currency}, activeDeals[], activeDealsCount, company{...}` |
| 🔒 | GET | `/company/dashboard` | Company dashboard (standalone) | `activeDealsCount, totalDues, upcomingPaymentsCount, upcomingPayments[]` |

### Feature 8: Notifications and device registration
**What it does:** The bell icon shows a list of notifications. Each has a `type` and a `dealId`/`paymentId`, so tapping opens the right screen. Types: `deal_request, deal_accepted, deal_rejected, deal_edit, deal_funded, deal_shipped, deal_delivered, dispute_update, payment_approval, payment_due, penalty, wallet, system`. For push notifications the app registers its FCM token.

| Auth | Method | Path | Purpose | Details |
|---|---|---|---|---|
| 🔒 | GET | `/notifications` | List | Paged. Item: `id, type, title, body, isRead, dealId, paymentId, createdAt` |
| 🔒 | GET | `/notifications/unread-count` | Unread count | `{count}` for the bell dot |
| 🔒 | PUT | `/notifications/{id}/read` | Mark one read | |
| 🔒 | PUT | `/notifications/read-all` | Mark all read | |
| 🔒 | POST | `/devices` | Register device | `deviceToken, platform (android/ios)` |
| 🔒 | DELETE | `/devices/{deviceToken}` | Unregister device | Called on logout |

### Feature 9: Profile ("My account")
**What it does:** Shows name, email, photo, the "verified via Nafath" badge, the bank account, and links to help and settings. The user can edit data, change password and language, and delete the account. Deletion is refused (`409`) if there are active deals or a balance.

| Auth | Method | Path | Purpose | Request / Response |
|---|---|---|---|---|
| 🔒 | GET | `/me` | My profile | `id, code (search code), type, name, phone, email, avatarUrl, language, phoneVerified, nafathVerified, walletActivated, bankAccount, company{commercialRegNo,taxNo}` |
| 🔒 | PUT | `/me` | Update profile | `username?, email?`; `422` duplicate email |
| 🔒 | PUT | `/me/password` | Change password | `currentPassword, newPassword` |
| 🔒 | PUT | `/me/language` | Change language | `language: ar|en` |
| 🔒 | POST | `/me/avatar` | Upload photo | `multipart/form-data`, field `file` |
| 🔒 | DELETE | `/me` | Delete account | `409` if active deals or balance |

### Feature 10: Identity verification via Nafath
**What it does:** The "identity verification" card (optional) starts Nafath. The API returns a **random number** (for example 42) that the user must select inside the Nafath app. The mobile app polls every 3 seconds until the status is `verified`, `rejected`, or `expired` (about 2 minutes). On success the "verified" badge appears and withdrawals become available. The real integration requires onboarding approval from Nafath.

| Auth | Method | Path | Purpose | Response |
|---|---|---|---|---|
| 🔒 | POST | `/me/kyc/nafath/start` | Start verification | `requestId, randomNumber, expiresInSeconds` |
| 🔒 | GET | `/me/kyc/nafath/status?requestId=` | Poll status | `status: pending | verified | rejected | expired` |

### Feature 11: Bank account
**What it does:** Used for withdrawals. The user enters the holder name and IBAN. The API validates the IBAN (length and checksum) and derives the bank name and logo from the bank code. Only a **masked IBAN** is ever returned (for example `SA•••••••••4582`).

| Auth | Method | Path | Purpose | Request / Response |
|---|---|---|---|---|
| 🔒 | GET | `/me/bank-account` | View | `holderName, ibanMasked, bankName, bankLogoUrl` (or `null`) |
| 🔒 | PUT | `/me/bank-account` | Add or update | `holderName, iban`; `422 INVALID_IBAN` |

### Feature 12: Seller lookup
**What it does:** In "new deal" there is a field for the seller's phone or code with a search icon. The result card shows name, verification, rating, and successful deal count so the buyer can confirm before continuing. No sensitive data (no email) is returned.

| Auth | Method | Path | Purpose | Response |
|---|---|---|---|---|
| 🔒 | GET | `/users/lookup?q=` | Find by phone or code | `id, name, phone, whatsappUrl, avatarUrl, verified, rating, successfulDeals`; `404` if not found |

### Feature 13: Create a deal (individual)
**What it does:** The buyer enters: deal title, seller, agreement details (up to 200 characters), amount, execution duration (number + hour/day), whether shipping is needed (then shipping company and address), and accepts the terms. The deal is created in `pending_seller` state, the seller is notified, and the "sent successfully" screen shows. **No money is taken yet**; funds are held after the seller accepts.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals` | Create deal | `title, sellerPhoneOrCode, description (≤200), amount, durationValue, durationUnit, needShipping, shippingCompanyId?, shippingAddressId?, acceptTerms` | `201` + full deal; `422` (e.g. shipping without a company, or seller equals buyer) |

### Feature 14: Accept or reject a deal
**What it does:** The seller sees requests in the "new requests" tab. **Accepting** shows the terms screen, and on confirmation the state becomes `accepted` and the buyer is asked to pay. **Rejecting** requires a reason from the list or "other" with text (mandatory for "other"). The buyer uses the same accept endpoint after reviewing a seller's edit. The API validates that the current state allows the action, otherwise `409 INVALID_DEAL_STATE`.

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/accept` | Accept | `acceptTerms=true` | `200` deal in new state; `409` |
| 🔒 | POST | `/deals/{id}/reject` | Reject | `reasonId, note? (≤200)` | `200`; `409` |

### Feature 15: Propose an edit and approve it
**What it does:** Instead of rejecting, the seller can propose a change to the **amount, duration, or details**. The state becomes `edit_proposed`, the buyer is notified, and sees a before/after comparison, then approves or rejects. An edit takes effect only after the other party approves. If rejected, the seller sees a red banner "the buyer rejected the edits".

| Auth | Method | Path | Purpose | Request |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/edits` | Propose an edit | `amount?, durationValue?, durationUnit?, description?` |
| 🔒 | POST | `/deals/{id}/edits/{editId}/approve` | Approve | none |
| 🔒 | POST | `/deals/{id}/edits/{editId}/reject` | Reject | none |

All three return the full deal with `pendingEdit{previous, proposed, proposedBy, status}`.

### Feature 16: Payment and escrow hold
**What it does:** After the seller accepts, the buyer pays. The amount is **held by the platform** and not released to the seller until receipt is confirmed. Methods: Apple Pay (shown in the design), mada, card, or wallet balance. A 3DS step may be required (`requires_action` with `redirectUrl`). The **`Idempotency-Key` header is required** (UUID generated by the app) so a retried request never charges twice. The final result is confirmed asynchronously by the gateway webhook (Feature 27).

| Auth | Method | Path | Purpose | Request | Responses |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/pay` | Hold the funds | Header `Idempotency-Key`; body `paymentMethod (apple_pay|mada|card|wallet), paymentToken?` | `200` + `status: succeeded|requires_action|failed, redirectUrl?, deal`; `402 PAYMENT_FAILED` |

After success: `fundsHeld=true`, the buyer sees "funds held until the deal ends", and the seller is notified to start.

### Feature 17: Shipping and tracking
**What it does:** For deals with shipping, the seller registers the shipment (creating an Aramex waybill via integration, or entering a tracking number manually). State becomes `shipped` and the buyer is notified. The buyer tracks the shipment in the app. A background job syncs Aramex status and moves the deal to "delivered" automatically.

| Auth | Method | Path | Purpose | Request / Response |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/ship` | Seller registers shipment | `trackingNumber?` (empty = auto-create Aramex shipment) |
| 🔒 | GET | `/deals/{id}/tracking` | Track shipment | `trackingNumber, status, events[{at, description}]` |

### Feature 18: Confirm receipt and "not received"
**What it does:** This is the core safety mechanism.
- **Confirm receipt:** the buyer confirms, funds move from hold to the seller's balance, the state becomes `completed`, and the success screen shows with a rating prompt.
- **Not received:** shows the "deal failure" screen (funds stay held, nothing is transferred) and opens the dispute path.

| Auth | Method | Path | Purpose | Response |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/confirm-delivery` | Confirm receipt, release funds | Deal `completed`; `409` if state does not allow |
| 🔒 | POST | `/deals/{id}/not-received` | Report non-receipt | Deal whose `availableActions` includes `open_dispute` |

### Feature 19: Ratings and the seller store page
**What it does:** After completion the buyer rates the store (1 to 5 stars + optional comment), one rating per deal (`409` if repeated). The store page shows overall rating (for example 4.9), successful deals (for example 345), and a list of user reviews with a verified-reviewer badge.

| Auth | Method | Path | Purpose | Request / Response |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/rating` | Add rating | `stars (1-5), comment? (≤300)` |
| 🔒 | GET | `/sellers/{id}/store` | Store page | `id, name, category, bannerUrl, rating, successfulDeals` |
| 🔒 | GET | `/sellers/{id}/reviews` | Store reviews | Paged: `reviewerName, reviewerAvatarUrl, verified, stars, comment, createdAt` |

### Feature 20: Deal lists, details, and status timeline
**What it does:** Buyers and sellers browse deals in tabs (new / in progress / completed). The details screen changes with state, so the API returns:
- `status`
- `banner`: colored notice at the top (blue: seller edited, green: buyer approved, red: edits rejected, yellow: funds held)
- **`availableActions`**: the buttons for the **current user** only (`accept, reject, edit, approve_edit, reject_edit, pay, ship, confirm_delivery, report_not_received, rate, open_dispute, contact_support, view_tracking`)
- `badge`: the time-left label and color

| Auth | Method | Path | Purpose | Details |
|---|---|---|---|---|
| 🔒 | GET | `/deals?role=&tab=&page=&pageSize=` | List | `role: buyer|seller` (required), `tab: new|in_progress|completed`. Items are `DealSummary` |
| 🔒 | GET | `/deals/{id}` | Details | Full `Deal`: counterparty, shipping, `pendingEdit`, rejection reason, `dispute`, `rated`; `404` if the user is not a party |
| 🔒 | GET | `/deals/{id}/timeline` | Status history | `[{status, label, at}]` |

### Feature 21: Wallet and withdrawals
**What it does:** Shows available balance, held amounts, and transaction history (Paid, Refund, release, withdrawal, penalty). The wallet is **inactive** for guests and for users who have not met requirements. **Withdrawal** transfers the balance to the user's bank account and requires a registered bank account, Nafath verification (per business decision), and sufficient balance. It also uses `Idempotency-Key`. Every movement is written to a double-entry ledger and is never edited.

| Auth | Method | Path | Purpose | Request / Responses |
|---|---|---|---|---|
| 🔒 | GET | `/wallet` | Balance | `activated, balance, heldAmount, currency` |
| 🔒 | GET | `/wallet/transactions` | History | `type (paid|refund|release|withdrawal|topup|penalty), amount (signed), dealNumber, createdAt` |
| 🔒 | POST | `/wallet/withdraw` | Request withdrawal | Header `Idempotency-Key`; body `amount`; `422 INSUFFICIENT_BALANCE / BANK_ACCOUNT_REQUIRED / KYC_REQUIRED` |
| 🔒 | GET | `/wallet/withdrawals` | Withdrawal requests | `id, amount, status (pending|processing|completed|rejected), createdAt` |

### Feature 22: File upload and disputes
**What it does:** On "not received" or any problem the buyer opens a **dispute**: picks one of 6 reasons, writes details (≤200 characters), attaches up to **5 files** (images or PDF), and confirms "I confirm the information is correct". Files are uploaded first and return a `fileId` that is passed when opening the dispute. Opening a dispute freezes the funds, assigns a number such as `D-45872` with status "under review", and notifies the seller. Admins review from the Angular dashboard and decide: release to the seller or refund the buyer. They can request extra documents from both parties via messages.

| Auth | Method | Path | Purpose | Request / Responses |
|---|---|---|---|---|
| 🔒 | POST | `/uploads` | Upload a file | `multipart` field `file` (image/PDF, max 10MB); `201` + `fileId, url`; `422` bad type or size |
| 🔒 | POST | `/disputes` | Open a dispute | `dealId, reasonId, details (≤200), fileIds[≤5], confirmed=true`; `201` + `Dispute{number, status...}`; `409` already open or state not allowed |
| 🔒 | GET | `/disputes` | My disputes | Paged list |
| 🔒 | GET | `/disputes/{id}` | Dispute details | `number, reason, details, status (under_review|awaiting_info|resolved_buyer|resolved_seller|closed), files, resolution` |
| 🔒 | POST | `/disputes/{id}/messages` | Add message or documents | `message?, fileIds[]?` |

### Feature 23: Support and help
**What it does:** The help section in "My account": FAQs (public, even for guests), "contact us" (creates a ticket, optionally linked to a deal), and "support requests" to follow tickets and their status. A "contact support" button appears on completed deals. A WhatsApp button on a deal opens a chat with the other party using `whatsappUrl` from the counterparty data.

| Auth | Method | Path | Purpose | Details |
|---|---|---|---|---|
| 🔓 | GET | `/support/faqs` | FAQs | `id, question, answer` |
| 🔒 | POST | `/support/contact` | Create a ticket | `subject, message, dealId?` |
| 🔒 | GET | `/support/tickets` | My tickets | `id, number (S-1021), subject, status (open|in_review|closed), createdAt` |

*Proposed, not in the contract yet:* `GET /support/tickets/{id}` (ticket details and replies).

### Feature 24: Create a company deal (5-step wizard)
**What it does:** The company's "open new deal" flow has five steps. Each step is saved in a **draft** so the user can go back or resume later, then review and submit:
1. **Deal details:** company (counterparty), deal name, company phone, details, total value, start date, contract attachment.
2. **Payment schedule:** add or remove installments (amount + date). **Rule:** installments must sum to the total value and every date must be after the start date.
3. **Payment approval settings:** how many days before the due date the approval request is sent, the response deadline, notes.
4. **Penalty clause:** percentage of the total value and clauses. Applied automatically if the company is late on any installment by more than **30 days** after the due date.
5. **Review and submit:** show everything, then submit. The other party is notified and the success screen appears.

| Auth | Method | Path | Purpose | Request / Responses |
|---|---|---|---|---|
| 🔒 | POST | `/company/deals/drafts` | Step 1: create draft | `companyName, title, companyPhone, details (≤200), totalAmount, startDate, contractFileId?`; `201` + `draftId` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}` | Edit step 1 | Same fields as step 1 |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/schedule` | Step 2: schedule | `payments[{amount, dueDate}]`; `422 PAYMENTS_TOTAL_MISMATCH` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/approval-settings` | Step 3: approval settings | `sendRequestDaysBefore, responseDeadlineDays, notes?` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/penalty` | Step 4: penalty clause | `percentage, graceDays (30), clauses?` |
| 🔒 | GET | `/company/deals/drafts/{draftId}/review` | Step 5: review | Full `CompanyDeal` with installments and penalty |
| 🔒 | POST | `/company/deals/drafts/{draftId}/submit` | Create and send | `201` + `CompanyDeal`; `422` if a step is missing |

### Feature 25: Manage company deals
**What it does:** Deals list in tabs (new / active / completed). Deal details show value, start date, the **installment schedule and statuses** (paid / awaiting approval / upcoming), the penalty clause, and a "deal contract" button. The other party (the one the deal was created for) **accepts or rejects** the deal, stating a reason when rejecting. The contract downloads through a short-lived link to protect the file.

| Auth | Method | Path | Purpose | Request / Response |
|---|---|---|---|---|
| 🔒 | GET | `/company/deals?tab=` | List | `tab: new|active|completed`. Item: `id, number, title, counterpartyName, totalAmount, startDate, status` |
| 🔒 | GET | `/company/deals/{id}` | Details | `CompanyDeal` with `payments[], approvalSettings, penalty, hasContract` |
| 🔒 | POST | `/company/deals/{id}/accept` | Accept (other party) | none |
| 🔒 | POST | `/company/deals/{id}/reject` | Reject (other party) | `reasonId, note?` |
| 🔒 | GET | `/company/deals/{id}/contract` | Contract link | `url, expiresAt` |

### Feature 26: Installments and approvals
**What it does:** Each installment has a lifecycle: `scheduled` → (before due date by the configured days) `awaiting_approval` → `approved` or `rejected` → `paid` or `overdue` → `penalty_applied`. Before the due date the other party receives an approval request, reviews the details and attachments, confirms that the deal's milestones are met, then **approves** or **rejects with a reason**, within the response deadline. The home screen shows "upcoming payments" with a badge: on time / near / upcoming / overdue. Delays beyond 30 days trigger the penalty automatically via a background job.

| Auth | Method | Path | Purpose | Request / Responses |
|---|---|---|---|---|
| 🔒 | GET | `/company/payments/upcoming` | Upcoming payments (view all) | `paymentId, dealId, counterpartyName, amount, dueDate, badge (on_time|near|upcoming|overdue)` |
| 🔒 | GET | `/company/payments/{id}` | Installment details (approval screen) | `order, label, amount, dueDate, status, dealNumber, dealTitle, notes, availableActions[approve|reject]` |
| 🔒 | POST | `/company/payments/{id}/approve` | Approve | `200`; `409` if state does not allow |
| 🔒 | POST | `/company/payments/{id}/reject` | Reject | `reason (≤200)` |

### Feature 27: Payment gateway webhook (internal)
**What it does:** The gateway reports payment results asynchronously (success, failure, refund). **The mobile app never uses it.** It is secured by verifying the gateway signature and ignoring duplicate events. It updates the hold, the deal, and the wallet, and sends a push notification.

| Auth | Method | Path | Purpose |
|---|---|---|---|
| 🔓 (signed) | POST | `/payments/webhook` | Receive gateway events |

*Proposed, not in the contract yet:* `POST /payments/intents` (create a payment intent before paying from the mobile app), only if your gateway requires it.

### Feature 28: Features without mobile endpoints
**Background jobs (Hangfire):** see section 7.
**Admin dashboard and `/admin/api`:** see section 8.

---

## 5) State machines

**Individual deal**
```
Draft → PendingSeller → (Rejected | EditProposed ⇄ PendingSeller/PendingBuyerApproval) → Accepted
→ Funded (held) → Shipped → Delivered → Completed (funds released)
                          ↘ Disputed → (ReleasedToSeller | RefundedToBuyer)
Expired / Cancelled possible at early stages
```
API values: `draft, pending_seller, edit_proposed, pending_buyer_approval, accepted, funded, shipped, delivered, completed, rejected, disputed, refunded, expired, cancelled`.

**Company installment**
```
Scheduled → ApprovalRequested → (Approved | Rejected) → Paid | Overdue → PenaltyApplied
```
API values: `scheduled, awaiting_approval, approved, rejected, paid, overdue, penalty_applied`.

Time badges are computed from the execution duration and acceptance time (yellow at 3 days left, red at 6 hours left).

---

## 6) Data model (main entities)

Users, CompanyProfiles, Otps, RefreshTokens, Devices (FCM), NafathVerifications, BankAccounts, Banks,
Deals, DealEditRequests, DealStatusHistory, DealAttachments, RejectReasons, TermsVersions + TermsAcceptances,
Shipments, ShippingCompanies,
Wallets, WalletTransactions (double-entry ledger), EscrowHolds, PaymentIntents, WithdrawalRequests,
Ratings, Disputes, DisputeReasons, DisputeMessages, DisputeFiles, SupportTickets, Faqs,
CompanyDeals, PaymentSchedules (installments), PaymentApprovals, PenaltyClauses, PenaltyCharges, Contracts,
Notifications, AuditLogs, AdminUsers/Roles/Permissions, AppSettings.

Rules: soft delete; audit log on every financial movement; money as `decimal(18,2)`; financial records are never edited, only reversed by a new entry; IBAN encrypted at rest.

---

## 7) Background jobs

- Expire deals the seller did not answer in time.
- Send reminders before deadlines (push + SMS) and refresh time badges.
- Send installment approval requests before the due date and enforce the response deadline.
- **Penalty engine:** after 30 days of delay, compute and record the penalty automatically.
- Sync Aramex shipment status and mark deals "delivered".
- Auto-release funds a set time after delivery if the buyer does not object (only if approved as a business rule).
- Clean up expired OTPs and tokens.
- Daily reconciliation with the payment gateway.

---

## 8) Admin dashboard (Angular) and `/admin/api`

The current design is mobile only, so the dashboard needs either a design or a ready component library (PrimeNG or Angular Material) with RTL support. It is not part of the mobile contract; write a separate OpenAPI for it.

| Page | Functions |
|---|---|
| Login and permissions | Roles and permissions (RBAC), 2FA for admins |
| KPIs dashboard | Users, deals, held value, open disputes |
| Users | Search, activate/suspend, Nafath status, review company accounts and commercial registration |
| Deals | Lists, filters, details, status history, manual intervention |
| Disputes | List, details, attachments, messages, decision (release to seller or refund buyer) |
| Finance | Holds, withdrawals (approve/reject), ledger, reports and Excel export |
| Company deals | Installments, penalties, delays |
| Content | FAQs, terms (versioned), reject and dispute reasons |
| Notifications | Bulk send, templates |
| Support | Ticket inbox and replies |
| Settings | Commissions, shipping companies, banks, wait periods |
| Audit log | Who did what and when |

---

## 9) External integrations

| Integration | Use | Note |
|---|---|---|
| SMS provider | OTP and alerts | Local Saudi provider; Sender ID registration required |
| FCM / APNs | Push notifications | |
| **Nafath** | Identity verification | Requires onboarding approval; apply immediately |
| Payment gateway | Apple Pay / mada / cards | Needs a merchant account. **Holding customer funds needs a licence or a licensed partner (SAMA)** |
| Aramex | Create shipments and tracking | Wrap in an adapter so the provider can be swapped |
| IBAN and commercial registration checks | Validation | Optional |
| File storage | S3 or Azure Blob | Short-lived signed links for contracts |

---

## 10) Non-functional requirements

- **Security:** short-lived JWT + refresh rotation; Argon2/BCrypt; rate limits on OTP and login; idempotency on money endpoints; encrypted IBAN; OWASP; Saudi PDPL compliance; signed webhooks.
- **Financial integrity:** database transactions, optimistic concurrency to prevent double spending, double-entry ledger.
- **Performance:** Redis cache, indexes, pagination.
- **Observability:** Serilog + Seq/ELK, health checks, alerts.
- **Suggested tech:** latest .NET LTS, Clean Architecture, EF Core, SQL Server or PostgreSQL, MediatR, FluentValidation, Swagger/OpenAPI, Hangfire, Redis, Docker, CI/CD. Angular (latest) with RTL and NgRx or Signals.
- **Testing:** unit and integration tests (about 70% overall, 90% or more on financial logic), security test and penetration test before launch.

---

## 11) Timeline and API delivery dates

Working week: Sunday to Thursday. Start: **Sunday 18 October 2026**.
Assumed team: 2 backend, 2 Angular, 1 QA, part-time DevOps, separate mobile team. If one person does it all, expect roughly 6 to 8 months.

| Phase | Duration | Dates | Deliverables |
|---|---|---|---|
| 0. Foundation | 1 week | 18 – 22 Oct | Architecture, DB, OpenAPI contract, CI/CD, Dev/Staging, start Nafath / gateway / Aramex onboarding |
| 1. Accounts and login | 2 weeks | 25 Oct – 5 Nov | Registration, OTP, login, password recovery, guest mode, languages |
| 2. Profile and notifications | 2 weeks | 8 – 19 Nov | Profile, bank/IBAN, Nafath, notifications (push/SMS), support and FAQs |
| 3. Individual deals | 4 weeks | 15 Nov – 10 Dec | Create, accept/reject/edit, terms, state machine, home, time badges |
| 4. Wallet and payment | 4 weeks | 29 Nov – 24 Dec | Gateway, hold/release/refund, ledger, withdrawals |
| 5. Shipping and ratings | 3 weeks | 13 – 31 Dec | Aramex, tracking, receipt confirmation, ratings, store page |
| 6. Disputes and support | 3 weeks | 20 Dec – 7 Jan | File upload, disputes, tickets, admin decisions |
| 7. Company deals | 5 weeks | 13 Dec – 14 Jan | 5-step wizard, installments, approvals, penalty, contracts |
| 8. Angular dashboard | parallel | 8 Nov – 14 Jan | Delivered incrementally with each module |
| 9. Testing and hardening | 3 weeks | 10 – 28 Jan | Integration, security, load testing, bug fixing |
| 10. UAT and launch | 2 weeks | 31 Jan – 11 Feb 2027 | Acceptance testing, production release, post-launch support |

**Expected launch: mid-February 2027** (about 17 weeks). For an earlier release, ship an individuals-only MVP in late January and companies in a second release.

**API delivery dates for the mobile team**

| Delivery | Date |
|---|---|
| OpenAPI contract + mock server (all modules) | 22 Oct 2026 |
| Accounts and login (Staging) | 5 Nov 2026 |
| Profile and notifications | 19 Nov 2026 |
| Individual deals | 10 Dec 2026 |
| Wallet and payment | 24 Dec 2026 |
| Shipping, ratings, disputes | 7 Jan 2027 |
| Company module | 14 Jan 2027 |

---

## 12) Mobile handoff checklist (what you deliver)

**Before any backend code (week 1)**
- The OpenAPI contract (`etamid-openapi.yaml`), reviewed and agreed with the mobile team.
- A **mock server** built from the contract (Postman Mock Server, or Prism: `prism mock openapi.yaml`).
- Agreed conventions (section 3): envelope, stable error codes, pagination, date and money formats, language header.

**Environments and tools**
- A stable Staging environment with a Swagger URL.
- A Postman collection updated with each delivery.
- Test accounts: individual buyer, individual seller, company; a fixed OTP on Staging (for example `123456`).
- Seed data covering deals in every state (new, edit proposed, funded, shipped, disputed, completed...).

**Agreements to settle with the mobile team**
- `availableActions`, `banner`, `badge` are server-driven.
- Token lifetime, auto-refresh, behavior on `401`.
- Notification payload shape and routing (`type` + `dealId`).
- Who talks to the payment gateway (mobile directly or via the backend) and how success is detected (polling or push).
- Upload limits and allowed types; minimum app version from `/config`; which endpoints are public for guests.
- Real-time updates: push only, or SignalR for live deal status.
- Nafath: SDK directly or through the backend.

**Ongoing**
- Weekly 15-minute sync; a changelog file; never break existing endpoints; respond to issues within a day.

---

## 13) Open questions and design gaps

**Questions that affect the design**
1. Is Angular only for the admin dashboard, or also a web portal for companies?
2. In a company deal, who pays and who receives? (Screens mention "total dues" and a penalty when the company is late.)
3. Is the escrow hold taken at **seller acceptance** or at **buyer submission**? (Screens suggest acceptance; the contract assumes acceptance.)
4. What is the commission, who pays it, and when are funds auto-released if the buyer never confirms?
5. The payment-method screen shows only Apple Pay. Are mada, cards, and bank transfer needed?
6. Are company payments processed through the platform, or only recorded externally?
7. Is Nafath mandatory before withdrawals or large deals?
8. Who is the licensed payment provider? (Biggest risk: holding third-party funds needs a licence.)
9. Minimum and maximum deal and withdrawal amounts.
10. Are ratings shown only after completion, and can sellers reply?

**Contract assumptions to confirm:** payment methods `apple_pay | mada | card | wallet`; `/deals/{id}/accept` serves both seller and buyer (after an edit); the hold happens in `/deals/{id}/pay`; the other party in company deals is named `counterparty`.

**Missing from the design:** notifications, FAQs, contact us, support requests, security and privacy, shipment tracking, withdrawal, bank edit, profile edit screens; the other party's side of company deals; empty and error states; some screens are cut off at the bottom buttons.

---

## 14) Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Licence to hold funds | Blocks launch | Contract a licensed escrow provider from the start |
| Nafath approval delay | Delays profile verification | Apply in week 1; make it optional in release 1 |
| Changing financial rules | Rework | Lock the state machine and ledger early and review with finance |
| Missing screens | Mobile blocked | Send the designer a list of gaps now |
| Aramex dependency | Shipping delays | Adapter layer so the provider can be replaced |

---

## 15) Appendix: all endpoints in one table

| # | Method | Path | Auth | Feature |
|---|---|---|---|---|
| 1 | GET | /config | 🔓 | 1 |
| 2 | GET | /lookups/{type} | 🔓 | 1 |
| 3 | GET | /terms/{type} | 🔓 | 1 |
| 4 | POST | /auth/register/individual | 🔓 | 2 |
| 5 | POST | /auth/register/company | 🔓 | 2 |
| 6 | POST | /auth/otp/verify | 🔓 | 3 |
| 7 | POST | /auth/otp/resend | 🔓 | 3 |
| 8 | POST | /auth/login | 🔓 | 4 |
| 9 | POST | /auth/refresh | 🔓 | 4 |
| 10 | POST | /auth/logout | 🔒 | 4 |
| 11 | POST | /auth/password/forgot | 🔓 | 5 |
| 12 | POST | /auth/password/verify-otp | 🔓 | 5 |
| 13 | POST | /auth/password/reset | 🔓 | 5 |
| 14 | GET | /me/home | 🔓/🔒 | 7 |
| 15 | GET | /company/dashboard | 🔒 | 7 |
| 16 | GET | /notifications | 🔒 | 8 |
| 17 | GET | /notifications/unread-count | 🔒 | 8 |
| 18 | PUT | /notifications/{id}/read | 🔒 | 8 |
| 19 | PUT | /notifications/read-all | 🔒 | 8 |
| 20 | POST | /devices | 🔒 | 8 |
| 21 | DELETE | /devices/{deviceToken} | 🔒 | 8 |
| 22 | GET | /me | 🔒 | 9 |
| 23 | PUT | /me | 🔒 | 9 |
| 24 | DELETE | /me | 🔒 | 9 |
| 25 | PUT | /me/password | 🔒 | 9 |
| 26 | PUT | /me/language | 🔒 | 9 |
| 27 | POST | /me/avatar | 🔒 | 9 |
| 28 | POST | /me/kyc/nafath/start | 🔒 | 10 |
| 29 | GET | /me/kyc/nafath/status | 🔒 | 10 |
| 30 | GET | /me/bank-account | 🔒 | 11 |
| 31 | PUT | /me/bank-account | 🔒 | 11 |
| 32 | GET | /users/lookup | 🔒 | 12 |
| 33 | POST | /deals | 🔒 | 13 |
| 34 | GET | /deals | 🔒 | 20 |
| 35 | GET | /deals/{id} | 🔒 | 20 |
| 36 | POST | /deals/{id}/accept | 🔒 | 14 |
| 37 | POST | /deals/{id}/reject | 🔒 | 14 |
| 38 | POST | /deals/{id}/edits | 🔒 | 15 |
| 39 | POST | /deals/{id}/edits/{editId}/approve | 🔒 | 15 |
| 40 | POST | /deals/{id}/edits/{editId}/reject | 🔒 | 15 |
| 41 | POST | /deals/{id}/pay | 🔒 | 16 |
| 42 | POST | /deals/{id}/ship | 🔒 | 17 |
| 43 | GET | /deals/{id}/tracking | 🔒 | 17 |
| 44 | POST | /deals/{id}/confirm-delivery | 🔒 | 18 |
| 45 | POST | /deals/{id}/not-received | 🔒 | 18 |
| 46 | POST | /deals/{id}/rating | 🔒 | 19 |
| 47 | GET | /deals/{id}/timeline | 🔒 | 20 |
| 48 | GET | /sellers/{id}/store | 🔒 | 19 |
| 49 | GET | /sellers/{id}/reviews | 🔒 | 19 |
| 50 | GET | /wallet | 🔒 | 21 |
| 51 | GET | /wallet/transactions | 🔒 | 21 |
| 52 | POST | /wallet/withdraw | 🔒 | 21 |
| 53 | GET | /wallet/withdrawals | 🔒 | 21 |
| 54 | POST | /uploads | 🔒 | 22 |
| 55 | POST | /disputes | 🔒 | 22 |
| 56 | GET | /disputes | 🔒 | 22 |
| 57 | GET | /disputes/{id} | 🔒 | 22 |
| 58 | POST | /disputes/{id}/messages | 🔒 | 22 |
| 59 | GET | /support/faqs | 🔓 | 23 |
| 60 | POST | /support/contact | 🔒 | 23 |
| 61 | GET | /support/tickets | 🔒 | 23 |
| 62 | POST | /company/deals/drafts | 🔒 | 24 |
| 63 | PUT | /company/deals/drafts/{draftId} | 🔒 | 24 |
| 64 | PUT | /company/deals/drafts/{draftId}/schedule | 🔒 | 24 |
| 65 | PUT | /company/deals/drafts/{draftId}/approval-settings | 🔒 | 24 |
| 66 | PUT | /company/deals/drafts/{draftId}/penalty | 🔒 | 24 |
| 67 | GET | /company/deals/drafts/{draftId}/review | 🔒 | 24 |
| 68 | POST | /company/deals/drafts/{draftId}/submit | 🔒 | 24 |
| 69 | GET | /company/deals | 🔒 | 25 |
| 70 | GET | /company/deals/{id} | 🔒 | 25 |
| 71 | POST | /company/deals/{id}/accept | 🔒 | 25 |
| 72 | POST | /company/deals/{id}/reject | 🔒 | 25 |
| 73 | GET | /company/deals/{id}/contract | 🔒 | 25 |
| 74 | GET | /company/payments/upcoming | 🔒 | 26 |
| 75 | GET | /company/payments/{id} | 🔒 | 26 |
| 76 | POST | /company/payments/{id}/approve | 🔒 | 26 |
| 77 | POST | /company/payments/{id}/reject | 🔒 | 26 |
| 78 | POST | /payments/webhook | 🔓 (signed) | 27 |
