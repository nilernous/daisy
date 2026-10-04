# Daisy API Design

## 1. Overview

This document defines the HTTP API exposed by `Daisy.Api` and consumed by `Daisy.Mvc` (see [ARCHITECTURE.md](ARCHITECTURE.md)).

Sources:

- Architecture and technology stack: [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Use cases: [`.claude/use-cases/`](../../.claude/use-cases/README.md) (`UC-XX`)
- Business rules: [`.claude/business-rules/`](../../.claude/business-rules/README.md) (`BR-XX`)
- Sequence diagrams: [`.claude/sequences/`](../../.claude/sequences/README.md)
- Database schema: [`infra/database/DaisyDB.sql`](../../infra/database/DaisyDB.sql)

The API has three surfaces:

| Surface | Prefix | Purpose |
|---|---|---|
| REST | `/api` | Storefront and customer operations, and every operation that runs a business process |
| REST (admin) | `/api/admin` | Staff and Admin operations |
| OData | `/odata` | Read-only, flexible querying of lists (filter, sort, page) |

Rule from ARCHITECTURE §23: anything that **changes state or runs a business process is REST**; OData is used **only for `GET` list queries**.

---

## 2. General Conventions

### 2.1 Transport and format

| Item | Convention |
|---|---|
| Protocol | HTTPS only |
| Media type | `application/json; charset=utf-8` (file uploads use `multipart/form-data`) |
| JSON naming | `camelCase` properties |
| Enums | Serialized as strings (`"Pending"`, not `1`) using `JsonStringEnumConverter` |
| Identifiers | Integers (`id`), matching the `INT IDENTITY` keys in the database |
| Money | JSON number in VND, e.g. `259000`; stored as `DECIMAL(18,2)` |
| Timestamps | ISO 8601 with offset, e.g. `2026-10-01T14:30:00+07:00` |
| Dates | `yyyy-MM-dd`, e.g. `2026-10-01` (date of birth, dashboard ranges) |
| Business time zone | Vietnam time (UTC+07:00) for "today", sale periods, voucher periods and dashboard grouping |
| Null fields | Included as `null` (not omitted) so the client sees the full shape |
| Versioning | No version segment for the current scope; a breaking change later goes under `/api/v2` |
| API docs | Swagger UI at `/swagger` (Development environment only) |

### 2.2 HTTP methods

| Method | Use |
|---|---|
| `GET` | Read; never changes state |
| `POST` | Create a resource, or run an action (`/cancel`, `/login`, `/checkout`) |
| `PUT` | Replace the editable fields of a resource |
| `PATCH` | Change one aspect of a resource (`/visibility`, `/status`, `/role`) |
| `DELETE` | Remove a resource that is allowed to be removed (cart line, address, wishlist item, image, sale price) |

Catalog items, vouchers and users are **never deleted** through the API (BR-11); they are hidden, deactivated or locked.

### 2.3 Status codes

| Code | Meaning in Daisy |
|---|---|
| `200 OK` | Success with a body |
| `201 Created` | Resource created; `Location` header points to it when it has its own `GET` |
| `204 No Content` | Success without a body |
| `400 Bad Request` | Invalid input (validation) or a request that can never succeed |
| `401 Unauthorized` | Missing / invalid / expired access token, wrong credentials, or invalid webhook key |
| `403 Forbidden` | Authenticated but the role is not allowed, or the account is locked (login) |
| `404 Not Found` | Resource does not exist, is hidden from the storefront, **or belongs to another user** (BR-08) |
| `409 Conflict` | Valid input but the current state does not allow it (stock, voucher, status transition, duplicates) |
| `503 Service Unavailable` | A required external service failed (e.g. email) |
| `500 Internal Server Error` | Unexpected error; details are logged, never returned |

### 2.4 Pagination

**REST list endpoints** use page-based pagination:

```http
GET /api/orders?page=1&pageSize=20
```

| Parameter | Default | Limits |
|---|---|---|
| `page` | `1` | `>= 1` |
| `pageSize` | `20` | `1..100` |

Response envelope `PagedResult<T>`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3
}
```

**OData endpoints** use `$top`, `$skip` and `$count` (see §7).

### 2.5 Request headers

| Header | Sent by | Purpose |
|---|---|---|
| `Authorization: Bearer {accessToken}` | MVC, for signed-in users | Authentication (§3) |
| `X-Cart-Session: {sessionId}` | MVC, for guests | Identifies the guest cart (§3.4) |
| `X-Correlation-Id: {guid}` | MVC (optional) | Correlates MVC and API logs; echoed back in the response |
| Webhook key header | Bank webhook service | Authenticates `POST /api/payments/webhook` (§6.8) |

---

## 3. Authentication and Authorization

### 3.1 Tokens (BR-04)

| Token | Format | Lifetime (configurable) | Storage |
|---|---|---|---|
| Access token | JWT, signed HS256 | 15 minutes | Encrypted auth cookie in MVC; never in the browser's JavaScript |
| Refresh token | 64 random bytes, Base64Url | 7 days | MVC auth cookie; the API stores only its SHA-256 hash in `Users.RefreshToken` |

Access token claims:

| Claim | Value |
|---|---|
| `sub` | `UserId` |
| `email` | Email |
| `name` | Full name |
| `role` | `Customer` / `Staff` / `Admin` |
| `jti`, `iat`, `exp` | Standard |

- One refresh token per account: a new login or refresh replaces it; logging in on another device ends the previous session.
- Refresh rotates the token: the old refresh token stops working immediately.
- Logout, password reset, password change and account lock clear the refresh token.
- A role change takes effect when the next access token is issued (UC-66).

### 3.2 Token refresh flow (UC-04)

```text
MVC ── request with expired access token ──► API ── 401 ──► MVC
MVC ── POST /api/auth/refresh { refreshToken } ──► API ── 200 { new tokens } ──► MVC
MVC ── retry original request ──► API
```

If refresh returns `401`, MVC clears the auth cookie and redirects to the login page.

### 3.3 Authorization policies (BR-07)

| Policy | Roles | Used by |
|---|---|---|
| `Public` | Anyone, no token | Catalog, register, login, password reset |
| `CartOwner` | Guest with `X-Cart-Session`, or any signed-in user | Cart endpoints |
| `Authenticated` | Customer, Staff, Admin | Logout, change password |
| `Customer` | Customer | Profile, addresses, wishlist, checkout, my orders, reviews |
| `Staff` | Staff, Admin | Catalog management, inventory, order management, review moderation |
| `Admin` | Admin | Flash sale, vouchers, users, dashboard |
| `Webhook` | Bank webhook service (API key) | `POST /api/payments/webhook` |

Ownership (BR-08): for every `Customer` endpoint the owner is taken from the `sub` claim, never from the URL or body. Accessing another user's address, order or cart line returns `404`.

### 3.4 Guest cart session

- MVC creates a random session id (GUID) for each visitor without a login, stores it in a cookie and sends it as `X-Cart-Session`.
- On cart endpoints, a valid Bearer token takes precedence; `X-Cart-Session` is then ignored.
- An invalid or expired Bearer token on a cart endpoint returns `401 UNAUTHORIZED` (MVC then refreshes, §3.2); it never falls back to the guest cart.
- If neither is present, cart endpoints return `400` with code `CART_OWNER_REQUIRED` (`GET /api/cart` returns an empty cart instead).
- `POST /api/auth/login` with `X-Cart-Session` merges the guest cart into the user cart (UC-31, BR-22).

---

## 4. Error Handling

All errors are produced by `ExceptionMiddleware` (ARCHITECTURE §28). Controllers do not catch exceptions.

### 4.1 Error body

The body extends the format in ARCHITECTURE §28 (`status`, `message`) with a machine-readable `code`:

```json
{
  "status": 409,
  "code": "INSUFFICIENT_STOCK",
  "message": "Only 2 items left in stock.",
  "errors": null,
  "details": { "variantId": 41, "availableStock": 2 },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Field | Description |
|---|---|
| `status` | HTTP status code |
| `code` | Stable error code (§4.3); MVC switches on this, not on `message` |
| `message` | Human-readable message, safe to show to the user |
| `errors` | Validation errors only: `{ "fieldName": ["message", ...] }` |
| `details` | Extra data for the client, e.g. available stock or the new total |
| `traceId` | Request trace id for log lookup |

Validation example (`400`):

```json
{
  "status": 400,
  "code": "VALIDATION_FAILED",
  "message": "One or more fields are invalid.",
  "errors": {
    "email": ["Email is not a valid email address."],
    "password": ["Password must be at least 8 characters."]
  },
  "details": null,
  "traceId": "..."
}
```

### 4.2 Exception mapping

| Application exception | Status |
|---|---|
| `ValidationException` (FluentValidation) | 400 |
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ServiceUnavailableException` | 503 |
| `DbUpdateConcurrencyException` | 409 `CONCURRENCY_CONFLICT` |
| Any other exception | 500 `INTERNAL_ERROR` (message is generic) |

### 4.3 Error codes

| Code | Status | Raised when |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Input fails validation |
| `CART_OWNER_REQUIRED` | 400 | Cart call has neither a token nor `X-Cart-Session` |
| `INVALID_RESET_TOKEN` | 400 | Reset token is wrong, expired or used (BR-05) |
| `INCORRECT_PASSWORD` | 400 | Current password is wrong (BR-06) |
| `VOUCHER_NOT_ELIGIBLE` | 400 / 409 | Voucher cannot be applied (BR-26); `details.reason` is `Inactive`, `NotStarted`, `Expired`, `BelowMinimumOrder` or `UsageLimitReached`. 400 on preview, 409 on place order |
| `INVALID_CATEGORY_PARENT` | 400 | Category would become its own ancestor (BR-13) |
| `INVALID_SALE` | 400 | Sale price / period breaks BR-14 |
| `UNSUPPORTED_FILE` | 400 | Upload type or size not allowed |
| `CANNOT_MODIFY_SELF` | 400 | Admin locks themselves or changes their own role |
| `UNAUTHORIZED` | 401 | Missing, invalid or expired access token |
| `INVALID_CREDENTIALS` | 401 | Wrong email or password |
| `INVALID_REFRESH_TOKEN` | 401 | Refresh token wrong, expired or revoked |
| `INVALID_WEBHOOK_SIGNATURE` | 401 | Webhook key / signature check failed |
| `FORBIDDEN` | 403 | Role not allowed |
| `ACCOUNT_LOCKED` | 403 | Login or refresh on a locked account (BR-03) |
| `NOT_FOUND` | 404 | Resource missing, hidden or not owned |
| `VOUCHER_NOT_FOUND` | 404 | Voucher code does not exist |
| `EMAIL_ALREADY_EXISTS` | 409 | Register with an existing email (BR-01) |
| `SLUG_ALREADY_EXISTS` | 409 | Duplicate category or product slug (BR-12) |
| `SKU_ALREADY_EXISTS` | 409 | Duplicate SKU (BR-12) |
| `VARIANT_OPTION_EXISTS` | 409 | Same size + color already exists on the product (BR-12) |
| `VOUCHER_CODE_EXISTS` | 409 | Duplicate voucher code (BR-25) |
| `ITEM_UNAVAILABLE` | 409 | Product or variant is hidden (BR-10) |
| `INSUFFICIENT_STOCK` | 409 | Quantity exceeds stock (BR-20, BR-23) |
| `NO_ITEMS_SELECTED` | 409 | Checkout with no selected cart lines (BR-23) |
| `TOTAL_CHANGED` | 409 | Backend total differs from `expectedTotal`; `details.newTotal` |
| `STOCK_CHANGED` | 409 | Stock edited concurrently (UC-51); `details.currentStock` |
| `INVALID_STATUS_TRANSITION` | 409 | Order status change not allowed (BR-35) |
| `ORDER_NOT_CANCELABLE` | 409 | Order is not `Pending` / `Confirmed` (BR-36) |
| `PAYMENT_NOT_AVAILABLE` | 409 | VietQR requested for a non-Online, paid or canceled order |
| `PAYMENT_EXPIRED` | 409 | VietQR requested after the payment deadline (BR-41); `details.expiredAt` |
| `ORDER_NOT_PAID` | 409 | Confirming a VietQR order that is not `Paid` yet (BR-35) |
| `PAYMENT_ACTION_NOT_ALLOWED` | 409 | Payment status change breaks BR-34 |
| `TRANSACTION_CODE_USED` | 409 | Bank reference already used by another order (BR-33) |
| `TRANSACTION_RECORDED` | 409 | Manual confirm with a bank reference already in `PaymentTransactions`; resolve it in §6.14 instead (BR-42) |
| `TRANSACTION_ALREADY_RESOLVED` | 409 | Payment transaction is `Matched` or already has a resolution (BR-42) |
| `ORDER_NOT_PAYABLE` | 409 | Applying a transfer to an order that is not `Online` / `Unpaid`, is canceled, or costs more than the transfer (BR-42) |
| `REVIEW_NOT_ALLOWED` | 409 | Order is not `Completed` (BR-37) |
| `ALREADY_REVIEWED` | 409 | Order item already has a review (BR-37) |
| `CONCURRENCY_CONFLICT` | 409 | Optimistic concurrency failure |
| `TOO_MANY_REQUESTS` | 429 | Rate limit exceeded (§10); `Retry-After` header is set |
| `EMAIL_SERVICE_UNAVAILABLE` | 503 | Reset email could not be sent |
| `INTERNAL_ERROR` | 500 | Unexpected error |

---

## 5. Shared Types

### 5.1 Enums

| Enum | Values | Source |
|---|---|---|
| `UserRole` | `Customer`, `Staff`, `Admin` | `CK_Users_Role` |
| `UserStatus` | `Active`, `Locked` | `CK_Users_Status` |
| `OrderStatus` | `Pending`, `Confirmed`, `Shipping`, `Completed`, `Canceled` | `CK_Orders_OrderStatus` |
| `PaymentMethod` | `COD`, `Online` (`Online` = VietQR bank transfer) | `CK_Orders_PaymentMethod` |
| `PaymentStatus` | `Unpaid`, `Paid`, `Failed`, `Refunded` | `CK_Orders_PaymentStatus` |
| `DiscountType` | `Percent`, `Fixed` | `CK_Vouchers_DiscountType` |
| `Gender` | `Male`, `Female`, `Other` | Proposed (column is free text) |
| `GenderTarget` | `Men`, `Women`, `Unisex`, `Kids` | Proposed (column is free text) |
| `PaymentAction` | `ConfirmCod`, `ConfirmTransfer`, `Refund` | UC-57 |
| `PaymentMatchResult` | `Matched`, `NoOrder`, `Underpaid`, `OrderNotPayable` | `CK_PaymentTransactions_MatchResult` |
| `PaymentResolution` | `Applied`, `Refunded`, `Ignored` | `CK_PaymentTransactions_Resolution` |
| `GroupBy` | `day`, `month` | UC-67, UC-70 |

### 5.2 Common DTOs

`UserSummaryDto`

```json
{ "id": 12, "fullName": "Nguyen Van A", "email": "a@example.com", "role": "Customer", "avatarUrl": null }
```

`AddressDto`

```json
{
  "id": 3,
  "receiverName": "Nguyen Van A",
  "receiverPhone": "0901234567",
  "province": "TP. Ho Chi Minh",
  "district": "Quan 1",
  "ward": "Phuong Ben Nghe",
  "streetAddress": "12 Le Loi",
  "fullAddress": "12 Le Loi, Phuong Ben Nghe, Quan 1, TP. Ho Chi Minh",
  "isDefault": true
}
```

`ProductListDto` (also the OData entity of `/odata/products`)

```json
{
  "id": 101,
  "name": "Basic T-Shirt",
  "slug": "basic-t-shirt",
  "brand": "Daisy",
  "categoryId": 5,
  "genderTarget": "Unisex",
  "basePrice": 199000,
  "salePrice": 149000,
  "effectivePrice": 149000,
  "isOnSale": true,
  "saleEndAt": "2026-10-10T23:59:59+07:00",
  "thumbnailUrl": "/uploads/products/101/thumb.webp",
  "averageRating": 4.6,
  "reviewCount": 23,
  "soldCount": 310,
  "isFeatured": true,
  "createdAt": "2026-09-01T09:00:00+07:00"
}
```

`effectivePrice` = active sale price or base price (BR-14). Variant prices add `extraPrice`.

---

## 6. REST Endpoints

Each endpoint lists: policy (§3.3), related use case, request, success response and specific errors. `401` / `403` for missing or wrong role are implied and not repeated.

### 6.1 Auth — `AuthController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| POST | `/api/auth/register` | Public | UC-01 | 201 |
| POST | `/api/auth/login` | Public | UC-02 | 200 |
| POST | `/api/auth/logout` | Authenticated | UC-03 | 204 |
| POST | `/api/auth/refresh` | Public | UC-04 | 200 |
| POST | `/api/auth/forgot-password` | Public | UC-05 | 200 |
| POST | `/api/auth/reset-password` | Public | UC-06 | 204 |
| POST | `/api/auth/change-password` | Authenticated | UC-07 | 204 |

#### `POST /api/auth/register`

```json
{
  "fullName": "Nguyen Van A",
  "email": "a@example.com",
  "password": "Secret@123",
  "confirmPassword": "Secret@123"
}
```

Validation: `fullName` 1–150 chars; `email` valid, max 255, compared case-insensitively; `password` 8–100 chars with at least one letter and one digit; `confirmPassword` equals `password`.

`201 Created`:

```json
{ "id": 12, "fullName": "Nguyen Van A", "email": "a@example.com", "role": "Customer", "avatarUrl": null }
```

The role is always `Customer` (BR-07); no tokens are returned — the user logs in next.

Errors: `400 VALIDATION_FAILED`, `409 EMAIL_ALREADY_EXISTS`.

#### `POST /api/auth/login`

Headers: optional `X-Cart-Session` (guest cart to merge).

```json
{ "email": "a@example.com", "password": "Secret@123" }
```

`200 OK` — `AuthResultDto`:

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-10-01T14:45:00+07:00",
  "refreshToken": "q3V0Z...",
  "refreshTokenExpiresAt": "2026-10-08T14:30:00+07:00",
  "user": { "id": 12, "fullName": "Nguyen Van A", "email": "a@example.com", "role": "Customer", "avatarUrl": null }
}
```

If `X-Cart-Session` is present, the guest cart is merged into the user cart before responding (UC-31, BR-22): quantities of the same variant are added and capped at stock; the guest lines are deleted.

Errors: `401 INVALID_CREDENTIALS` (same response for unknown email and wrong password), `403 ACCOUNT_LOCKED`.

#### `POST /api/auth/logout`

No body. Clears the stored refresh token. `204 No Content`.

If the access token has already expired, MVC simply deletes its cookie; the stored refresh token then expires on its own.

#### `POST /api/auth/refresh`

```json
{ "refreshToken": "q3V0Z..." }
```

`200 OK` — `AuthResultDto` with a new access token and a new refresh token (rotation).

Errors: `401 INVALID_REFRESH_TOKEN`, `403 ACCOUNT_LOCKED`.

#### `POST /api/auth/forgot-password`

```json
{ "email": "a@example.com" }
```

`200 OK` with the same body whether or not the email exists (BR-05):

```json
{ "message": "If the email exists, a reset link has been sent." }
```

The email contains `{MvcBaseUrl}/account/reset-password?token={token}`; the token is valid for 30 minutes (configurable) and stored hashed.

Errors: `400 VALIDATION_FAILED`, `503 EMAIL_SERVICE_UNAVAILABLE`.

#### `POST /api/auth/reset-password`

```json
{ "token": "x8Fq...", "newPassword": "NewSecret@123", "confirmPassword": "NewSecret@123" }
```

On success the token is cleared and the refresh token revoked. `204 No Content`.

Errors: `400 VALIDATION_FAILED`, `400 INVALID_RESET_TOKEN`.

#### `POST /api/auth/change-password`

```json
{ "currentPassword": "Secret@123", "newPassword": "NewSecret@123", "confirmPassword": "NewSecret@123" }
```

`204 No Content`. The refresh token is revoked, so other sessions end; MVC logs in again with the new password or asks the user to.

Errors: `400 VALIDATION_FAILED`, `400 INCORRECT_PASSWORD`.

---

### 6.2 Profile & Address Book — `ProfileController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/profile` | Customer | UC-08 | 200 |
| PUT | `/api/profile` | Customer | UC-09 | 200 |
| GET | `/api/profile/addresses` | Customer | UC-10 | 200 |
| POST | `/api/profile/addresses` | Customer | UC-11 | 201 |
| PUT | `/api/profile/addresses/{id}` | Customer | UC-11 | 200 |
| DELETE | `/api/profile/addresses/{id}` | Customer | UC-11 | 204 |
| PUT | `/api/profile/addresses/{id}/default` | Customer | UC-12 | 204 |

#### `GET /api/profile`

`200 OK` — `ProfileDto`:

```json
{
  "id": 12,
  "fullName": "Nguyen Van A",
  "email": "a@example.com",
  "phone": "0901234567",
  "gender": "Male",
  "dateOfBirth": "2000-05-20",
  "avatarUrl": "/uploads/avatars/12.webp",
  "role": "Customer",
  "createdAt": "2026-09-01T09:00:00+07:00"
}
```

Password hash and tokens are never returned (BR-02).

#### `PUT /api/profile`

`multipart/form-data`:

| Field | Type | Rules |
|---|---|---|
| `fullName` | string | Required, 1–150 |
| `phone` | string | Optional, Vietnamese phone format, max 20 |
| `gender` | `Gender` | Optional |
| `dateOfBirth` | date | Optional, in the past |
| `avatar` | file | Optional, `image/jpeg`, `image/png`, `image/webp`, max 2 MB |
| `removeAvatar` | bool | Optional, clears the avatar |

`email` is not editable (BR-01). `200 OK` — updated `ProfileDto`.

Errors: `400 VALIDATION_FAILED`, `400 UNSUPPORTED_FILE`.

#### `GET /api/profile/addresses`

`200 OK` — `AddressDto[]`, default address first, then newest.

#### `POST /api/profile/addresses`

```json
{
  "receiverName": "Nguyen Van A",
  "receiverPhone": "0901234567",
  "province": "TP. Ho Chi Minh",
  "district": "Quan 1",
  "ward": "Phuong Ben Nghe",
  "streetAddress": "12 Le Loi",
  "isDefault": false
}
```

The first address of a customer becomes the default automatically. If `isDefault` is `true`, the previous default is cleared in the same transaction (BR-09).

`201 Created` — `AddressDto`, `Location: /api/profile/addresses/{id}` (no single-address `GET`; the location is informational).

#### `PUT /api/profile/addresses/{id}`

Same body as `POST` without `isDefault`. `200 OK` — `AddressDto`. Existing orders keep their snapshot (BR-28).

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`.

#### `DELETE /api/profile/addresses/{id}`

`204 No Content`. If it was the default, no default remains (BR-09). Errors: `404 NOT_FOUND`.

#### `PUT /api/profile/addresses/{id}/default`

No body. `204 No Content`. Errors: `404 NOT_FOUND`.

---

### 6.3 Catalog — `CategoriesController`, `ProductsController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/categories` | Public | UC-13 | 200 |
| GET | `/api/products/{slug}` | Public | UC-15 | 200 |
| GET | `/api/products/{productId:int}/reviews` | Public | UC-42 | 200 |
| GET | `/odata/products` | Public | UC-14, UC-16 – UC-22 | 200 (§7.1) |

#### `GET /api/categories`

Active categories only (BR-10), as a tree ordered by `sortOrder` (BR-13). A hidden parent hides its children.

`200 OK` — `CategoryTreeDto[]`:

```json
[
  {
    "id": 1,
    "name": "Men",
    "slug": "men",
    "imageUrl": "/uploads/categories/men.webp",
    "sortOrder": 0,
    "children": [
      { "id": 5, "name": "Shirts", "slug": "men-shirts", "imageUrl": null, "sortOrder": 0, "children": [] }
    ]
  }
]
```

#### `GET /api/products/{slug}`

`200 OK` — `ProductDetailDto`:

```json
{
  "id": 101,
  "slug": "basic-t-shirt",
  "name": "Basic T-Shirt",
  "brand": "Daisy",
  "description": "100% cotton t-shirt.",
  "material": "Cotton",
  "genderTarget": "Unisex",
  "category": { "id": 5, "name": "Shirts", "slug": "men-shirts" },
  "basePrice": 199000,
  "salePrice": 149000,
  "effectivePrice": 149000,
  "isOnSale": true,
  "saleEndAt": "2026-10-10T23:59:59+07:00",
  "averageRating": 4.6,
  "reviewCount": 23,
  "soldCount": 310,
  "variants": [
    {
      "id": 41,
      "sku": "TS-BASIC-M-BLK",
      "size": "M",
      "color": "Black",
      "colorHex": "#000000",
      "extraPrice": 0,
      "price": 149000,
      "stockQuantity": 2,
      "inStock": true,
      "isLowStock": true
    }
  ],
  "images": [
    { "id": 900, "url": "/uploads/products/101/1.webp", "isThumbnail": true, "sortOrder": 0, "variantId": null, "color": null }
  ]
}
```

- Only active variants are returned; `price = effectivePrice + extraPrice` (BR-14).
- `images` are ordered by `sortOrder`; `color` is the color of the linked variant, so MVC can switch the gallery by color (BR-16).

Errors: `404 NOT_FOUND` (missing, hidden product, or hidden category).

#### `GET /api/products/{productId}/reviews`

Query: `page`, `pageSize`, optional `rating` (1–5).

`200 OK` — `ProductReviewsDto` (visible reviews only, BR-39):

```json
{
  "averageRating": 4.6,
  "reviewCount": 23,
  "ratingBreakdown": { "5": 17, "4": 4, "3": 1, "2": 1, "1": 0 },
  "reviews": {
    "items": [
      {
        "id": 77,
        "rating": 5,
        "comment": "Good fabric.",
        "userFullName": "Nguyen V. A",
        "userAvatarUrl": null,
        "size": "M",
        "color": "Black",
        "createdAt": "2026-09-20T10:00:00+07:00"
      }
    ],
    "page": 1, "pageSize": 10, "totalCount": 23, "totalPages": 3
  }
}
```

Errors: `404 NOT_FOUND`.

---

### 6.4 Wishlist — `WishlistController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/wishlist` | Customer | UC-25 | 200 |
| POST | `/api/wishlist/{productId}` | Customer | UC-23 | 204 |
| DELETE | `/api/wishlist/{productId}` | Customer | UC-24 | 204 |

- `GET` returns `WishlistItemDto[]`, newest first: `{ productId, slug, name, thumbnailUrl, basePrice, effectivePrice, isOnSale, isAvailable, addedAt }`. Products hidden after being added are returned with `isAvailable: false`.
- `POST` is idempotent: adding a product already in the wishlist returns `204` (BR-18). `404 NOT_FOUND` if the product is missing or hidden.
- `DELETE` is idempotent: removing a product not in the wishlist also returns `204`.

---

### 6.5 Cart — `CartController`

Policy `CartOwner` (§3.4) for all endpoints.

| Method | Path | UC | Success |
|---|---|---|---|
| GET | `/api/cart` | UC-27 | 200 |
| POST | `/api/cart/items` | UC-26 | 200 |
| PUT | `/api/cart/items/{id}` | UC-28 | 200 |
| DELETE | `/api/cart/items/{id}` | UC-29 | 200 |
| PUT | `/api/cart/selection` | UC-30 | 200 |

#### `CartDto`

Prices are read live on every call (BR-21).

```json
{
  "items": [
    {
      "id": 501,
      "variantId": 41,
      "productId": 101,
      "productSlug": "basic-t-shirt",
      "productName": "Basic T-Shirt",
      "thumbnailUrl": "/uploads/products/101/1.webp",
      "size": "M",
      "color": "Black",
      "unitPrice": 149000,
      "basePrice": 199000,
      "quantity": 2,
      "lineTotal": 298000,
      "stockQuantity": 2,
      "isSelected": true,
      "isAvailable": true,
      "unavailableReason": null
    }
  ],
  "itemCount": 1,
  "totalQuantity": 2,
  "selectedCount": 1,
  "selectedSubtotal": 298000
}
```

`unavailableReason`: `null`, `Hidden` (product or variant hidden, BR-10) or `OutOfStock` / `NotEnoughStock`. Unavailable lines are never counted in `selectedSubtotal`.

#### `POST /api/cart/items`

```json
{ "variantId": 41, "quantity": 1 }
```

Adds a line, or increases the quantity of the existing line for the same variant (BR-19). New lines are selected.

`200 OK` — `CartSummaryDto`:

```json
{ "itemCount": 3, "totalQuantity": 5 }
```

`200` (not `201`) because the call may update an existing line.

Errors: `400 VALIDATION_FAILED` (`quantity < 1`), `404 NOT_FOUND` (variant missing or hidden), `409 INSUFFICIENT_STOCK` (`details.availableStock`, counting the quantity already in the cart).

#### `PUT /api/cart/items/{id}`

```json
{ "quantity": 3 }
```

`200 OK` — `CartDto`. Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 INSUFFICIENT_STOCK`.

#### `DELETE /api/cart/items/{id}`

`200 OK` — `CartDto` (so the page can re-render totals). Errors: `404 NOT_FOUND`.

#### `PUT /api/cart/selection`

```json
{ "itemIds": [501, 502], "selected": true }
```

"Select all" sends every line id. Ids not owned by the caller are ignored.

`200 OK` — `CartDto`. Errors: `409 ITEM_UNAVAILABLE` when selecting an unavailable line.

---

### 6.6 Checkout — `CheckoutController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/checkout` | Customer | UC-32, UC-33 | 200 |
| POST | `/api/checkout/voucher` | Customer | UC-33 | 200 |
| POST | `/api/checkout` | Customer | UC-34, UC-35 | 201 |

The backend always computes every amount (BR-24); the client never sends prices.

#### `GET /api/checkout`

Returns the checkout preview for the selected cart lines, without a voucher.

`200 OK` — `CheckoutDto`:

```json
{
  "items": [
    { "cartItemId": 501, "variantId": 41, "productName": "Basic T-Shirt", "size": "M", "color": "Black",
      "thumbnailUrl": "/uploads/products/101/1.webp", "unitPrice": 149000, "quantity": 2, "lineTotal": 298000,
      "isAvailable": true, "unavailableReason": null }
  ],
  "addresses": [ { "id": 3, "receiverName": "Nguyen Van A", "...": "AddressDto" } ],
  "defaultAddressId": 3,
  "paymentMethods": ["COD", "Online"],
  "totals": {
    "subtotalAmount": 298000,
    "discountAmount": 0,
    "shippingFee": 30000,
    "totalAmount": 328000,
    "voucher": null
  },
  "canPlaceOrder": true
}
```

`canPlaceOrder` is `false` when there are no selected lines, any selected line is unavailable, or the customer has no address.

#### `POST /api/checkout/voucher`

```json
{ "code": "DAISY10" }
```

Previews the totals with the voucher. It does **not** reserve the voucher; the voucher is checked again at order placement (BR-26). Removing the voucher = calling `GET /api/checkout` again.

`200 OK` — `CheckoutTotalsDto`:

```json
{
  "subtotalAmount": 298000,
  "discountAmount": 29800,
  "shippingFee": 30000,
  "totalAmount": 298200,
  "voucher": { "code": "DAISY10", "name": "10% off", "discountType": "Percent", "discountValue": 10, "maxDiscountAmount": 50000 }
}
```

Discount (BR-27): `Percent` → `subtotal × value / 100`, capped at `maxDiscountAmount`; `Fixed` → `value`; never above the subtotal; never reduces shipping.

Errors: `404 VOUCHER_NOT_FOUND`, `400 VOUCHER_NOT_ELIGIBLE` (`details.reason`), `409 NO_ITEMS_SELECTED`.

#### `POST /api/checkout`

```json
{
  "addressId": 3,
  "paymentMethod": "Online",
  "voucherCode": "DAISY10",
  "customerNote": "Call before delivery",
  "expectedTotal": 298200
}
```

| Field | Rules |
|---|---|
| `addressId` | Required, must belong to the caller (BR-08, BR-23) |
| `paymentMethod` | `COD` or `Online` (BR-31) |
| `voucherCode` | Optional, at most one voucher (BR-26) |
| `customerNote` | Optional, max 500 |
| `expectedTotal` | Required — the total the customer saw; protects against price changes between preview and confirm |

Processing (one transaction, BR-30): validate items and stock → validate voucher → compute totals → compare with `expectedTotal` → generate `OrderCode` (BR-29) → insert `Order` (`Pending`, `Unpaid`; for `Online` also `PaymentExpiresAt = now + VietQr:PaymentTimeoutMinutes`, BR-41) with address snapshot and `OrderItems` with product snapshots (BR-28) → conditional stock deduction → `SoldCount += qty` → voucher `UsedCount += 1` (conditional) → delete the ordered cart lines → commit.

`201 Created`, `Location: /api/orders/{id}` — `PlaceOrderResultDto`:

```json
{
  "orderId": 2001,
  "orderCode": "DS2610014K7Q",
  "orderStatus": "Pending",
  "paymentMethod": "Online",
  "paymentStatus": "Unpaid",
  "totalAmount": 298200,
  "paymentExpiresAt": "2026-10-01T15:00:00+07:00",
  "paymentQrUrl": "/api/orders/2001/payment-qr"
}
```

`paymentExpiresAt` and `paymentQrUrl` are `null` for COD. For `Online`, MVC immediately calls `paymentQrUrl` (UC-35).

Errors:

| Code | Status | Details |
|---|---|---|
| `VALIDATION_FAILED` | 400 | |
| `NOT_FOUND` | 404 | Address not found / not owned |
| `NO_ITEMS_SELECTED` | 409 | |
| `ITEM_UNAVAILABLE`, `INSUFFICIENT_STOCK` | 409 | `details.items: [{ cartItemId, reason, availableStock }]` |
| `VOUCHER_NOT_ELIGIBLE` | 409 | `details.reason` |
| `TOTAL_CHANGED` | 409 | `details.newTotal`; MVC shows the new total and asks to confirm again |
| `CONCURRENCY_CONFLICT` | 409 | A conditional update affected 0 rows (stock or voucher taken by another order); rolled back |

Double submit: the ordered cart lines are removed in the same transaction, so a repeated request returns `409 NO_ITEMS_SELECTED` instead of creating a second order.

---

### 6.7 My Orders — `OrdersController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/orders` | Customer | UC-37 | 200 |
| GET | `/api/orders/{id}` | Customer | UC-35, UC-38, UC-39 | 200 |
| GET | `/api/orders/{id}/payment-qr` | Customer | UC-35, UC-38 | 200 |
| POST | `/api/orders/{id}/cancel` | Customer | UC-40 | 200 |

#### `GET /api/orders`

Query: `page`, `pageSize`, optional `status` (`OrderStatus`). Newest first.

`200 OK` — `PagedResult<OrderSummaryDto>`:

```json
{
  "items": [
    {
      "id": 2001,
      "orderCode": "DS2610014K7Q",
      "createdAt": "2026-10-01T14:30:00+07:00",
      "orderStatus": "Pending",
      "paymentMethod": "Online",
      "paymentStatus": "Unpaid",
      "totalAmount": 298200,
      "itemCount": 2,
      "thumbnailUrl": "/uploads/products/101/1.webp"
    }
  ],
  "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1
}
```

#### `GET /api/orders/{id}`

`200 OK` — `OrderDetailDto` (all product and address data from snapshots, BR-28):

```json
{
  "id": 2001,
  "orderCode": "DS2610014K7Q",
  "orderStatus": "Pending",
  "paymentMethod": "Online",
  "paymentStatus": "Unpaid",
  "receiverName": "Nguyen Van A",
  "receiverPhone": "0901234567",
  "shippingAddress": "12 Le Loi, Phuong Ben Nghe, Quan 1, TP. Ho Chi Minh",
  "customerNote": "Call before delivery",
  "items": [
    {
      "orderItemId": 3001,
      "productId": 101,
      "productSlug": "basic-t-shirt",
      "productName": "Basic T-Shirt",
      "size": "M",
      "color": "Black",
      "imageUrl": "/uploads/products/101/1.webp",
      "unitPrice": 149000,
      "quantity": 2,
      "lineTotal": 298000,
      "canReview": false,
      "reviewId": null
    }
  ],
  "voucherCode": "DAISY10",
  "subtotalAmount": 298000,
  "discountAmount": 29800,
  "shippingFee": 30000,
  "totalAmount": 298200,
  "createdAt": "2026-10-01T14:30:00+07:00",
  "paymentExpiresAt": "2026-10-01T15:00:00+07:00",
  "paidAt": null,
  "completedAt": null,
  "canceledAt": null,
  "cancelReason": null,
  "canCancel": true,
  "canPayWithQr": true
}
```

- `canCancel`: status is `Pending` or `Confirmed` (BR-36).
- `canPayWithQr`: `Online`, `Unpaid`, not `Canceled`, and `paymentExpiresAt` not passed (BR-41).
- `canReview`: order `Completed` and the item has no review (BR-37).
- MVC polls this endpoint every 3–5 seconds while showing the QR code (UC-35) and stops when `paymentStatus` is `Paid`, when `orderStatus` is `Canceled`, or when `paymentExpiresAt` + grace period has passed.
- An order canceled by the payment deadline (UC-72) has `cancelReason = "Payment not received in time"`.

Errors: `404 NOT_FOUND`.

#### `GET /api/orders/{id}/payment-qr`

`200 OK` — `PaymentQrDto` (BR-32):

```json
{
  "orderId": 2001,
  "orderCode": "DS2610014K7Q",
  "amount": 298200,
  "bankBin": "970436",
  "bankName": "Vietcombank",
  "accountNumber": "0123456789",
  "accountName": "CONG TY DAISY",
  "transferContent": "DS2610014K7Q",
  "qrImageUrl": "https://img.vietqr.io/image/970436-0123456789-compact2.png?amount=298200&addInfo=DS2610014K7Q&accountName=CONG%20TY%20DAISY",
  "expiresAt": "2026-10-01T15:00:00+07:00"
}
```

The QR is generated on each call from `VietQrOptions` (not stored), so it can be reopened from the order detail page (UC-38) until `expiresAt`. MVC shows a countdown to `expiresAt`. The grace period is not shown to the customer.

Errors: `404 NOT_FOUND`, `409 PAYMENT_NOT_AVAILABLE` (COD, already paid, or canceled), `409 PAYMENT_EXPIRED` (deadline passed; the order is canceled by UC-72 once the grace period ends).

#### `POST /api/orders/{id}/cancel`

```json
{ "reason": "Changed my mind" }
```

`reason` required, max 500 (BR-36). In one transaction: status `Canceled`, `CancelReason`, `CanceledAt`; stock restored, `SoldCount` and voucher `UsedCount` decreased.

`200 OK` — `CanceledOrderDto`:

```json
{
  "id": 2001,
  "orderCode": "DS2610014K7Q",
  "orderStatus": "Canceled",
  "paymentStatus": "Paid",
  "canceledAt": "2026-10-01T15:00:00+07:00",
  "refundRequired": true
}
```

`refundRequired` is `true` when the order was already paid; staff then refund it (UC-57).

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 ORDER_NOT_CANCELABLE`.

---

### 6.8 Payments — `PaymentsController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| POST | `/api/payments/webhook` | Webhook | UC-36 | 200 |

Called by the **SePay** bank webhook service when a transfer arrives on the receiving account. The SePay payload is mapped to a normalized `BankTransfer` inside `SePayPaymentGateway`; the rest of the flow is provider-independent. With `VietQr:Provider = Mock` (development), `MockPaymentGateway` accepts the same payload and key.

#### Authentication

- SePay sends the configured key as `Authorization: Apikey {VietQr:WebhookApiKey}`.
- Transfers to an account other than `VietQr:AccountNumber`, and outgoing transfers (`transferType = "out"`), are acknowledged and ignored. When SePay sends no `referenceCode`, the reference becomes `SEPAY-{id}`.
- Verification happens **before** the body is used (BR-33). Comparison is constant-time.
- Failure → `401 INVALID_WEBHOOK_SIGNATURE`.

#### Normalized request

| Field | Description |
|---|---|
| `reference` | Bank transaction reference (unique per transfer) → `Orders.TransactionCode` |
| `amount` | Amount received, VND |
| `content` | Transfer content entered by the customer |
| `transferredAt` | Transfer time → `Orders.PaidAt` |
| `accountNumber` | Receiving account; must equal `VietQrOptions.AccountNumber` |
| `direction` | Only incoming transfers are processed; outgoing ones are acknowledged and ignored |

Example of a provider payload (SePay-style):

```json
{
  "id": 92704,
  "gateway": "Vietcombank",
  "transactionDate": "2026-10-01 14:32:10",
  "accountNumber": "0123456789",
  "content": "DS2610014K7Q chuyen tien",
  "transferType": "in",
  "transferAmount": 298200,
  "referenceCode": "FT26274123456"
}
```

#### Processing (BR-32, BR-33, BR-34, BR-42)

1. Verify the key / signature.
2. If `PaymentTransactions` already has `BankReference = reference` → acknowledge, no change (idempotent).
3. Extract the `OrderCode` from `content` (case-insensitive match of the `DS…` pattern, ignoring spaces and punctuation) and find the order.
4. Decide `MatchResult`:

   | Condition | `MatchResult` |
   |---|---|
   | No `OrderCode` found, or no such order | `NoOrder` |
   | Order is COD, already `Paid`, or `Canceled` (including expired, BR-41) | `OrderNotPayable` |
   | `amount < TotalAmount` | `Underpaid` |
   | Otherwise | `Matched` |

5. In one database transaction:
   - if `Matched`: `UPDATE Orders SET PaymentStatus = 'Paid', PaidAt = transferredAt, TransactionCode = reference WHERE OrderId = @id AND PaymentStatus = 'Unpaid' AND OrderStatus <> 'Canceled'`. If 0 rows are affected (the expiry job canceled it at the same moment), the result becomes `OrderNotPayable`.
   - insert the `PaymentTransaction` with its `MatchResult` and the related `OrderId` (if any).
   - if the insert hits `UQ_PaymentTransactions_BankReference` (the same webhook delivered twice at the same moment), roll back and acknowledge.
6. Transfers that are not `Matched` appear in the staff reconciliation list (§6.14, UC-71).

A webhook never changes stock, `SoldCount` or voucher usage.

#### Response

`200 OK` for every verified request, including duplicates and unmatched transfers, so the provider does not retry them:

```json
{ "success": true }
```

Only a transient server error returns `5xx`, which lets the provider retry; step 2 makes the retry safe.

#### `POST /api/dev/payments/simulate-transfer` (Development only)

Available only when the API runs in the Development environment with `VietQr:Provider = Mock`; otherwise `404`. Policy: Authenticated.

```json
{ "orderCode": "DS2610014K7Q", "amount": null, "content": null }
```

Builds a SePay-format webhook (amount defaults to the order total, content to the order code) and runs it through the normal webhook flow above. `200 OK` — `{ "success": true }`. The MVC payment page shows a "simulate transfer" button in development that calls it.

---

### 6.9 Reviews — `ReviewsController`

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| POST | `/api/reviews` | Customer | UC-41 | 201 |

```json
{ "orderItemId": 3001, "rating": 5, "comment": "Good fabric." }
```

`rating` is an integer 1–5; `comment` optional, max 1000 (BR-37). The user and product are taken from the order item, not from the request. After saving, `AverageRating` and `ReviewCount` are recalculated from visible reviews (BR-38).

`201 Created` — `ReviewDto` (same shape as in §6.3).

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND` (item missing or not owned), `409 REVIEW_NOT_ALLOWED`, `409 ALREADY_REVIEWED`.

Editing and deleting reviews are not in scope (features.docx defines only REV-01).

---

### 6.10 Admin — Categories (`Admin/CategoriesController`)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/admin/categories` | Staff | UC-43 *(added)* | 200 |
| GET | `/api/admin/categories/{id}` | Staff | UC-43 *(added)* | 200 |
| POST | `/api/admin/categories` | Staff | UC-43 | 201 |
| PUT | `/api/admin/categories/{id}` | Staff | UC-43 | 200 |
| PATCH | `/api/admin/categories/{id}/visibility` | Staff | UC-44 | 204 |

- `GET` returns the full tree including hidden categories, with `isActive` and `productCount` on each node.
- `GET /{id}` returns one `CategoryDto` (used by the edit form).
- `POST` / `PUT` body:

```json
{ "name": "Shirts", "slug": "men-shirts", "parentCategoryId": 1, "imageUrl": null, "sortOrder": 0 }
```

`slug`: lowercase letters, digits and `-`, max 200, unique (BR-12). `parentCategoryId` cannot be the category itself or a descendant (BR-13). Response: `CategoryDto` `{ id, name, slug, parentCategoryId, imageUrl, sortOrder, isActive }`.

Errors: `400 VALIDATION_FAILED`, `400 INVALID_CATEGORY_PARENT`, `404 NOT_FOUND`, `409 SLUG_ALREADY_EXISTS`.

- `PATCH .../visibility`: `{ "isActive": false }`. Errors: `404 NOT_FOUND`.

### 6.11 Admin — Products, Variants, Images (`Admin/ProductsController`)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/admin/products` | Staff | UC-45 *(added)* | 200 |
| GET | `/api/admin/products/{id}` | Staff | UC-45 *(added)* | 200 |
| POST | `/api/admin/products` | Staff | UC-45 | 201 |
| PUT | `/api/admin/products/{id}` | Staff | UC-45 | 200 |
| PATCH | `/api/admin/products/{id}/visibility` | Staff | UC-46 | 204 |
| PATCH | `/api/admin/products/{id}/featured` | Staff | UC-49 | 204 |
| PUT | `/api/admin/products/{id}/sale` | **Admin** | UC-50 | 204 |
| DELETE | `/api/admin/products/{id}/sale` | **Admin** | UC-50 | 204 |
| POST | `/api/admin/products/{id}/variants` | Staff | UC-47 | 201 |
| PUT | `/api/admin/variants/{id}` | Staff | UC-47 | 200 |
| PATCH | `/api/admin/variants/{id}/visibility` | Staff | UC-47 | 204 |
| POST | `/api/admin/products/{id}/images` | Staff | UC-48 | 201 |
| PATCH | `/api/admin/images/{id}/thumbnail` | Staff | UC-48 | 204 |
| PUT | `/api/admin/products/{id}/images/order` | Staff | UC-48 | 204 |
| PATCH | `/api/admin/images/{id}/variant` | Staff | UC-48 | 204 |
| DELETE | `/api/admin/images/{id}` | Staff | UC-48 | 204 |

#### `GET /api/admin/products`

Query: `page`, `pageSize`, `search` (name, brand or SKU), `categoryId`, `isActive`, `isFeatured`, `isOnSale`, `lowStock` (bool). Includes hidden products.

`200 OK` — `PagedResult<ProductAdminListDto>`: `ProductListDto` fields plus `isActive`, `variantCount`, `totalStock`, `hasLowStock`.

#### `GET /api/admin/products/{id}`

`200 OK` — `ProductAdminDto`: all editable product fields, `salePrice`, `saleStartAt`, `saleEndAt`, `isActive`, `isFeatured`, and **all** variants and images (hidden ones included, each with `isActive`).

#### `POST /api/admin/products`, `PUT /api/admin/products/{id}`

```json
{
  "categoryId": 5,
  "name": "Basic T-Shirt",
  "slug": "basic-t-shirt",
  "brand": "Daisy",
  "description": "100% cotton t-shirt.",
  "material": "Cotton",
  "genderTarget": "Unisex",
  "basePrice": 199000
}
```

`basePrice >= 0` and not below an existing `salePrice` (BR-14). Products are created active. Response: `ProductAdminDto`; `201` with `Location: /api/admin/products/{id}` on create.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND` (product or category), `409 SLUG_ALREADY_EXISTS`.

#### Visibility and featured

- `PATCH .../visibility`: `{ "isActive": false }` — hides the product; cart lines become unavailable; orders unaffected (BR-10, BR-11).
- `PATCH .../featured`: `{ "isFeatured": true }` (BR-15).

#### `PUT /api/admin/products/{id}/sale` (Admin)

```json
{ "salePrice": 149000, "startAt": "2026-10-05T00:00:00+07:00", "endAt": "2026-10-10T23:59:59+07:00" }
```

`0 <= salePrice <= basePrice`; `startAt` / `endAt` optional, `startAt < endAt` when both set (BR-14). `DELETE .../sale` clears all three fields.

Errors: `400 INVALID_SALE`, `404 NOT_FOUND`.

#### Variants

`POST /api/admin/products/{id}/variants`:

```json
{ "sku": "TS-BASIC-M-BLK", "size": "M", "color": "Black", "colorHex": "#000000", "extraPrice": 0, "stockQuantity": 20, "lowStockThreshold": 5 }
```

`PUT /api/admin/variants/{id}` takes the same body **without** `stockQuantity` (stock changes go through §6.12).

Response: `VariantAdminDto` `{ id, productId, sku, size, color, colorHex, extraPrice, stockQuantity, lowStockThreshold, isActive }`.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 SKU_ALREADY_EXISTS`, `409 VARIANT_OPTION_EXISTS`.

`PATCH /api/admin/variants/{id}/visibility`: `{ "isActive": false }`. Variants are never deleted (BR-11).

#### Images

- `POST /api/admin/products/{id}/images` — `multipart/form-data`, field `files` (1–10 files), `image/jpeg`, `image/png` or `image/webp`, max 5 MB each. Files are saved through `IStorageService`; new images are appended to the end of the order. If the product had no images, the first becomes the thumbnail. `201 Created` — `ProductImageDto[]` `{ id, url, isThumbnail, sortOrder, variantId }`. Errors: `400 UNSUPPORTED_FILE`, `404 NOT_FOUND`.
- `PATCH /api/admin/images/{id}/thumbnail` — no body; clears the previous thumbnail in the same transaction (BR-16).
- `PUT /api/admin/products/{id}/images/order` — `{ "imageIds": [903, 900, 901] }`; must contain exactly the product's image ids, else `400`.
- `PATCH /api/admin/images/{id}/variant` — `{ "variantId": 41 }` or `{ "variantId": null }`; the variant must belong to the same product.
- `DELETE /api/admin/images/{id}` — deletes the record and the file. If it was the thumbnail, the next image by `sortOrder` becomes the thumbnail. Order snapshots keep their own `ImageUrlSnapshot` (BR-28), so the stored file of a snapshot is kept if still referenced.

### 6.12 Admin — Inventory (`Admin/InventoryController`)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| PUT | `/api/admin/variants/{id}/stock` | Staff | UC-51 | 204 |
| GET | `/api/admin/inventory/low-stock` | Staff | UC-52 | 200 |

#### `PUT /api/admin/variants/{id}/stock`

```json
{ "stockQuantity": 50, "expectedCurrentStock": 2 }
```

`stockQuantity >= 0` (BR-17). Optimistic check: the update runs `WHERE StockQuantity = @expectedCurrentStock`, so a sale that happened while staff were editing is not overwritten.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 STOCK_CHANGED` (`details.currentStock`).

#### `GET /api/admin/inventory/low-stock`

Query: optional `categoryId`, `includeHidden` (default `false`). Active variants with `StockQuantity <= LowStockThreshold`, lowest stock first.

`200 OK` — `LowStockItemDto[]`:

```json
[
  { "variantId": 41, "productId": 101, "productName": "Basic T-Shirt", "sku": "TS-BASIC-M-BLK",
    "size": "M", "color": "Black", "stockQuantity": 2, "lowStockThreshold": 5 }
]
```

### 6.13 Admin — Orders (`Admin/OrdersController`)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/odata/orders` | Staff | UC-53 | 200 (§7.2) |
| GET | `/api/admin/orders/{id}` | Staff | UC-54 | 200 |
| PATCH | `/api/admin/orders/{id}/status` | Staff | UC-55 | 204 |
| POST | `/api/admin/orders/{id}/cancel` | Staff | UC-56 | 200 |
| PATCH | `/api/admin/orders/{id}/payment` | Staff | UC-57 | 204 |

#### `GET /api/admin/orders/{id}`

`200 OK` — `OrderAdminDetailDto`: `OrderDetailDto` (§6.7) plus:

```json
{
  "customer": { "id": 12, "fullName": "Nguyen Van A", "email": "a@example.com", "phone": "0901234567" },
  "transactionCode": "FT26274123456",
  "allowedStatusTransitions": ["Confirmed"],
  "allowedPaymentActions": ["ConfirmTransfer"]
}
```

`allowedStatusTransitions` and `allowedPaymentActions` let MVC show only valid buttons; the API checks them again on submit.

#### `PATCH /api/admin/orders/{id}/status`

```json
{ "status": "Confirmed" }
```

Only one step forward: `Pending → Confirmed → Shipping → Completed` (BR-35). `Completed` sets `CompletedAt`. Cancel uses its own endpoint. A VietQR order can be confirmed only once it is `Paid`; until then it stays `Pending` and is canceled automatically if its deadline passes (BR-41).

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 INVALID_STATUS_TRANSITION`, `409 ORDER_NOT_PAID`.

#### `POST /api/admin/orders/{id}/cancel`

Same body, rules and response as the customer cancel (§6.7), without the ownership check.

#### `PATCH /api/admin/orders/{id}/payment`

```json
{ "action": "ConfirmTransfer", "transactionCode": "FT26274123456" }
```

| Action | Allowed when | Effect |
|---|---|---|
| `ConfirmCod` | `COD` and `Unpaid` | `Paid`, `PaidAt = now` |
| `ConfirmTransfer` | `Online`, `Unpaid`, not `Canceled`; `transactionCode` required, unused by any order and not recorded in `PaymentTransactions` | `Paid`, `PaidAt = now`, `TransactionCode` |
| `Refund` | `Paid` | `Refunded` (the money is returned outside Daisy) |

`ConfirmTransfer` is for transfers the webhook never reported. A transfer that reached Daisy but was not matched is resolved in §6.14 instead.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 PAYMENT_ACTION_NOT_ALLOWED`, `409 TRANSACTION_CODE_USED`, `409 TRANSACTION_RECORDED`.

### 6.14 Admin — Payment Transactions (`Admin/PaymentTransactionsController`)

Reconciliation of bank transfers recorded by the webhook (UC-71, BR-42).

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/admin/payment-transactions` | Staff | UC-71 | 200 |
| POST | `/api/admin/payment-transactions/{id}/apply` | Staff | UC-71 | 200 |
| PATCH | `/api/admin/payment-transactions/{id}/resolution` | Staff | UC-71 | 200 |

#### `PaymentTransactionDto`

```json
{
  "id": 310,
  "bankReference": "FT26274123999",
  "provider": "SePay",
  "accountNumber": "0123456789",
  "amount": 298200,
  "transferContent": "DS261001AK7Q chuyen tien",
  "transferredAt": "2026-10-01T14:40:02+07:00",
  "receivedAt": "2026-10-01T14:40:05+07:00",
  "matchResult": "NoOrder",
  "order": null,
  "needsReview": true,
  "resolution": null,
  "resolutionNote": null,
  "resolvedBy": null,
  "resolvedAt": null
}
```

`order` is `{ id, orderCode, orderStatus, paymentStatus, totalAmount }` when a related order is known. `resolvedBy` is `{ id, fullName }`. `needsReview` = not `Matched` and no `resolution`.

#### `GET /api/admin/payment-transactions`

Query: `page`, `pageSize`, `needsReview` (bool), `matchResult`, `resolution`, `from`, `to` (`transferredAt` dates), `search` (bank reference or transfer content). Newest first.

`200 OK` — `PagedResult<PaymentTransactionDto>`. MVC shows the number of transfers with `needsReview = true` as a badge in the admin menu.

#### `POST /api/admin/payment-transactions/{id}/apply`

```json
{ "orderId": 2001, "note": "Customer typed AK7Q instead of 4K7Q" }
```

Applies the transfer to an order. The order must be `Online`, `Unpaid`, not `Canceled`, and `TotalAmount <= amount`. In one transaction: the order becomes `Paid` (`PaidAt` = transfer time, `TransactionCode` = bank reference) with the same conditional update as the webhook, and the transaction gets `Resolution = Applied`, `OrderId`, `ResolvedByUserId`, `ResolvedAt`.

`200 OK` — updated `PaymentTransactionDto`.

Errors: `404 NOT_FOUND` (transaction or order), `409 TRANSACTION_ALREADY_RESOLVED`, `409 ORDER_NOT_PAYABLE`, `409 TRANSACTION_CODE_USED`.

#### `PATCH /api/admin/payment-transactions/{id}/resolution`

```json
{ "resolution": "Refunded", "note": "Order DS2609308H2M expired; refunded to sender on 2026-10-02" }
```

`resolution` is `Refunded` or `Ignored` (`Applied` only through `/apply`); `note` is required, max 500. A resolution is final.

`200 OK` — updated `PaymentTransactionDto`.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 TRANSACTION_ALREADY_RESOLVED`.

### 6.15 Admin — Vouchers (`Admin/VouchersController`, Admin only)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/api/admin/vouchers` | Admin | UC-58 *(added)* | 200 |
| GET | `/api/admin/vouchers/{id}` | Admin | UC-58 *(added)* | 200 |
| POST | `/api/admin/vouchers` | Admin | UC-58 | 201 |
| PUT | `/api/admin/vouchers/{id}` | Admin | UC-58 | 200 |
| PATCH | `/api/admin/vouchers/{id}/active` | Admin | UC-59 | 204 |
| GET | `/api/admin/vouchers/{id}/usage` | Admin | UC-60 | 200 |

#### `GET /api/admin/vouchers`

Query: `page`, `pageSize`, `search` (code or name), `isActive`, `state` (`Upcoming`, `Running`, `Expired`). `200 OK` — `PagedResult<VoucherDto>`.

#### `POST`, `PUT`

```json
{
  "code": "DAISY10",
  "name": "10% off",
  "discountType": "Percent",
  "discountValue": 10,
  "maxDiscountAmount": 50000,
  "minOrderAmount": 200000,
  "usageLimit": 100,
  "startAt": "2026-10-01T00:00:00+07:00",
  "endAt": "2026-10-31T23:59:59+07:00",
  "isActive": true
}
```

Rules (BR-25): `code` uppercase letters and digits, 3–50, unique; `discountValue > 0` and `<= 100` for `Percent`; `maxDiscountAmount` only meaningful for `Percent`, `>= 0`; `minOrderAmount >= 0`; `usageLimit` null or `>= 1`; `startAt < endAt`. On update, `usageLimit` cannot go below `usedCount`, and `code` and `discountType` cannot change once `usedCount > 0`.

`VoucherDto`: the fields above plus `id`, `usedCount`, `state`.

Errors: `400 VALIDATION_FAILED`, `404 NOT_FOUND`, `409 VOUCHER_CODE_EXISTS`.

#### `PATCH /api/admin/vouchers/{id}/active`

`{ "isActive": false }`. Inactive vouchers cannot be applied to new orders; existing orders are unaffected.

#### `GET /api/admin/vouchers/{id}/usage`

Query: `page`, `pageSize`.

```json
{
  "voucher": { "id": 7, "code": "DAISY10", "usedCount": 12, "usageLimit": 100, "...": "VoucherDto" },
  "totalDiscountAmount": 480000,
  "orders": {
    "items": [
      { "orderId": 2001, "orderCode": "DS2610014K7Q", "customerName": "Nguyen Van A",
        "discountAmount": 29800, "totalAmount": 298200, "orderStatus": "Pending", "createdAt": "2026-10-01T14:30:00+07:00" }
    ],
    "page": 1, "pageSize": 20, "totalCount": 13, "totalPages": 1
  }
}
```

Canceled orders are listed (with their status) but not counted in `usedCount` or `totalDiscountAmount` (BR-36).

### 6.16 Admin — Reviews (`Admin/ReviewsController`)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/odata/reviews` | Staff | UC-61 | 200 (§7.3) |
| PATCH | `/api/admin/reviews/{id}/visibility` | Staff | UC-62 | 204 |

`PATCH`: `{ "isVisible": false }`. Recalculates the product's `AverageRating` and `ReviewCount` (BR-38). Errors: `404 NOT_FOUND`.

### 6.17 Admin — Users (`Admin/UsersController`, Admin only)

| Method | Path | Policy | UC | Success |
|---|---|---|---|---|
| GET | `/odata/users` | Admin | UC-63 | 200 (§7.4) |
| GET | `/api/admin/users/{id}` | Admin | UC-64 | 200 |
| PATCH | `/api/admin/users/{id}/status` | Admin | UC-65 | 204 |
| PATCH | `/api/admin/users/{id}/role` | Admin | UC-66 | 204 |

#### `GET /api/admin/users/{id}`

Query: `ordersPage`, `ordersPageSize`.

```json
{
  "id": 12,
  "fullName": "Nguyen Van A",
  "email": "a@example.com",
  "phone": "0901234567",
  "gender": "Male",
  "dateOfBirth": "2000-05-20",
  "avatarUrl": null,
  "role": "Customer",
  "status": "Active",
  "createdAt": "2026-09-01T09:00:00+07:00",
  "orderCount": 5,
  "completedSpent": 1250000,
  "orders": { "items": [ { "...": "OrderSummaryDto" } ], "page": 1, "pageSize": 10, "totalCount": 5, "totalPages": 1 }
}
```

No password hash or tokens (BR-02).

#### `PATCH /api/admin/users/{id}/status`

`{ "status": "Locked" }`. Locking clears the refresh token (BR-03). An Admin cannot lock themselves.

Errors: `400 CANNOT_MODIFY_SELF`, `404 NOT_FOUND`.

#### `PATCH /api/admin/users/{id}/role`

`{ "role": "Staff" }`. Applies at the user's next token refresh. An Admin cannot change their own role (prevents removing the last Admin by accident).

Errors: `400 CANNOT_MODIFY_SELF`, `404 NOT_FOUND`.

### 6.18 Admin — Dashboard (`Admin/DashboardController`, Admin only)

| Method | Path | UC | Response |
|---|---|---|---|
| GET | `/api/admin/dashboard/revenue` | UC-67 | `RevenuePointDto[]` |
| GET | `/api/admin/dashboard/orders-by-status` | UC-68 | `OrderStatusCountDto[]` |
| GET | `/api/admin/dashboard/top-products` | UC-69 | `TopProductDto[]` |
| GET | `/api/admin/dashboard/new-customers` | UC-70 | `NewCustomerPointDto[]` |

Common query parameters:

| Parameter | Default | Rules |
|---|---|---|
| `from` | 30 days before today | `yyyy-MM-dd`, inclusive |
| `to` | today | `yyyy-MM-dd`, inclusive, `from <= to` |
| `groupBy` | `day` | `day` (range ≤ 366 days) or `month` (range ≤ 5 years); revenue and new customers only |
| `top` | `10` | `1..50`; top products only |

Definitions follow BR-40. Points with no data are returned with value `0`, so charts have no gaps.

```json
// revenue
[ { "period": "2026-10-01", "revenue": 4520000, "orderCount": 12 } ]

// orders-by-status (every status is present)
[ { "status": "Pending", "count": 4 }, { "status": "Confirmed", "count": 2 }, { "status": "Shipping", "count": 3 },
  { "status": "Completed", "count": 40 }, { "status": "Canceled", "count": 1 } ]

// top-products
[ { "productId": 101, "productName": "Basic T-Shirt", "thumbnailUrl": "/uploads/products/101/1.webp", "quantitySold": 120, "revenue": 17880000 } ]

// new-customers
[ { "period": "2026-10", "count": 35 } ]
```

`period` is `yyyy-MM-dd` for `day` and `yyyy-MM` for `month`.

---

## 7. OData Endpoints

Registered with `Microsoft.AspNetCore.OData` under the route prefix `/odata`. Each entity set is backed by an Application query that returns `IQueryable<TDto>` (a LINQ projection, ARCHITECTURE §7), so OData never exposes entities and the query is translated to SQL by EF Core.

Common settings:

| Setting | Value |
|---|---|
| Allowed options | `$filter`, `$orderby`, `$top`, `$skip`, `$count`, `$select` |
| `$expand` | Not allowed (lambda `any()` on `variants` is allowed in `$filter`) |
| `MaxTop` | 100 |
| Default page size | 20 when `$top` is missing (server-driven paging with `@odata.nextLink`) |
| Max filter depth / node count | 5 / 100 (protects against expensive queries) |
| Functions allowed | `contains`, `startswith`, `tolower`, comparison and logical operators, `any` |
| Errors | Invalid queries return `400` in the standard error body (§4.1) |

Response shape:

```json
{
  "@odata.context": "https://api.daisy.local/odata/$metadata#Products",
  "@odata.count": 57,
  "value": [ { "...": "ProductListDto" } ]
}
```

### 7.1 `GET /odata/products` — Public

Entity: `ProductListDto` (§5.2) plus a `variants` collection of `{ size, color }` for filtering. Only active products in active categories, with at least one active variant (BR-10). `effectivePrice` and `isOnSale` are computed in the projection using the request time (BR-14).

> The EDM is built with `EnableLowerCamelCase()`, so query property names are camelCase and match the JSON (`effectivePrice`, not `EffectivePrice`). The sequence diagrams write them in PascalCase; this document is the reference.

| Use case | Query |
|---|---|
| UC-14 Listing by category | `?$filter=categoryId eq 5&$top=20&$skip=0&$count=true` |
| UC-16 Search | `?$filter=contains(tolower(name),'ao thun') or contains(tolower(brand),'ao thun')&$count=true` |
| UC-17 Filter | `?$filter=effectivePrice ge 100000 and effectivePrice le 300000 and brand eq 'Daisy' and genderTarget eq 'Unisex' and variants/any(v: v/size eq 'M' and v/color eq 'Black')` |
| UC-18 Sort | `$orderby=createdAt desc` · `effectivePrice asc` · `effectivePrice desc` · `soldCount desc` · `averageRating desc` |
| UC-19 Featured | `?$filter=isFeatured eq true&$top=8` |
| UC-20 New arrivals | `?$orderby=createdAt desc&$top=8` |
| UC-21 Best sellers | `?$orderby=soldCount desc&$top=8` |
| UC-22 Flash sale | `?$filter=isOnSale eq true&$orderby=saleEndAt` |

Category filtering includes sub-categories: MVC sends `categoryId in (1,5,6,7)` using the ids from `GET /api/categories`.

### 7.2 `GET /odata/orders` — Staff

Entity `OrderAdminListDto`:

| Property | Type |
|---|---|
| `id`, `orderCode` | int, string |
| `customerId`, `customerName`, `customerEmail`, `receiverPhone` | int, string, string, string |
| `orderStatus`, `paymentMethod`, `paymentStatus` | enums |
| `totalAmount` | decimal |
| `itemCount` | int |
| `createdAt`, `paidAt` | timestamps |

Example (UC-53):

```http
GET /odata/orders?$filter=orderStatus eq 'Pending' and paymentStatus eq 'Unpaid'
    and createdAt ge 2026-10-01T00:00:00+07:00 and contains(orderCode,'DS2610')
    &$orderby=createdAt desc&$top=20&$skip=0&$count=true
```

### 7.3 `GET /odata/reviews` — Staff

Entity `ReviewAdminDto`: `id`, `productId`, `productName`, `userId`, `userFullName`, `orderItemId`, `rating`, `comment`, `isVisible`, `createdAt`. Includes hidden reviews.

```http
GET /odata/reviews?$filter=productId eq 101 and rating le 2 and isVisible eq true&$orderby=createdAt desc&$top=20&$count=true
```

### 7.4 `GET /odata/users` — Admin

Entity `UserListDto`: `id`, `fullName`, `email`, `phone`, `role`, `status`, `createdAt`. Never contains password hash or tokens (BR-02).

```http
GET /odata/users?$filter=(contains(tolower(fullName),'an') or contains(tolower(email),'an')) and role eq 'Customer' and status eq 'Active'&$orderby=createdAt desc&$top=20&$count=true
```

---

## 8. Endpoint Index

| # | Method | Path | Policy | UC |
|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | Public | UC-01 |
| 2 | POST | `/api/auth/login` | Public | UC-02, UC-31 |
| 3 | POST | `/api/auth/logout` | Authenticated | UC-03 |
| 4 | POST | `/api/auth/refresh` | Public | UC-04 |
| 5 | POST | `/api/auth/forgot-password` | Public | UC-05 |
| 6 | POST | `/api/auth/reset-password` | Public | UC-06 |
| 7 | POST | `/api/auth/change-password` | Authenticated | UC-07 |
| 8 | GET | `/api/profile` | Customer | UC-08 |
| 9 | PUT | `/api/profile` | Customer | UC-09 |
| 10 | GET | `/api/profile/addresses` | Customer | UC-10 |
| 11 | POST | `/api/profile/addresses` | Customer | UC-11 |
| 12 | PUT | `/api/profile/addresses/{id}` | Customer | UC-11 |
| 13 | DELETE | `/api/profile/addresses/{id}` | Customer | UC-11 |
| 14 | PUT | `/api/profile/addresses/{id}/default` | Customer | UC-12 |
| 15 | GET | `/api/categories` | Public | UC-13 |
| 16 | GET | `/api/products/{slug}` | Public | UC-15 |
| 17 | GET | `/api/products/{productId}/reviews` | Public | UC-42 |
| 18 | GET | `/odata/products` | Public | UC-14, UC-16 – UC-22 |
| 19 | GET | `/api/wishlist` | Customer | UC-25 |
| 20 | POST | `/api/wishlist/{productId}` | Customer | UC-23 |
| 21 | DELETE | `/api/wishlist/{productId}` | Customer | UC-24 |
| 22 | GET | `/api/cart` | CartOwner | UC-27 |
| 23 | POST | `/api/cart/items` | CartOwner | UC-26 |
| 24 | PUT | `/api/cart/items/{id}` | CartOwner | UC-28 |
| 25 | DELETE | `/api/cart/items/{id}` | CartOwner | UC-29 |
| 26 | PUT | `/api/cart/selection` | CartOwner | UC-30 |
| 27 | GET | `/api/checkout` | Customer | UC-32 |
| 28 | POST | `/api/checkout/voucher` | Customer | UC-33 |
| 29 | POST | `/api/checkout` | Customer | UC-34, UC-35 |
| 30 | GET | `/api/orders` | Customer | UC-37 |
| 31 | GET | `/api/orders/{id}` | Customer | UC-35, UC-38, UC-39 |
| 32 | GET | `/api/orders/{id}/payment-qr` | Customer | UC-35, UC-38 |
| 33 | POST | `/api/orders/{id}/cancel` | Customer | UC-40 |
| 34 | POST | `/api/payments/webhook` | Webhook | UC-36 |
| 35 | POST | `/api/dev/payments/simulate-transfer` *(added, Development only)* | Authenticated | UC-35 |
| 36 | POST | `/api/reviews` | Customer | UC-41 |
| 37 | GET | `/api/admin/categories` *(added)* | Staff | UC-43 |
| 38 | GET | `/api/admin/categories/{id}` *(added)* | Staff | UC-43 |
| 39 | POST | `/api/admin/categories` | Staff | UC-43 |
| 40 | PUT | `/api/admin/categories/{id}` | Staff | UC-43 |
| 41 | PATCH | `/api/admin/categories/{id}/visibility` | Staff | UC-44 |
| 42 | GET | `/api/admin/products` *(added)* | Staff | UC-45 |
| 43 | GET | `/api/admin/products/{id}` *(added)* | Staff | UC-45 |
| 44 | POST | `/api/admin/products` | Staff | UC-45 |
| 45 | PUT | `/api/admin/products/{id}` | Staff | UC-45 |
| 46 | PATCH | `/api/admin/products/{id}/visibility` | Staff | UC-46 |
| 47 | POST | `/api/admin/products/{id}/variants` | Staff | UC-47 |
| 48 | PUT | `/api/admin/variants/{id}` | Staff | UC-47 |
| 49 | PATCH | `/api/admin/variants/{id}/visibility` | Staff | UC-47 |
| 50 | POST | `/api/admin/products/{id}/images` | Staff | UC-48 |
| 51 | PATCH | `/api/admin/images/{id}/thumbnail` | Staff | UC-48 |
| 52 | PUT | `/api/admin/products/{id}/images/order` | Staff | UC-48 |
| 53 | PATCH | `/api/admin/images/{id}/variant` | Staff | UC-48 |
| 54 | DELETE | `/api/admin/images/{id}` | Staff | UC-48 |
| 55 | PATCH | `/api/admin/products/{id}/featured` | Staff | UC-49 |
| 56 | PUT | `/api/admin/products/{id}/sale` | Admin | UC-50 |
| 57 | DELETE | `/api/admin/products/{id}/sale` | Admin | UC-50 |
| 58 | PUT | `/api/admin/variants/{id}/stock` | Staff | UC-51 |
| 59 | GET | `/api/admin/inventory/low-stock` | Staff | UC-52 |
| 60 | GET | `/odata/orders` | Staff | UC-53 |
| 61 | GET | `/api/admin/orders/{id}` | Staff | UC-54 |
| 62 | PATCH | `/api/admin/orders/{id}/status` | Staff | UC-55 |
| 63 | POST | `/api/admin/orders/{id}/cancel` | Staff | UC-56 |
| 64 | PATCH | `/api/admin/orders/{id}/payment` | Staff | UC-57 |
| 65 | GET | `/api/admin/payment-transactions` | Staff | UC-71 |
| 66 | POST | `/api/admin/payment-transactions/{id}/apply` | Staff | UC-71 |
| 67 | PATCH | `/api/admin/payment-transactions/{id}/resolution` | Staff | UC-71 |
| 68 | GET | `/api/admin/vouchers` *(added)* | Admin | UC-58 |
| 69 | GET | `/api/admin/vouchers/{id}` *(added)* | Admin | UC-58 |
| 70 | POST | `/api/admin/vouchers` | Admin | UC-58 |
| 71 | PUT | `/api/admin/vouchers/{id}` | Admin | UC-58 |
| 72 | PATCH | `/api/admin/vouchers/{id}/active` | Admin | UC-59 |
| 73 | GET | `/api/admin/vouchers/{id}/usage` | Admin | UC-60 |
| 74 | GET | `/odata/reviews` | Staff | UC-61 |
| 75 | PATCH | `/api/admin/reviews/{id}/visibility` | Staff | UC-62 |
| 76 | GET | `/odata/users` | Admin | UC-63 |
| 77 | GET | `/api/admin/users/{id}` | Admin | UC-64 |
| 78 | PATCH | `/api/admin/users/{id}/status` | Admin | UC-65 |
| 79 | PATCH | `/api/admin/users/{id}/role` | Admin | UC-66 |
| 80 | GET | `/api/admin/dashboard/revenue` | Admin | UC-67 |
| 81 | GET | `/api/admin/dashboard/orders-by-status` | Admin | UC-68 |
| 82 | GET | `/api/admin/dashboard/top-products` | Admin | UC-69 |
| 83 | GET | `/api/admin/dashboard/new-customers` | Admin | UC-70 |

Endpoints marked *(added)* are design additions beyond features.docx and the first set of sequence diagrams:

- Admin lists and edit forms that UC-43, UC-45 and UC-58 need (the storefront endpoints hide inactive items, so they cannot be reused).
- A single-category read for the admin edit form, and a Development-only transfer simulation for the mock VietQR provider.

UC-72 (CHK-08, expiry of unpaid VietQR orders) has no endpoint; it runs as a background job (`UnpaidOrderExpiryJob`).

---

## 9. Controller Map

| Controller | Route prefix | Application service |
|---|---|---|
| `AuthController` | `/api/auth` | `Features/Auth/AuthService` |
| `ProfileController` | `/api/profile` | `Features/Profile/ProfileService` |
| `CategoriesController` | `/api/categories` | `Features/Categories/CategoryService` |
| `ProductsController` | `/api/products` | `Features/Products/ProductService`, `Features/Reviews/ReviewService` |
| `WishlistController` | `/api/wishlist` | `Features/Wishlist/WishlistService` |
| `CartController` | `/api/cart` | `Features/Cart/CartService` |
| `CheckoutController` | `/api/checkout` | `Features/Checkout/CheckoutService` |
| `OrdersController` | `/api/orders` | `Features/Orders/OrderService`, `Features/Payments/PaymentService` |
| `PaymentsController` | `/api/payments` | `Features/Payments/PaymentService` |
| `DevPaymentsController` (Development only) | `/api/dev/payments` | `PaymentService` |
| `ReviewsController` | `/api/reviews` | `Features/Reviews/ReviewService` |
| `Admin/CategoriesController` | `/api/admin/categories` | `CategoryService` |
| `Admin/ProductsController` | `/api/admin/products`, `/api/admin/variants`, `/api/admin/images` | `ProductService` |
| `Admin/InventoryController` | `/api/admin/inventory`, `/api/admin/variants/{id}/stock` | `Features/Inventory/InventoryService` |
| `Admin/OrdersController` | `/api/admin/orders` | `OrderService`, `PaymentService` |
| `Admin/PaymentTransactionsController` | `/api/admin/payment-transactions` | `PaymentService` |
| `Admin/VouchersController` | `/api/admin/vouchers` | `Features/Vouchers/VoucherService` |
| `Admin/ReviewsController` | `/api/admin/reviews` | `ReviewService` |
| `Admin/UsersController` | `/api/admin/users` | `Features/Users/UserService` |
| `Admin/DashboardController` | `/api/admin/dashboard` | `Features/Dashboard/DashboardService` |
| `OData/ProductsController` | `/odata/products` | `ProductService` (query) |
| `OData/OrdersController` | `/odata/orders` | `OrderService` (query) |
| `OData/ReviewsController` | `/odata/reviews` | `ReviewService` (query) |
| `OData/UsersController` | `/odata/users` | `UserService` (query) |

Two classes named `ProductsController` (REST and OData) live in different namespaces (`Daisy.Api.Controllers` and `Daisy.Api.OData`).

Background job (no controller):

| Job | Schedule | Application service |
|---|---|---|
| `Daisy.Infrastructure/BackgroundJobs/UnpaidOrderExpiryJob` | Every minute | `Features/Orders/OrderExpiryService.CancelExpiredOrdersAsync` (UC-72) |

---

## 10. Security Notes

- **HTTPS** everywhere; HSTS in production.
- **CORS**: not enabled. Only `Daisy.Mvc` (server-to-server) and the bank webhook service call the API; the browser never calls it directly.
- **Rate limiting** (ASP.NET Core `RateLimiter`, fixed window per IP):
  - `login`, `refresh`: 10 requests / minute
  - `forgot-password`, `reset-password`: 5 requests / 15 minutes
  - `checkout/voucher`: 20 requests / minute (stops voucher code guessing)
  - Exceeding the limit returns `429 Too Many Requests` with `Retry-After`.
- **Uploads**: content type checked by file signature, not only by extension; files renamed to a generated name; stored outside the API's code folder.
- **Secrets** (`Jwt:SigningKey`, `VietQr:WebhookApiKey`, SMTP password, connection string) come from user secrets / environment variables, never from committed `appsettings.json`.
- **Logging**: never log passwords, tokens, reset tokens or the webhook key.

---

## 11. Configuration

| Section | Keys |
|---|---|
| `Jwt` | `Issuer`, `Audience`, `SigningKey`, `AccessTokenMinutes` (15), `RefreshTokenDays` (7) |
| `PasswordReset` | `TokenMinutes` (30), `ResetUrl` (MVC page) |
| `Checkout` | `ShippingFee` (flat, e.g. 30000), `FreeShippingThreshold` (optional) |
| `VietQr` | `Provider` (`SePay` or `Mock`), `BankBin`, `BankName`, `AccountNumber`, `AccountName`, `QrTemplate`, `WebhookApiKey`, `PaymentTimeoutMinutes` (30), `ExpiryGraceMinutes` (5) |
| `Storage` | `RootPath` (default `../storage`), `PublicBasePath` (`/uploads`), `MaxImageBytes` (5 MB), `MaxAvatarBytes` (2 MB), `MaxFilesPerUpload` (10) |
| `Smtp` | `Host`, `Port`, `UserName`, `Password`, `FromAddress`, `FromName`; an empty `Host` writes emails to the log instead of sending them |
| `RateLimits` | `Auth` (10/min), `Password` (5/15 min), `Voucher` (20/min) |
| `BackgroundJobs` | `Enabled` (true); turns `UnpaidOrderExpiryJob` off, e.g. in tests |
| `ConnectionStrings` | `Daisy` (SQL Server) |

Development defaults: `appsettings.Development.json` sets `VietQr:Provider = Mock` with a demo account; `Jwt:SigningKey` is set with `dotnet user-secrets` on each machine. The MVC client reads `Api:BaseUrl` (default `https://localhost:7001`).

---

## 12. Open Questions and Assumptions

Decisions this document makes where the source documents are silent. Confirm or change them before implementation.

| # | Topic | Assumption in this document |
|---|---|---|
| 1 | Shipping fee | Flat fee from configuration (`Checkout:ShippingFee`), optional free-shipping threshold; no carrier integration. features.docx does not define the formula. |
| 2 | Token lifetimes | Access 15 min, refresh 7 days, reset token 30 min. |
| 3 | `PaymentMethod` value | Database value `Online` means VietQR; the API keeps `Online` to match the schema. Renaming to `VietQR` would need a schema change. |
| 4 | VietQR payment deadline | 30 minutes + 5 minutes grace (BR-41); both configurable. |
| 5 | `Payment status Failed` | BR-34 allows `Unpaid → Failed`, but no use case sets it; the API exposes no action for it. Expired VietQR orders keep `PaymentStatus = Unpaid` and become `OrderStatus = Canceled`. |
| 6 | Order code format | `DS` + `yyMMdd` + 4 random uppercase alphanumerics (e.g. `DS2610014K7Q`), retried on collision (BR-29). |
| 7 | `Gender` / `GenderTarget` values | Proposed enums in §5.1; the database columns are free text. |
| 8 | Profile for Staff / Admin | Profile endpoints are restricted to `Customer` as in UC-08; Staff and Admin only change their password. Relax to `Authenticated` if staff need a profile page. |
| 9 | Expiry notification | The customer is not emailed when an order expires; they see it in Order Detail. An email can be added later through `IEmailService`. |

Resolved (2026-10-01):

- `OrderStatus` uses `Canceled` everywhere (ARCHITECTURE aligned with `CK_Orders_OrderStatus`).
- Unpaid VietQR orders expire at `Orders.PaymentExpiresAt` and are canceled by `UnpaidOrderExpiryJob` (BR-41, UC-72).
- Every verified transfer is stored in `PaymentTransactions`; unmatched ones are reconciled by staff (BR-42, UC-71, §6.14).
- `Orders.TransactionCode` has the filtered unique index `UX_Orders_TransactionCode`.

Resolved during implementation (2026-10-01):

- Webhook provider: **SePay** (`Authorization: Apikey`), with a mock provider for development (§6.8).
- Refresh and password reset tokens are stored as SHA-256 hashes in `Users.RefreshToken` and `Users.ResetPasswordToken`.
- Six foreign-key indexes were added to `DaisyDB.sql` so the script and the EF Core migration create identical databases.
