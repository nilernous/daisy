# Daisy Use Cases

Use cases derived from `docs/docx/features.docx` (74 features in 16 groups).

- Each user-facing feature maps to one use case, `UC-XX.md`.
- Four features describe system behavior rather than a user goal; they are captured as business rules instead (see below).
- Business rules are in [`../business-rules/`](../business-rules/README.md).
- UC-71 and UC-72 are not in features.docx; they were added with the VietQR payment design (payment deadline and reconciliation of unmatched transfers).

## Actors

| Actor | Description |
|---|---|
| Guest | Visitor who is not signed in: browses products, uses a session cart, registers / logs in. |
| Customer | Signed-in customer: buys, manages orders, reviews, wishlist. |
| Staff | Employee: manages products, inventory, orders and review moderation. |
| Admin | Full access, including users, vouchers, flash sales and the dashboard. |
| System | Automatic server-side processing. |
| Bank webhook service | External service that notifies Daisy of incoming VietQR bank transfers. |

## Index

### Authentication

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-01](UC-01.md) | Register | AUTH-01 | Guest | A guest creates a customer account with full name, email and password. |
| [UC-02](UC-02.md) | Login | AUTH-02 | Guest | A guest signs in with email and password and receives an access token and a refresh token. |
| [UC-03](UC-03.md) | Logout | AUTH-03 | Customer, Staff, Admin | A signed-in user ends their session and the current refresh token is revoked. |
| [UC-04](UC-04.md) | Refresh Token | AUTH-04 | Customer, Staff, Admin | The client obtains a new access token using a valid refresh token when the access token expires. |
| [UC-05](UC-05.md) | Forgot Password | AUTH-05 | Guest | A guest requests an email with a time-limited link to reset their password. |
| [UC-06](UC-06.md) | Reset Password | AUTH-06 | Guest | A guest sets a new password using the token from the reset email. |
| [UC-07](UC-07.md) | Change Password | AUTH-07 | Customer, Staff, Admin | A signed-in user changes their password after confirming the current one. |

### Profile & Address Book

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-08](UC-08.md) | View Profile | PROF-01 | Customer | A customer views their personal information. |
| [UC-09](UC-09.md) | Update Profile | PROF-02 | Customer | A customer updates their full name, phone, gender, date of birth and avatar. |
| [UC-10](UC-10.md) | View Addresses | PROF-03 | Customer | A customer views their saved shipping addresses. |
| [UC-11](UC-11.md) | Add / Update / Delete Address | PROF-04 | Customer | A customer adds, edits or deletes a shipping address. |
| [UC-12](UC-12.md) | Set Default Address | PROF-05 | Customer | A customer chooses which address is preselected at checkout. |

### Product Catalog

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-13](UC-13.md) | View Categories | CAT-01 | Guest, Customer | A visitor browses the category tree. |
| [UC-14](UC-14.md) | Product Listing | CAT-02 | Guest, Customer | A visitor sees a paged list of products, optionally in one category. |
| [UC-15](UC-15.md) | Product Detail | CAT-03 | Guest, Customer | A visitor views full product information: gallery, sizes/colors, price, stock and rating. |
| [UC-16](UC-16.md) | Search Products | CAT-04 | Guest, Customer | A visitor searches products by keyword in name or brand. |
| [UC-17](UC-17.md) | Filter Products | CAT-05 | Guest, Customer | A visitor narrows the product list by category, price range, size, color, brand and gender. |
| [UC-18](UC-18.md) | Sort Products | CAT-06 | Guest, Customer | A visitor sorts the product list by newest, price, best selling or rating. |
| [UC-19](UC-19.md) | Featured Products | CAT-07 | Guest, Customer | The home page shows featured products. |
| [UC-20](UC-20.md) | New Arrivals | CAT-08 | Guest, Customer | The home page shows the newest products. |
| [UC-21](UC-21.md) | Best Sellers | CAT-09 | Guest, Customer | The home page shows the best-selling products. |
| [UC-22](UC-22.md) | Flash Sale / Hot Deal | CAT-10 | Guest, Customer | A visitor sees products whose sale is active now. |

### Wishlist

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-23](UC-23.md) | Add to Wishlist | WISH-01 | Customer | A customer saves a product to their wishlist. |
| [UC-24](UC-24.md) | Remove from Wishlist | WISH-02 | Customer | A customer removes a product from their wishlist. |
| [UC-25](UC-25.md) | View Wishlist | WISH-03 | Customer | A customer views their saved products. |

### Shopping Cart

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-26](UC-26.md) | Add to Cart | CART-01 | Guest, Customer | A visitor adds a variant (size/color) to their cart; guests use a session cart. |
| [UC-27](UC-27.md) | View Cart | CART-02 | Guest, Customer | A visitor views their cart with current prices. |
| [UC-28](UC-28.md) | Update Quantity | CART-03 | Guest, Customer | A visitor changes the quantity of a cart line. |
| [UC-29](UC-29.md) | Remove Item | CART-04 | Guest, Customer | A visitor removes a line from their cart. |
| [UC-30](UC-30.md) | Select Items for Checkout | CART-05 | Guest, Customer | A visitor chooses which cart lines will be checked out. |
| [UC-31](UC-31.md) | Merge Guest Cart | CART-06 | System | When a guest logs in, the system moves their session cart into their user cart. |

### Checkout & Payment

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-32](UC-32.md) | Select Shipping Address | CHK-01 | Customer | At checkout, a customer chooses a shipping address from their address book. |
| [UC-33](UC-33.md) | Apply Voucher | CHK-02 | Customer | At checkout, a customer enters a voucher code and sees the discount. |
| [UC-34](UC-34.md) | Place Order (COD) | CHK-04 | Customer | A customer places an order paid by Cash on Delivery. |
| [UC-35](UC-35.md) | Pay with VietQR | CHK-05 | Customer | A customer places an order and pays by bank transfer by scanning a VietQR code. |
| [UC-36](UC-36.md) | Receive Payment Webhook | CHK-06 | Bank webhook service (System) | The bank webhook service notifies Daisy of an incoming transfer, and Daisy marks the matching order as paid. |
| [UC-72](UC-72.md) | Expire Unpaid VietQR Order | — (added) | System | A background job cancels VietQR orders not paid before their deadline and releases their stock. |

### My Orders

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-37](UC-37.md) | Order History | ORD-01 | Customer | A customer views their orders, newest first. |
| [UC-38](UC-38.md) | Order Detail | ORD-02 | Customer | A customer views the items, address, amounts and payment of one of their orders. |
| [UC-39](UC-39.md) | Track Order Status | ORD-03 | Customer | A customer follows the progress of an order: Pending -> Confirmed -> Shipping -> Completed. |
| [UC-40](UC-40.md) | Cancel Order (Customer) | ORD-04 | Customer | A customer cancels their own order while it is still Pending or Confirmed. |

### Review & Rating

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-41](UC-41.md) | Write Review | REV-01 | Customer | A customer rates (1-5 stars) and comments on a product they bought in a completed order. |
| [UC-42](UC-42.md) | View Product Reviews | REV-02 | Guest, Customer | A visitor reads the reviews of a product. |

### Category Management

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-43](UC-43.md) | Create / Update Category | ACAT-01 | Admin, Staff | Staff create or edit a category: name, slug, image, parent and sort order. |
| [UC-44](UC-44.md) | Show / Hide Category | ACAT-02 | Admin, Staff | Staff show or hide a category in the storefront. |

### Product Management

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-45](UC-45.md) | Create / Update Product | APRD-01 | Admin, Staff | Staff create or edit a product: category, name, slug, brand, description, material, gender and base price. |
| [UC-46](UC-46.md) | Show / Hide Product | APRD-02 | Admin, Staff | Staff hide a product instead of deleting it, to keep order history. |
| [UC-47](UC-47.md) | Manage Variants | APRD-03 | Admin, Staff | Staff add, edit or hide product variants: SKU, size, color, extra price. |
| [UC-48](UC-48.md) | Manage Images | APRD-04 | Admin, Staff | Staff upload product images, choose the thumbnail, reorder them and link them to a color. |
| [UC-49](UC-49.md) | Set Featured | APRD-05 | Admin, Staff | Staff mark or unmark a product as featured on the home page. |
| [UC-50](UC-50.md) | Set Flash Sale | APRD-06 | Admin | An Admin sets a sale price and its start and end time for a product. |

### Inventory

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-51](UC-51.md) | Update Stock | INV-01 | Admin, Staff | Staff update the stock quantity of a variant. |
| [UC-52](UC-52.md) | Low-stock Warning | INV-02 | Admin, Staff | Staff see the variants whose stock is at or below their warning threshold. |

### Order Management

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-53](UC-53.md) | View / Filter Orders | AORD-01 | Admin, Staff | Staff view all orders and filter by status, date and order code. |
| [UC-54](UC-54.md) | View Order Detail (Admin) | AORD-02 | Admin, Staff | Staff view an order's items, amounts, payment and customer information. |
| [UC-55](UC-55.md) | Update Order Status | AORD-03 | Admin, Staff | Staff move an order forward: Confirmed -> Shipping -> Completed. |
| [UC-56](UC-56.md) | Cancel Order (Admin) | AORD-04 | Admin, Staff | Staff cancel an order with a reason; stock is restored. |
| [UC-57](UC-57.md) | Update Payment Status | AORD-05 | Admin, Staff | Staff confirm COD collection, confirm a VietQR transfer manually, or mark a refund. |
| [UC-71](UC-71.md) | Reconcile Payment Transactions | — (added) | Admin, Staff | Staff apply, refund or ignore bank transfers that were not matched to an order automatically. |

### Voucher Management

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-58](UC-58.md) | Create / Update Voucher | AVOU-01 | Admin | An Admin creates or edits a voucher: type, value, maximum discount, minimum order, usage limit and period. |
| [UC-59](UC-59.md) | Activate / Deactivate Voucher | AVOU-02 | Admin | An Admin turns a voucher on or off. |
| [UC-60](UC-60.md) | View Voucher Usage | AVOU-03 | Admin | An Admin sees how many times a voucher was used and which orders used it. |

### Review Moderation

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-61](UC-61.md) | View All Reviews | AREV-01 | Admin, Staff | Staff view all reviews and filter by product and rating. |
| [UC-62](UC-62.md) | Show / Hide Review | AREV-02 | Admin, Staff | Staff hide a review that breaks the rules, or show it again. |

### User Management

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-63](UC-63.md) | View / Search Users | AUSR-01 | Admin | An Admin views the user list and searches by name or email. |
| [UC-64](UC-64.md) | View User Detail | AUSR-02 | Admin | An Admin views a user's information and order history. |
| [UC-65](UC-65.md) | Lock / Unlock Account | AUSR-03 | Admin | An Admin locks or unlocks a user account. |
| [UC-66](UC-66.md) | Change Role | AUSR-04 | Admin | An Admin changes a user's role between Customer, Staff and Admin. |

### Dashboard & Reports

| ID | Name | Feature | Actor(s) | Description |
|---|---|---|---|---|
| [UC-67](UC-67.md) | Revenue Statistics | DASH-01 | Admin | An Admin sees revenue by day or month from completed orders. |
| [UC-68](UC-68.md) | Order Statistics | DASH-02 | Admin | An Admin sees the number of orders per status. |
| [UC-69](UC-69.md) | Top Selling Products | DASH-03 | Admin | An Admin sees the best-selling products. |
| [UC-70](UC-70.md) | New Customers | DASH-04 | Admin | An Admin sees the number of new customer registrations over time. |

## Features Covered by Business Rules

| Feature | Name | Business rule |
|---|---|---|
| AUTH-08 | Role-based Authorization | [BR-07](../business-rules/BR-07.md) Role-Based Permissions |
| CHK-03 | Calculate Order Total | [BR-24](../business-rules/BR-24.md) Order Total Calculation |
| CHK-07 | Stock Deduction | [BR-30](../business-rules/BR-30.md) Atomic Order Placement |
| REV-03 | Update Product Rating | [BR-38](../business-rules/BR-38.md) Rating Aggregation |

## Feature to Use Case Map

| Feature | Use case |
|---|---|
| AUTH-01 | [UC-01](UC-01.md) |
| AUTH-02 | [UC-02](UC-02.md) |
| AUTH-03 | [UC-03](UC-03.md) |
| AUTH-04 | [UC-04](UC-04.md) |
| AUTH-05 | [UC-05](UC-05.md) |
| AUTH-06 | [UC-06](UC-06.md) |
| AUTH-07 | [UC-07](UC-07.md) |
| AUTH-08 | [BR-07](../business-rules/BR-07.md) |
| PROF-01 | [UC-08](UC-08.md) |
| PROF-02 | [UC-09](UC-09.md) |
| PROF-03 | [UC-10](UC-10.md) |
| PROF-04 | [UC-11](UC-11.md) |
| PROF-05 | [UC-12](UC-12.md) |
| CAT-01 | [UC-13](UC-13.md) |
| CAT-02 | [UC-14](UC-14.md) |
| CAT-03 | [UC-15](UC-15.md) |
| CAT-04 | [UC-16](UC-16.md) |
| CAT-05 | [UC-17](UC-17.md) |
| CAT-06 | [UC-18](UC-18.md) |
| CAT-07 | [UC-19](UC-19.md) |
| CAT-08 | [UC-20](UC-20.md) |
| CAT-09 | [UC-21](UC-21.md) |
| CAT-10 | [UC-22](UC-22.md) |
| WISH-01 | [UC-23](UC-23.md) |
| WISH-02 | [UC-24](UC-24.md) |
| WISH-03 | [UC-25](UC-25.md) |
| CART-01 | [UC-26](UC-26.md) |
| CART-02 | [UC-27](UC-27.md) |
| CART-03 | [UC-28](UC-28.md) |
| CART-04 | [UC-29](UC-29.md) |
| CART-05 | [UC-30](UC-30.md) |
| CART-06 | [UC-31](UC-31.md) |
| CHK-01 | [UC-32](UC-32.md) |
| CHK-02 | [UC-33](UC-33.md) |
| CHK-03 | [BR-24](../business-rules/BR-24.md) |
| CHK-04 | [UC-34](UC-34.md) |
| CHK-05 | [UC-35](UC-35.md) |
| CHK-06 | [UC-36](UC-36.md) |
| CHK-07 | [BR-30](../business-rules/BR-30.md) |
| ORD-01 | [UC-37](UC-37.md) |
| ORD-02 | [UC-38](UC-38.md) |
| ORD-03 | [UC-39](UC-39.md) |
| ORD-04 | [UC-40](UC-40.md) |
| REV-01 | [UC-41](UC-41.md) |
| REV-02 | [UC-42](UC-42.md) |
| REV-03 | [BR-38](../business-rules/BR-38.md) |
| ACAT-01 | [UC-43](UC-43.md) |
| ACAT-02 | [UC-44](UC-44.md) |
| APRD-01 | [UC-45](UC-45.md) |
| APRD-02 | [UC-46](UC-46.md) |
| APRD-03 | [UC-47](UC-47.md) |
| APRD-04 | [UC-48](UC-48.md) |
| APRD-05 | [UC-49](UC-49.md) |
| APRD-06 | [UC-50](UC-50.md) |
| INV-01 | [UC-51](UC-51.md) |
| INV-02 | [UC-52](UC-52.md) |
| AORD-01 | [UC-53](UC-53.md) |
| AORD-02 | [UC-54](UC-54.md) |
| AORD-03 | [UC-55](UC-55.md) |
| AORD-04 | [UC-56](UC-56.md) |
| AORD-05 | [UC-57](UC-57.md) |
| AVOU-01 | [UC-58](UC-58.md) |
| AVOU-02 | [UC-59](UC-59.md) |
| AVOU-03 | [UC-60](UC-60.md) |
| AREV-01 | [UC-61](UC-61.md) |
| AREV-02 | [UC-62](UC-62.md) |
| AUSR-01 | [UC-63](UC-63.md) |
| AUSR-02 | [UC-64](UC-64.md) |
| AUSR-03 | [UC-65](UC-65.md) |
| AUSR-04 | [UC-66](UC-66.md) |
| DASH-01 | [UC-67](UC-67.md) |
| DASH-02 | [UC-68](UC-68.md) |
| DASH-03 | [UC-69](UC-69.md) |
| DASH-04 | [UC-70](UC-70.md) |
