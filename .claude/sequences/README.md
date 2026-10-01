# Daisy Sequence Diagrams

One PlantUML sequence diagram per use case in [`../use-cases/`](../use-cases/README.md).
`UC-XX.puml` is the sequence diagram of `UC-XX.md`.

## Conventions

- Participants follow [`docs/md/ARCHITECTURE.md`](../../docs/md/ARCHITECTURE.md): `Daisy.Mvc` -> `Daisy.Api` controller -> `Daisy.Application` service -> `DaisyDbContext` (SQL Server), with external dependencies behind `Daisy.Infrastructure` interfaces.
- REST endpoints are under `/api`, admin endpoints under `/api/admin`, OData queries under `/odata`.
- `Daisy.Mvc` sends the access token as `Authorization: Bearer` and, for guests, the cart session as the `X-Cart-Session` header.
- Errors are returned by `ExceptionMiddleware`: 400 validation, 401 unauthenticated, 403 forbidden, 404 not found / not owned, 409 conflict.
- `(BR-XX)` in a message points to the business rule in [`../business-rules/`](../business-rules/README.md).
- Render with any PlantUML tool, e.g. the VS Code PlantUML extension or `plantuml -tsvg *.puml`.

## Index

### Authentication

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-01.puml](UC-01.puml) | [UC-01](../use-cases/UC-01.md) Register | AUTH-01 | Guest | `POST /api/auth/register` |
| [UC-02.puml](UC-02.puml) | [UC-02](../use-cases/UC-02.md) Login | AUTH-02 | Guest | `POST /api/auth/login` |
| [UC-03.puml](UC-03.puml) | [UC-03](../use-cases/UC-03.md) Logout | AUTH-03 | Customer / Staff / Admin | `POST /api/auth/logout` |
| [UC-04.puml](UC-04.puml) | [UC-04](../use-cases/UC-04.md) Refresh Token | AUTH-04 | Customer / Staff / Admin | `POST /api/auth/refresh` |
| [UC-05.puml](UC-05.puml) | [UC-05](../use-cases/UC-05.md) Forgot Password | AUTH-05 | Guest | `POST /api/auth/forgot-password` |
| [UC-06.puml](UC-06.puml) | [UC-06](../use-cases/UC-06.md) Reset Password | AUTH-06 | Guest | `POST /api/auth/reset-password` |
| [UC-07.puml](UC-07.puml) | [UC-07](../use-cases/UC-07.md) Change Password | AUTH-07 | Customer / Staff / Admin | `POST /api/auth/change-password` |

### Profile & Address Book

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-08.puml](UC-08.puml) | [UC-08](../use-cases/UC-08.md) View Profile | PROF-01 | Customer | `GET /api/profile` |
| [UC-09.puml](UC-09.puml) | [UC-09](../use-cases/UC-09.md) Update Profile | PROF-02 | Customer | `PUT /api/profile` |
| [UC-10.puml](UC-10.puml) | [UC-10](../use-cases/UC-10.md) View Addresses | PROF-03 | Customer | `GET /api/profile/addresses` |
| [UC-11.puml](UC-11.puml) | [UC-11](../use-cases/UC-11.md) Add / Update / Delete Address | PROF-04 | Customer | `POST /api/profile/addresses`<br>`PUT /api/profile/addresses/{id}`<br>`DELETE /api/profile/addresses/{id}` |
| [UC-12.puml](UC-12.puml) | [UC-12](../use-cases/UC-12.md) Set Default Address | PROF-05 | Customer | `PUT /api/profile/addresses/{id}/default` |

### Product Catalog

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-13.puml](UC-13.puml) | [UC-13](../use-cases/UC-13.md) View Categories | CAT-01 | Guest / Customer | `GET /api/categories` |
| [UC-14.puml](UC-14.puml) | [UC-14](../use-cases/UC-14.md) Product Listing | CAT-02 | Guest / Customer | `GET /odata/products` |
| [UC-15.puml](UC-15.puml) | [UC-15](../use-cases/UC-15.md) Product Detail | CAT-03 | Guest / Customer | `GET /api/products/{slug}` |
| [UC-16.puml](UC-16.puml) | [UC-16](../use-cases/UC-16.md) Search Products | CAT-04 | Guest / Customer | `GET /odata/products` |
| [UC-17.puml](UC-17.puml) | [UC-17](../use-cases/UC-17.md) Filter Products | CAT-05 | Guest / Customer | `GET /odata/products` |
| [UC-18.puml](UC-18.puml) | [UC-18](../use-cases/UC-18.md) Sort Products | CAT-06 | Guest / Customer | `GET /odata/products` |
| [UC-19.puml](UC-19.puml) | [UC-19](../use-cases/UC-19.md) Featured Products | CAT-07 | Guest / Customer | `GET /odata/products` |
| [UC-20.puml](UC-20.puml) | [UC-20](../use-cases/UC-20.md) New Arrivals | CAT-08 | Guest / Customer | `GET /odata/products` |
| [UC-21.puml](UC-21.puml) | [UC-21](../use-cases/UC-21.md) Best Sellers | CAT-09 | Guest / Customer | `GET /odata/products` |
| [UC-22.puml](UC-22.puml) | [UC-22](../use-cases/UC-22.md) Flash Sale / Hot Deal | CAT-10 | Guest / Customer | `GET /odata/products` |

### Wishlist

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-23.puml](UC-23.puml) | [UC-23](../use-cases/UC-23.md) Add to Wishlist | WISH-01 | Customer | `POST /api/wishlist/{productId}` |
| [UC-24.puml](UC-24.puml) | [UC-24](../use-cases/UC-24.md) Remove from Wishlist | WISH-02 | Customer | `DELETE /api/wishlist/{productId}` |
| [UC-25.puml](UC-25.puml) | [UC-25](../use-cases/UC-25.md) View Wishlist | WISH-03 | Customer | `GET /api/wishlist` |

### Shopping Cart

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-26.puml](UC-26.puml) | [UC-26](../use-cases/UC-26.md) Add to Cart | CART-01 | Guest / Customer | `POST /api/cart/items` |
| [UC-27.puml](UC-27.puml) | [UC-27](../use-cases/UC-27.md) View Cart | CART-02 | Guest / Customer | `GET /api/cart` |
| [UC-28.puml](UC-28.puml) | [UC-28](../use-cases/UC-28.md) Update Quantity | CART-03 | Guest / Customer | `PUT /api/cart/items/{id}` |
| [UC-29.puml](UC-29.puml) | [UC-29](../use-cases/UC-29.md) Remove Item | CART-04 | Guest / Customer | `DELETE /api/cart/items/{id}` |
| [UC-30.puml](UC-30.puml) | [UC-30](../use-cases/UC-30.md) Select Items for Checkout | CART-05 | Guest / Customer | `PUT /api/cart/selection` |
| [UC-31.puml](UC-31.puml) | [UC-31](../use-cases/UC-31.md) Merge Guest Cart | CART-06 | System | Internal (called by UC-02) |

### Checkout & Payment

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-32.puml](UC-32.puml) | [UC-32](../use-cases/UC-32.md) Select Shipping Address | CHK-01 | Customer | `GET /api/checkout` |
| [UC-33.puml](UC-33.puml) | [UC-33](../use-cases/UC-33.md) Apply Voucher | CHK-02 | Customer | `POST /api/checkout/voucher` |
| [UC-34.puml](UC-34.puml) | [UC-34](../use-cases/UC-34.md) Place Order (COD) | CHK-04 | Customer | `POST /api/checkout` |
| [UC-35.puml](UC-35.puml) | [UC-35](../use-cases/UC-35.md) Pay with VietQR | CHK-05 | Customer | `POST /api/checkout`<br>`GET /api/orders/{id}/payment-qr`<br>`GET /api/orders/{id}` |
| [UC-36.puml](UC-36.puml) | [UC-36](../use-cases/UC-36.md) Receive Payment Webhook | CHK-06 | Bank webhook service | `POST /api/payments/webhook` |
| [UC-72.puml](UC-72.puml) | [UC-72](../use-cases/UC-72.md) Expire Unpaid VietQR Order | — (added) | System | Background job (no endpoint) |

### My Orders

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-37.puml](UC-37.puml) | [UC-37](../use-cases/UC-37.md) Order History | ORD-01 | Customer | `GET /api/orders` |
| [UC-38.puml](UC-38.puml) | [UC-38](../use-cases/UC-38.md) Order Detail | ORD-02 | Customer | `GET /api/orders/{id}`<br>`GET /api/orders/{id}/payment-qr` |
| [UC-39.puml](UC-39.puml) | [UC-39](../use-cases/UC-39.md) Track Order Status | ORD-03 | Customer | `GET /api/orders/{id}` |
| [UC-40.puml](UC-40.puml) | [UC-40](../use-cases/UC-40.md) Cancel Order (Customer) | ORD-04 | Customer | `POST /api/orders/{id}/cancel` |

### Review & Rating

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-41.puml](UC-41.puml) | [UC-41](../use-cases/UC-41.md) Write Review | REV-01 | Customer | `POST /api/reviews` |
| [UC-42.puml](UC-42.puml) | [UC-42](../use-cases/UC-42.md) View Product Reviews | REV-02 | Guest / Customer | `GET /api/products/{id}/reviews` |

### Category Management

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-43.puml](UC-43.puml) | [UC-43](../use-cases/UC-43.md) Create / Update Category | ACAT-01 | Staff / Admin | `POST /api/admin/categories`<br>`PUT /api/admin/categories/{id}` |
| [UC-44.puml](UC-44.puml) | [UC-44](../use-cases/UC-44.md) Show / Hide Category | ACAT-02 | Staff / Admin | `PATCH /api/admin/categories/{id}/visibility` |

### Product Management

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-45.puml](UC-45.puml) | [UC-45](../use-cases/UC-45.md) Create / Update Product | APRD-01 | Staff / Admin | `POST /api/admin/products`<br>`PUT /api/admin/products/{id}` |
| [UC-46.puml](UC-46.puml) | [UC-46](../use-cases/UC-46.md) Show / Hide Product | APRD-02 | Staff / Admin | `PATCH /api/admin/products/{id}/visibility` |
| [UC-47.puml](UC-47.puml) | [UC-47](../use-cases/UC-47.md) Manage Variants | APRD-03 | Staff / Admin | `POST /api/admin/products/{id}/variants`<br>`PUT /api/admin/variants/{id}`<br>`PATCH /api/admin/variants/{id}/visibility` |
| [UC-48.puml](UC-48.puml) | [UC-48](../use-cases/UC-48.md) Manage Images | APRD-04 | Staff / Admin | `POST /api/admin/products/{id}/images`<br>`PATCH /api/admin/images/{id}/thumbnail`<br>`PUT /api/admin/products/{id}/images/order`<br>`PATCH /api/admin/images/{id}/variant`<br>`DELETE /api/admin/images/{id}` |
| [UC-49.puml](UC-49.puml) | [UC-49](../use-cases/UC-49.md) Set Featured | APRD-05 | Staff / Admin | `PATCH /api/admin/products/{id}/featured` |
| [UC-50.puml](UC-50.puml) | [UC-50](../use-cases/UC-50.md) Set Flash Sale | APRD-06 | Admin | `PUT /api/admin/products/{id}/sale`<br>`DELETE /api/admin/products/{id}/sale` |

### Inventory

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-51.puml](UC-51.puml) | [UC-51](../use-cases/UC-51.md) Update Stock | INV-01 | Staff / Admin | `PUT /api/admin/variants/{id}/stock` |
| [UC-52.puml](UC-52.puml) | [UC-52](../use-cases/UC-52.md) Low-stock Warning | INV-02 | Staff / Admin | `GET /api/admin/inventory/low-stock` |

### Order Management

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-53.puml](UC-53.puml) | [UC-53](../use-cases/UC-53.md) View / Filter Orders | AORD-01 | Staff / Admin | `GET /odata/orders` |
| [UC-54.puml](UC-54.puml) | [UC-54](../use-cases/UC-54.md) View Order Detail (Admin) | AORD-02 | Staff / Admin | `GET /api/admin/orders/{id}` |
| [UC-55.puml](UC-55.puml) | [UC-55](../use-cases/UC-55.md) Update Order Status | AORD-03 | Staff / Admin | `PATCH /api/admin/orders/{id}/status` |
| [UC-56.puml](UC-56.puml) | [UC-56](../use-cases/UC-56.md) Cancel Order (Admin) | AORD-04 | Staff / Admin | `POST /api/admin/orders/{id}/cancel` |
| [UC-57.puml](UC-57.puml) | [UC-57](../use-cases/UC-57.md) Update Payment Status | AORD-05 | Staff / Admin | `PATCH /api/admin/orders/{id}/payment` |
| [UC-71.puml](UC-71.puml) | [UC-71](../use-cases/UC-71.md) Reconcile Payment Transactions | — (added) | Staff / Admin | `GET /api/admin/payment-transactions`<br>`POST /api/admin/payment-transactions/{id}/apply`<br>`PATCH /api/admin/payment-transactions/{id}/resolution` |

### Voucher Management

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-58.puml](UC-58.puml) | [UC-58](../use-cases/UC-58.md) Create / Update Voucher | AVOU-01 | Admin | `POST /api/admin/vouchers`<br>`PUT /api/admin/vouchers/{id}` |
| [UC-59.puml](UC-59.puml) | [UC-59](../use-cases/UC-59.md) Activate / Deactivate Voucher | AVOU-02 | Admin | `PATCH /api/admin/vouchers/{id}/active` |
| [UC-60.puml](UC-60.puml) | [UC-60](../use-cases/UC-60.md) View Voucher Usage | AVOU-03 | Admin | `GET /api/admin/vouchers/{id}/usage` |

### Review Moderation

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-61.puml](UC-61.puml) | [UC-61](../use-cases/UC-61.md) View All Reviews | AREV-01 | Staff / Admin | `GET /odata/reviews` |
| [UC-62.puml](UC-62.puml) | [UC-62](../use-cases/UC-62.md) Show / Hide Review | AREV-02 | Staff / Admin | `PATCH /api/admin/reviews/{id}/visibility` |

### User Management

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-63.puml](UC-63.puml) | [UC-63](../use-cases/UC-63.md) View / Search Users | AUSR-01 | Admin | `GET /odata/users` |
| [UC-64.puml](UC-64.puml) | [UC-64](../use-cases/UC-64.md) View User Detail | AUSR-02 | Admin | `GET /api/admin/users/{id}` |
| [UC-65.puml](UC-65.puml) | [UC-65](../use-cases/UC-65.md) Lock / Unlock Account | AUSR-03 | Admin | `PATCH /api/admin/users/{id}/status` |
| [UC-66.puml](UC-66.puml) | [UC-66](../use-cases/UC-66.md) Change Role | AUSR-04 | Admin | `PATCH /api/admin/users/{id}/role` |

### Dashboard & Reports

| Diagram | Use case | Feature | Actor | Endpoints |
|---|---|---|---|---|
| [UC-67.puml](UC-67.puml) | [UC-67](../use-cases/UC-67.md) Revenue Statistics | DASH-01 | Admin | `GET /api/admin/dashboard/revenue` |
| [UC-68.puml](UC-68.puml) | [UC-68](../use-cases/UC-68.md) Order Statistics | DASH-02 | Admin | `GET /api/admin/dashboard/orders-by-status` |
| [UC-69.puml](UC-69.puml) | [UC-69](../use-cases/UC-69.md) Top Selling Products | DASH-03 | Admin | `GET /api/admin/dashboard/top-products` |
| [UC-70.puml](UC-70.puml) | [UC-70](../use-cases/UC-70.md) New Customers | DASH-04 | Admin | `GET /api/admin/dashboard/new-customers` |

## Endpoint Summary

All endpoints used in the diagrams, as a starting point for `docs/md/API_DESIGN.md`.

| Method | Path | Used by |
|---|---|---|
| POST | `/api/admin/categories` | UC-43 |
| PUT | `/api/admin/categories/{id}` | UC-43 |
| PATCH | `/api/admin/categories/{id}/visibility` | UC-44 |
| GET | `/api/admin/dashboard/new-customers` | UC-70 |
| GET | `/api/admin/dashboard/orders-by-status` | UC-68 |
| GET | `/api/admin/dashboard/revenue` | UC-67 |
| GET | `/api/admin/dashboard/top-products` | UC-69 |
| DELETE | `/api/admin/images/{id}` | UC-48 |
| PATCH | `/api/admin/images/{id}/thumbnail` | UC-48 |
| PATCH | `/api/admin/images/{id}/variant` | UC-48 |
| GET | `/api/admin/inventory/low-stock` | UC-52 |
| GET | `/api/admin/orders/{id}` | UC-54 |
| POST | `/api/admin/orders/{id}/cancel` | UC-56 |
| PATCH | `/api/admin/orders/{id}/payment` | UC-57 |
| GET | `/api/admin/payment-transactions` | UC-71 |
| POST | `/api/admin/payment-transactions/{id}/apply` | UC-71 |
| PATCH | `/api/admin/payment-transactions/{id}/resolution` | UC-71 |
| PATCH | `/api/admin/orders/{id}/status` | UC-55 |
| POST | `/api/admin/products` | UC-45 |
| PUT | `/api/admin/products/{id}` | UC-45 |
| PATCH | `/api/admin/products/{id}/featured` | UC-49 |
| POST | `/api/admin/products/{id}/images` | UC-48 |
| PUT | `/api/admin/products/{id}/images/order` | UC-48 |
| PUT | `/api/admin/products/{id}/sale` | UC-50 |
| DELETE | `/api/admin/products/{id}/sale` | UC-50 |
| POST | `/api/admin/products/{id}/variants` | UC-47 |
| PATCH | `/api/admin/products/{id}/visibility` | UC-46 |
| PATCH | `/api/admin/reviews/{id}/visibility` | UC-62 |
| GET | `/api/admin/users/{id}` | UC-64 |
| PATCH | `/api/admin/users/{id}/role` | UC-66 |
| PATCH | `/api/admin/users/{id}/status` | UC-65 |
| PUT | `/api/admin/variants/{id}` | UC-47 |
| PUT | `/api/admin/variants/{id}/stock` | UC-51 |
| PATCH | `/api/admin/variants/{id}/visibility` | UC-47 |
| POST | `/api/admin/vouchers` | UC-58 |
| PUT | `/api/admin/vouchers/{id}` | UC-58 |
| PATCH | `/api/admin/vouchers/{id}/active` | UC-59 |
| GET | `/api/admin/vouchers/{id}/usage` | UC-60 |
| POST | `/api/auth/change-password` | UC-07 |
| POST | `/api/auth/forgot-password` | UC-05 |
| POST | `/api/auth/login` | UC-02 |
| POST | `/api/auth/logout` | UC-03 |
| POST | `/api/auth/refresh` | UC-04 |
| POST | `/api/auth/register` | UC-01 |
| POST | `/api/auth/reset-password` | UC-06 |
| GET | `/api/cart` | UC-27 |
| POST | `/api/cart/items` | UC-26 |
| PUT | `/api/cart/items/{id}` | UC-28 |
| DELETE | `/api/cart/items/{id}` | UC-29 |
| PUT | `/api/cart/selection` | UC-30 |
| GET | `/api/categories` | UC-13 |
| GET | `/api/checkout` | UC-32 |
| POST | `/api/checkout` | UC-34, UC-35 |
| POST | `/api/checkout/voucher` | UC-33 |
| GET | `/api/orders` | UC-37 |
| GET | `/api/orders/{id}` | UC-35, UC-38, UC-39 |
| POST | `/api/orders/{id}/cancel` | UC-40 |
| GET | `/api/orders/{id}/payment-qr` | UC-35, UC-38 |
| POST | `/api/payments/webhook` | UC-36 |
| GET | `/api/products/{id}/reviews` | UC-42 |
| GET | `/api/products/{slug}` | UC-15 |
| GET | `/api/profile` | UC-08 |
| PUT | `/api/profile` | UC-09 |
| GET | `/api/profile/addresses` | UC-10 |
| POST | `/api/profile/addresses` | UC-11 |
| PUT | `/api/profile/addresses/{id}` | UC-11 |
| DELETE | `/api/profile/addresses/{id}` | UC-11 |
| PUT | `/api/profile/addresses/{id}/default` | UC-12 |
| POST | `/api/reviews` | UC-41 |
| GET | `/api/wishlist` | UC-25 |
| POST | `/api/wishlist/{productId}` | UC-23 |
| DELETE | `/api/wishlist/{productId}` | UC-24 |
| GET | `/odata/orders` | UC-53 |
| GET | `/odata/products` | UC-14, UC-16, UC-17, UC-18, UC-19, UC-20, UC-21, UC-22 |
| GET | `/odata/reviews` | UC-61 |
| GET | `/odata/users` | UC-63 |
