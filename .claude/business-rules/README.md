# Daisy Business Rules

Business rules derived from `docs/docx/features.docx` and `infra/database/DaisyDB.sql`.

- Each rule is in its own file, `BR-XX.md`.
- **Enforced by** in each file says whether the database, the application, or both enforce the rule.
- **Notes** list known gaps between the rule and the current database schema.
- Use cases are in [`../use-cases/`](../use-cases/README.md).

## Index

### Account & Access

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-01](BR-01.md) | Unique Email | One account per email address. | [UC-01](../use-cases/UC-01.md) |
| [BR-02](BR-02.md) | Password Hashing | Passwords are stored only as hashes. | [UC-01](../use-cases/UC-01.md), [UC-02](../use-cases/UC-02.md), [UC-06](../use-cases/UC-06.md), [UC-07](../use-cases/UC-07.md), [UC-08](../use-cases/UC-08.md), [UC-63](../use-cases/UC-63.md) |
| [BR-03](BR-03.md) | Account Lock | Locked accounts cannot sign in or refresh tokens. | [UC-02](../use-cases/UC-02.md), [UC-04](../use-cases/UC-04.md), [UC-65](../use-cases/UC-65.md) |
| [BR-04](BR-04.md) | Access and Refresh Tokens | Short-lived JWT access token plus one refresh token per account. | [UC-02](../use-cases/UC-02.md), [UC-03](../use-cases/UC-03.md), [UC-04](../use-cases/UC-04.md), [UC-66](../use-cases/UC-66.md) |
| [BR-05](BR-05.md) | Password Reset Token | Reset tokens are random, time-limited and single-use. | [UC-05](../use-cases/UC-05.md), [UC-06](../use-cases/UC-06.md) |
| [BR-06](BR-06.md) | Password Change Verification | Changing a password requires the current password. | [UC-07](../use-cases/UC-07.md) |
| [BR-07](BR-07.md) | Role-Based Permissions | Customer / Staff / Admin permissions per feature group. | [UC-01](../use-cases/UC-01.md), [UC-43](../use-cases/UC-43.md), [UC-44](../use-cases/UC-44.md), [UC-45](../use-cases/UC-45.md), [UC-46](../use-cases/UC-46.md), [UC-47](../use-cases/UC-47.md), [UC-48](../use-cases/UC-48.md), [UC-49](../use-cases/UC-49.md), [UC-50](../use-cases/UC-50.md), [UC-51](../use-cases/UC-51.md), [UC-52](../use-cases/UC-52.md), [UC-53](../use-cases/UC-53.md), [UC-54](../use-cases/UC-54.md), [UC-55](../use-cases/UC-55.md), [UC-56](../use-cases/UC-56.md), [UC-57](../use-cases/UC-57.md), [UC-58](../use-cases/UC-58.md), [UC-59](../use-cases/UC-59.md), [UC-60](../use-cases/UC-60.md), [UC-61](../use-cases/UC-61.md), [UC-62](../use-cases/UC-62.md), [UC-63](../use-cases/UC-63.md), [UC-64](../use-cases/UC-64.md), [UC-65](../use-cases/UC-65.md), [UC-66](../use-cases/UC-66.md), [UC-67](../use-cases/UC-67.md), [UC-68](../use-cases/UC-68.md), [UC-69](../use-cases/UC-69.md), [UC-70](../use-cases/UC-70.md), [UC-71](../use-cases/UC-71.md) |
| [BR-08](BR-08.md) | Data Ownership | Customers can only access their own data. | [UC-08](../use-cases/UC-08.md), [UC-09](../use-cases/UC-09.md), [UC-10](../use-cases/UC-10.md), [UC-11](../use-cases/UC-11.md), [UC-12](../use-cases/UC-12.md), [UC-23](../use-cases/UC-23.md), [UC-24](../use-cases/UC-24.md), [UC-25](../use-cases/UC-25.md), [UC-32](../use-cases/UC-32.md), [UC-37](../use-cases/UC-37.md), [UC-38](../use-cases/UC-38.md), [UC-39](../use-cases/UC-39.md), [UC-40](../use-cases/UC-40.md), [UC-41](../use-cases/UC-41.md) |

### Profile & Address Book

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-09](BR-09.md) | Single Default Address | At most one default address per customer. | [UC-10](../use-cases/UC-10.md), [UC-11](../use-cases/UC-11.md), [UC-12](../use-cases/UC-12.md), [UC-32](../use-cases/UC-32.md) |

### Product Catalog

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-10](BR-10.md) | Storefront Visibility | Only active categories, products and variants are shown to customers. | [UC-13](../use-cases/UC-13.md), [UC-14](../use-cases/UC-14.md), [UC-15](../use-cases/UC-15.md), [UC-16](../use-cases/UC-16.md), [UC-17](../use-cases/UC-17.md), [UC-19](../use-cases/UC-19.md), [UC-20](../use-cases/UC-20.md), [UC-21](../use-cases/UC-21.md), [UC-22](../use-cases/UC-22.md), [UC-23](../use-cases/UC-23.md), [UC-25](../use-cases/UC-25.md), [UC-26](../use-cases/UC-26.md), [UC-27](../use-cases/UC-27.md), [UC-44](../use-cases/UC-44.md), [UC-46](../use-cases/UC-46.md) |
| [BR-11](BR-11.md) | Hide Instead of Delete | Catalog items are hidden, not deleted, to keep order history. | [UC-44](../use-cases/UC-44.md), [UC-46](../use-cases/UC-46.md), [UC-47](../use-cases/UC-47.md), [UC-59](../use-cases/UC-59.md) |
| [BR-12](BR-12.md) | Unique Catalog Identifiers | Unique slugs, SKUs and size/color options. | [UC-43](../use-cases/UC-43.md), [UC-45](../use-cases/UC-45.md), [UC-47](../use-cases/UC-47.md) |
| [BR-13](BR-13.md) | Category Hierarchy | Categories form a parent/child tree ordered by SortOrder. | [UC-13](../use-cases/UC-13.md), [UC-43](../use-cases/UC-43.md) |
| [BR-14](BR-14.md) | Effective Price and Flash Sale | Price shown and charged = active sale price or base price, plus variant extra price. | [UC-14](../use-cases/UC-14.md), [UC-15](../use-cases/UC-15.md), [UC-17](../use-cases/UC-17.md), [UC-18](../use-cases/UC-18.md), [UC-22](../use-cases/UC-22.md), [UC-25](../use-cases/UC-25.md), [UC-45](../use-cases/UC-45.md), [UC-50](../use-cases/UC-50.md) |
| [BR-15](BR-15.md) | Product Collections | Definitions of Featured, New Arrivals, Best Sellers and sort orders. | [UC-18](../use-cases/UC-18.md), [UC-19](../use-cases/UC-19.md), [UC-20](../use-cases/UC-20.md), [UC-21](../use-cases/UC-21.md), [UC-22](../use-cases/UC-22.md), [UC-49](../use-cases/UC-49.md) |
| [BR-16](BR-16.md) | Product Images | One thumbnail per product; images can be linked to a color. | [UC-15](../use-cases/UC-15.md), [UC-48](../use-cases/UC-48.md) |

### Inventory

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-17](BR-17.md) | Stock and Low-Stock Threshold | Stock is per variant, never negative; low stock when stock <= threshold. | [UC-15](../use-cases/UC-15.md), [UC-26](../use-cases/UC-26.md), [UC-31](../use-cases/UC-31.md), [UC-47](../use-cases/UC-47.md), [UC-51](../use-cases/UC-51.md), [UC-52](../use-cases/UC-52.md) |

### Wishlist

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-18](BR-18.md) | Wishlist Uniqueness | A product appears at most once in a customer's wishlist. | [UC-23](../use-cases/UC-23.md), [UC-24](../use-cases/UC-24.md) |

### Shopping Cart

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-19](BR-19.md) | Cart Ownership and Lines | A cart belongs to a user or a guest session; one line per variant. | [UC-26](../use-cases/UC-26.md), [UC-27](../use-cases/UC-27.md), [UC-28](../use-cases/UC-28.md), [UC-29](../use-cases/UC-29.md), [UC-30](../use-cases/UC-30.md) |
| [BR-20](BR-20.md) | Cart Quantity Limits | Quantity is at least 1 and no more than available stock. | [UC-26](../use-cases/UC-26.md), [UC-28](../use-cases/UC-28.md), [UC-31](../use-cases/UC-31.md) |
| [BR-21](BR-21.md) | Live Cart Pricing | Cart prices are read live, not stored. | [UC-27](../use-cases/UC-27.md) |
| [BR-22](BR-22.md) | Guest Cart Merge | On login, the guest cart is merged into the user cart. | [UC-02](../use-cases/UC-02.md), [UC-31](../use-cases/UC-31.md) |

### Checkout & Payment

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-23](BR-23.md) | Checkout Eligibility | Only signed-in customers can check out selected, available items. | [UC-30](../use-cases/UC-30.md), [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md) |
| [BR-24](BR-24.md) | Order Total Calculation | Total = Subtotal - Discount + Shipping fee, calculated by the backend. | [UC-33](../use-cases/UC-33.md), [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md) |
| [BR-30](BR-30.md) | Atomic Order Placement | Order creation, stock deduction and counters succeed or fail together. | [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md) |
| [BR-31](BR-31.md) | Payment Methods | COD or VietQR bank transfer. | [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md), [UC-57](../use-cases/UC-57.md) |
| [BR-32](BR-32.md) | VietQR Payment Confirmation | A VietQR order is paid when a matching transfer is received. | [UC-35](../use-cases/UC-35.md), [UC-36](../use-cases/UC-36.md), [UC-38](../use-cases/UC-38.md), [UC-57](../use-cases/UC-57.md), [UC-71](../use-cases/UC-71.md) |
| [BR-33](BR-33.md) | Payment Webhook Idempotency | Repeated or forged webhooks never change an order twice. | [UC-36](../use-cases/UC-36.md), [UC-57](../use-cases/UC-57.md), [UC-71](../use-cases/UC-71.md) |
| [BR-34](BR-34.md) | Payment Status Transitions | Allowed changes of PaymentStatus. | [UC-36](../use-cases/UC-36.md), [UC-57](../use-cases/UC-57.md), [UC-71](../use-cases/UC-71.md) |
| [BR-41](BR-41.md) | VietQR Payment Deadline | Unpaid VietQR orders are canceled automatically after their payment deadline. | [UC-35](../use-cases/UC-35.md), [UC-38](../use-cases/UC-38.md), [UC-55](../use-cases/UC-55.md), [UC-72](../use-cases/UC-72.md) |
| [BR-42](BR-42.md) | Payment Transaction Record | Every verified bank transfer is stored; unmatched ones wait for staff. | [UC-36](../use-cases/UC-36.md), [UC-57](../use-cases/UC-57.md), [UC-71](../use-cases/UC-71.md) |

### Voucher

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-25](BR-25.md) | Voucher Definition | Valid voucher code, type, value, limit and period. | [UC-33](../use-cases/UC-33.md), [UC-58](../use-cases/UC-58.md) |
| [BR-26](BR-26.md) | Voucher Eligibility | When a voucher can be applied to an order. | [UC-33](../use-cases/UC-33.md), [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md), [UC-59](../use-cases/UC-59.md) |
| [BR-27](BR-27.md) | Voucher Discount Calculation | How the discount amount is calculated. | [UC-33](../use-cases/UC-33.md) |

### Orders

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-28](BR-28.md) | Order Snapshot | Address and product details are copied into the order at checkout. | [UC-11](../use-cases/UC-11.md), [UC-32](../use-cases/UC-32.md), [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md), [UC-38](../use-cases/UC-38.md), [UC-45](../use-cases/UC-45.md), [UC-54](../use-cases/UC-54.md) |
| [BR-29](BR-29.md) | Order Code | Unique, short, ASCII order code used as the VietQR transfer content. | [UC-34](../use-cases/UC-34.md), [UC-35](../use-cases/UC-35.md) |
| [BR-35](BR-35.md) | Order Status Lifecycle | Pending -> Confirmed -> Shipping -> Completed, or Canceled. | [UC-34](../use-cases/UC-34.md), [UC-39](../use-cases/UC-39.md), [UC-40](../use-cases/UC-40.md), [UC-55](../use-cases/UC-55.md), [UC-56](../use-cases/UC-56.md), [UC-72](../use-cases/UC-72.md) |
| [BR-36](BR-36.md) | Order Cancellation | Cancel only Pending/Confirmed orders, with a reason; restore stock and counters. | [UC-40](../use-cases/UC-40.md), [UC-56](../use-cases/UC-56.md), [UC-72](../use-cases/UC-72.md) |

### Review & Rating

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-37](BR-37.md) | Review Eligibility | Only buyers of completed orders, once per order item, 1-5 stars. | [UC-41](../use-cases/UC-41.md) |
| [BR-38](BR-38.md) | Rating Aggregation | AverageRating and ReviewCount are recalculated from visible reviews. | [UC-41](../use-cases/UC-41.md), [UC-62](../use-cases/UC-62.md) |
| [BR-39](BR-39.md) | Review Moderation | Hidden reviews are not shown publicly but are kept. | [UC-42](../use-cases/UC-42.md), [UC-62](../use-cases/UC-62.md) |

### Dashboard & Reports

| ID | Name | Summary | Used by |
|---|---|---|---|
| [BR-40](BR-40.md) | Reporting Definitions | How revenue, order, top-selling and new-customer statistics are calculated. | [UC-67](../use-cases/UC-67.md), [UC-68](../use-cases/UC-68.md), [UC-69](../use-cases/UC-69.md), [UC-70](../use-cases/UC-70.md) |
