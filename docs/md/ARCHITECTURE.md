# Daisy Architecture

## 1. Overview

**Daisy** is an e-commerce web application built using **ASP.NET Core** and organized according to **Clean Architecture** principles.

The system separates the user interface, application logic, domain model, and infrastructure concerns into independent projects.

The solution consists of five main projects and one test project:

```text
Daisy/
├── Daisy.Domain/
├── Daisy.Application/
├── Daisy.Infrastructure/
├── Daisy.Api/
├── Daisy.Mvc/
├── Daisy.Tests/            (integration tests)
├── infra/database/         (DaisyDB.sql schema, DaisySeed.sql development data)
└── Daisy.slnx
```

The architecture follows this dependency direction:

```text
Daisy.Mvc
    │
    │ HTTP / REST / OData
    ▼
Daisy.Api
    │
    ▼
Daisy.Application
    │
    ▼
Daisy.Domain

Daisy.Infrastructure
    │
    ├── Database
    ├── Authentication
    ├── Payment
    ├── Email
    └── File Storage
```

The core dependency rule is:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API
```

`Daisy.Mvc` communicates with `Daisy.Api` through HTTP and does not access the database directly.

## Backend Technology Stack

| Concern | Technology | Used in |
|---|---|---|
| Runtime & language | .NET 8 (LTS), C# | All projects |
| Web API | ASP.NET Core Web API (controllers) | `Daisy.Api` |
| Flexible querying | ASP.NET Core OData (`Microsoft.AspNetCore.OData`) | `Daisy.Api` |
| API documentation | OpenAPI + Swagger UI (`Swashbuckle.AspNetCore`) | `Daisy.Api` |
| Database | Microsoft SQL Server 2022 | Schema: `infra/database/DaisyDB.sql` |
| ORM | Entity Framework Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`) | `Daisy.Infrastructure` |
| Authentication | JWT Bearer access token (`Microsoft.AspNetCore.Authentication.JwtBearer`) + refresh token | `Daisy.Api`, `Daisy.Infrastructure` |
| Authorization | ASP.NET Core role-based authorization (Customer / Staff / Admin) | `Daisy.Api` |
| Password hashing | ASP.NET Core Identity `PasswordHasher<T>` (PBKDF2), without the full Identity stack | `Daisy.Infrastructure` |
| Validation | FluentValidation | `Daisy.Application` |
| DTO mapping | Manual mapping and LINQ projections (no AutoMapper) | `Daisy.Application` |
| Payment | VietQR bank transfer + SePay bank webhook service (a mock provider in development) | `Daisy.Infrastructure` |
| Email | MailKit (SMTP); emails are written to the log when no SMTP host is configured | `Daisy.Infrastructure` |
| File storage | Local file system behind `IStorageService` | `Daisy.Infrastructure` |
| Background work | `BackgroundService` hosted in the API process | `Daisy.Infrastructure` |
| Rate limiting | ASP.NET Core rate limiter (fixed window per IP) | `Daisy.Api` |
| Secrets | `dotnet user-secrets` in development, environment variables elsewhere | `Daisy.Api` |
| Web client | ASP.NET Core MVC (Razor views, Vietnamese UI), cookie authentication holding the API tokens | `Daisy.Mvc` |
| Tests | xUnit + `WebApplicationFactory`, against a throw-away SQL Server LocalDB database | `Daisy.Tests` |

---

# 2. Architecture Principles

The project follows the following principles:

- Domain logic must not depend on ASP.NET Core, EF Core, OData, or external services.
- Application defines system use cases and abstractions.
- Infrastructure implements database and external-service integrations.
- API exposes application functionality through REST and OData endpoints.
- MVC acts as the web client and communicates with the API.
- Controllers should contain minimal business logic.
- Business rules should be handled by Domain or Application.
- DTOs are used for communication instead of exposing entities directly.
- External services are accessed through interfaces.
- Database operations that affect multiple entities should use transactions.

---

# 3. Daisy.Domain

## Purpose

`Daisy.Domain` contains the core business model.

It does not depend on any other Daisy project.

```text
Daisy.Domain/
├── Entities/
├── Enums/
└── Common/
```

## Entities

The entities correspond to the main business objects in Daisy.

```text
Entities/
├── User.cs
├── UserAddress.cs
├── Category.cs
├── Product.cs
├── ProductVariant.cs
├── ProductImage.cs
├── CartItem.cs
├── Wishlist.cs
├── Voucher.cs
├── Order.cs
├── OrderItem.cs
├── PaymentTransaction.cs
└── Review.cs
```

These entities correspond one-to-one to the 13 tables in `infra/database/DaisyDB.sql`, the main database structures used by the Daisy features such as authentication, catalog, cart, checkout, orders, vouchers, reviews, inventory, and user management.

## Enums

```text
Enums/
├── UserRole.cs
├── UserStatus.cs
├── OrderStatus.cs
├── PaymentMethod.cs
├── PaymentStatus.cs
├── PaymentMatchResult.cs
├── PaymentResolution.cs
└── DiscountType.cs
```

Enum member names are exactly the values allowed by the database `CHECK` constraints, and EF Core stores them as strings (`HasConversion<string>()`):

| Enum | Values | Database constraint |
|---|---|---|
| `UserRole` | `Customer`, `Staff`, `Admin` | `CK_Users_Role` |
| `UserStatus` | `Active`, `Locked` | `CK_Users_Status` |
| `OrderStatus` | `Pending`, `Confirmed`, `Shipping`, `Completed`, `Canceled` | `CK_Orders_OrderStatus` |
| `PaymentMethod` | `COD`, `Online` (`Online` = VietQR bank transfer) | `CK_Orders_PaymentMethod` |
| `PaymentStatus` | `Unpaid`, `Paid`, `Failed`, `Refunded` | `CK_Orders_PaymentStatus` |
| `PaymentMatchResult` | `Matched`, `NoOrder`, `Underpaid`, `OrderNotPayable` | `CK_PaymentTransactions_MatchResult` |
| `PaymentResolution` | `Applied`, `Refunded`, `Ignored` | `CK_PaymentTransactions_Resolution` |
| `DiscountType` | `Percent`, `Fixed` | `CK_Vouchers_DiscountType` |

Example:

```csharp
public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipping,
    Completed,
    Canceled
}
```

## Business Rules

Important domain rules can be placed inside entities.

Example:

```csharp
public class Order
{
    public OrderStatus Status { get; private set; }

    public void Cancel()
    {
        if (Status != OrderStatus.Pending &&
            Status != OrderStatus.Confirmed)
        {
            throw new InvalidOperationException(
                "This order cannot be canceled.");
        }

        Status = OrderStatus.Canceled;
    }
}
```

Examples of domain rules include:

- An order can only be canceled in valid states.
- Product stock cannot become negative.
- Product quantity must be greater than zero.
- Voucher discounts cannot make an order total negative.
- Rating values must be between 1 and 5.

---

# 4. Daisy.Application

## Purpose

`Daisy.Application` contains the application use cases.

It coordinates:

- Domain entities
- Database abstractions
- Validation
- DTO mapping
- Authentication services
- Payment services
- File storage
- Email services

Structure:

```text
Daisy.Application/
├── Features/      (services, DTOs and validators per feature, see §6)
├── Interfaces/
└── Common/        (exceptions, paging, options, Vietnam time)
```

---

# 5. Application Features

The application is organized by business feature.

```text
Features/
├── Auth/
├── Profile/
├── Categories/
├── Products/
├── Cart/
├── Wishlist/
├── Checkout/
├── Payments/        (VietQR, webhook, reconciliation)
├── Orders/          (incl. expiry of unpaid VietQR orders)
├── Reviews/
├── Inventory/
├── Vouchers/
├── Users/
└── Dashboard/
```

This structure follows the functional areas of Daisy instead of grouping everything only by technical type.

---

# 6. Feature Structure

A feature should initially remain simple.

Example:

```text
Products/
├── ProductService.cs
├── ProductDtos.cs
└── ProductValidator.cs
```

Example:

```text
Cart/
├── CartService.cs
└── CartDtos.cs
```

Example:

```text
Orders/
├── OrderService.cs
└── OrderDtos.cs
```

The project does not require CQRS or MediatR initially.

If a feature becomes large, it can later be separated into commands and queries.

---

# 7. DTOs

Domain entities should not be returned directly from the API.

DTOs are used according to specific use cases.

Example:

```csharp
public sealed record ProductListDto(
    int Id,
    string Name,
    decimal Price,
    decimal? SalePrice,
    string? ThumbnailUrl,
    decimal AverageRating
);
```

For product details:

```csharp
public sealed record ProductDetailDto(
    int Id,
    string Name,
    string Brand,
    string Description,
    decimal Price,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images
);
```

Different use cases may use different DTOs:

```text
ProductListDto
ProductDetailDto
ProductAdminDto
CreateProductDto
UpdateProductDto
```

---

# 8. Application Interfaces

External dependencies are defined through interfaces.

```text
Interfaces/
├── IApplicationDbContext.cs
├── ITokenService.cs
├── IPasswordHasher.cs
├── IEmailService.cs
├── IStorageService.cs
├── IPaymentGateway.cs
└── ICurrentUserService.cs
```

Example:

```csharp
public interface IPaymentGateway
{
    // Builds the VietQR transfer info (QR image, account, amount, transfer content).
    Task<PaymentQrResult> CreatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    // Verifies a webhook reporting an incoming bank transfer.
    Task<PaymentResult> VerifyWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default);
}
```

The Application project knows only `IPaymentGateway`.

It does not know whether the implementation uses:

```text
SePayPaymentGateway   VietQR + the SePay bank webhook service (VietQr:Provider = SePay)
MockPaymentGateway    same QR and webhook format, plus simulated transfers (VietQr:Provider = Mock, development)
```

---

# 9. Daisy.Infrastructure

## Purpose

`Daisy.Infrastructure` contains concrete implementations for external dependencies.

```text
Daisy.Infrastructure/
├── Persistence/
├── Authentication/
├── Payments/
├── BackgroundJobs/
├── Storage/
├── Email/
└── DependencyInjection.cs
```

---

# 10. Persistence

Database access is implemented using **Entity Framework Core**.

```text
Persistence/
├── DaisyDbContext.cs
├── DaisyDbContextFactory.cs   (design-time factory for dotnet-ef)
├── Configurations/
└── Migrations/
```

`infra/database/DaisyDB.sql` remains the documented schema. The EF Core model maps the same names, constraints and indexes, so the database created by the `InitialCreate` migration is identical to the one created by the script (checked by comparing columns, defaults, indexes, check constraints and foreign keys). Either can be used to create a development database; `infra/database/DaisySeed.sql` then loads the development data (Admin, Staff and Customer accounts, catalog, vouchers, sample orders).

Entity configurations are separated from the DbContext.

```text
Configurations/
├── UserConfiguration.cs
├── UserAddressConfiguration.cs
├── CategoryConfiguration.cs
├── ProductConfiguration.cs
├── ProductVariantConfiguration.cs
├── ProductImageConfiguration.cs
├── CartItemConfiguration.cs
├── WishlistConfiguration.cs
├── VoucherConfiguration.cs
├── OrderConfiguration.cs
├── OrderItemConfiguration.cs
├── PaymentTransactionConfiguration.cs
└── ReviewConfiguration.cs
```

There is one configuration per table. Configurations map to the existing column names, constraints and indexes of `DaisyDB.sql` (e.g. `ProductName`, `ProductNameSnapshot`, `Sku`), so the EF Core model and the SQL script describe the same schema.

Example:

```csharp
public sealed class CategoryConfiguration
    : IEntityTypeConfiguration<Category>
{
    public void Configure(
        EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(x => x.CategoryId);

        builder.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId);
    }
}
```

The category relationship is self-referencing:

```text
Category
│
├── CategoryId
└── ParentCategoryId
       │
       └── Categories.CategoryId
```

This allows Daisy to represent hierarchical categories such as:

```text
Men
├── Shirts
├── Pants
└── Shoes
```

---

# 11. Database Access

Application should interact with the database through:

```csharp
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductVariant> ProductVariants { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<PaymentTransaction> PaymentTransactions { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
```

`DaisyDbContext` implements this interface.

```text
Application
     │
     ▼
IApplicationDbContext
     ▲
     │
Infrastructure
     │
     ▼
DaisyDbContext
     │
     ▼
SQL Server
```

A generic repository is not required.

EF Core can be used directly through the DbContext abstraction.

---

# 12. Authentication

Authentication is implemented using:

- JWT Access Token
- Refresh Token
- Password Hashing
- Role-based Authorization

Infrastructure:

```text
Authentication/
├── JwtTokenService.cs
└── PasswordHasher.cs
```

Application features:

```text
Auth/
├── AuthService.cs
└── AuthDtos.cs
```

Supported operations include:

```text
Register
Login
Logout
Refresh Token
Forgot Password
Reset Password
Change Password
```

Authorization roles include:

```text
Customer
Staff
Admin
```

---

# 13. Product Catalog

The product model is separated into:

```text
Product
├── ProductVariants
└── ProductImages
```

`Product` contains common product information (`Products` table):

```text
CategoryId
ProductName
Slug
Brand
Description
Material
GenderTarget
BasePrice
SalePrice
SaleStartAt
SaleEndAt
AverageRating
ReviewCount
SoldCount
IsFeatured
IsActive
CreatedAt
UpdatedAt
```

`SalePrice` applies only between `SaleStartAt` and `SaleEndAt` (both NULL = permanent sale price), so flash sales need no separate table.

`ProductVariant` represents purchasable combinations (`ProductVariants` table):

```text
Sku
Size
Color
ColorHex
ExtraPrice
StockQuantity
LowStockThreshold
IsActive
```

`Sku` is unique, and `(ProductId, Size, Color)` is unique.

For example:

```text
Product: Basic T-Shirt

Variants:
├── S / Black
├── M / Black
├── L / Black
├── S / White
└── M / White
```

---

# 14. Shopping Cart

Daisy supports both guest and authenticated carts.

Guest:

```text
SessionId
   │
   ▼
CartItems
```

Customer:

```text
UserId
   │
   ▼
CartItems
```

When the customer logs in:

```text
Guest Cart
     +
Customer Cart
     │
     ▼
Merge Cart
```

A CartItem belongs to a User or to a guest Session. The database (`CK_CartItems_UserOrSession`) requires at least one of `UserId` / `SessionId`; the application always sets exactly one, and the merge clears `SessionId` when it moves a line to the user.

A cart has at most one line per variant (`UX_CartItems_User_Variant`, `UX_CartItems_Session_Variant`). Prices are not stored in the cart; they are read live from `Products` and `ProductVariants`.

---

# 15. Checkout

Checkout is separated from Cart because checkout contains more complex business operations.

Typical checkout flow:

```text
Selected Cart Items
        │
        ▼
Validate Products
        │
        ▼
Validate Inventory
        │
        ▼
Validate Voucher
        │
        ▼
Calculate Subtotal
        │
        ▼
Calculate Discount
        │
        ▼
Calculate Shipping
        │
        ▼
Calculate Final Total
        │
        ▼
Create Order
```

The final order amount must always be calculated by the backend.

The MVC client must not be trusted to provide the final amount.

---

# 16. Orders

An order stores snapshot data so historical orders are not affected by later changes.

Order shipping information includes:

```text
ReceiverName
ReceiverPhone
ShippingAddress
```

OrderItem stores product snapshots:

```text
ProductNameSnapshot
SizeSnapshot
ColorSnapshot
ImageUrlSnapshot
UnitPrice
Quantity
LineTotal        (computed: UnitPrice * Quantity)
```

This ensures that changing product information later does not modify historical orders.

Order status lifecycle (`CK_Orders_OrderStatus`):

```text
Pending ──► Confirmed ──► Shipping ──► Completed
   │            │
   └────────────┴──► Canceled
```

- Status moves forward one step at a time; `Completed` and `Canceled` are final.
- A VietQR order can move from `Pending` to `Confirmed` only after it is `Paid`.
- Canceling restores stock, `SoldCount` and voucher `UsedCount` in one transaction. An order is canceled by the customer, by staff, or by the system when a VietQR payment deadline passes (§17).

Payment fields are stored directly on `Orders` (one order = one payment):

```text
PaymentMethod      (COD / Online)
PaymentStatus      (Unpaid / Paid / Failed / Refunded)
PaymentExpiresAt   (VietQR payment deadline; NULL for COD)
TransactionCode    (bank reference; unique when set)
PaidAt
```

---

# 17. Payment

Payment is separated through an abstraction.

```text
Application
     │
     ▼
IPaymentGateway
     ▲
     │
Infrastructure
     │
     ▼
VietQR + Bank Webhook Service
```

Infrastructure structure:

```text
Payments/
├── SePayPaymentGateway.cs
├── MockPaymentGateway.cs
└── VietQrOptions.cs

BackgroundJobs/
└── UnpaidOrderExpiryJob.cs
```

SePay calls the webhook with `Authorization: Apikey {WebhookApiKey}`. The mock gateway accepts the same payload, so in development a transfer can be simulated (`POST /api/dev/payments/simulate-transfer`) and still goes through the real verification and matching code.

`VietQrOptions` holds the receiving account, webhook and deadline settings:

```text
BankBin               (receiving bank BIN)
AccountNumber         (receiving account number)
AccountName           (account holder name)
QrTemplate            (QR image template, e.g. compact2)
WebhookApiKey         (key used to verify webhooks)
PaymentTimeoutMinutes (payment deadline after ordering, default 30)
ExpiryGraceMinutes    (late webhooks still accepted, default 5)
```

Daisy supports two payment methods.

### Cash on Delivery (COD)

```text
PaymentMethod = COD
PaymentStatus = Unpaid
```

Staff confirm the payment once the order is delivered (AORD-05).

### Bank Transfer via VietQR

VietQR is the NAPAS interbank QR transfer standard. The customer scans the code with any banking app, and the amount and transfer content are pre-filled.

```text
PaymentMethod = Online (VietQR)
PaymentStatus = Unpaid → Paid
```

Payment flow:

```text
Checkout
   │
   ▼
Create Order
(PaymentStatus = Unpaid, PaymentExpiresAt = now + PaymentTimeoutMinutes)
   │
   ▼
Generate VietQR
(receiving account + amount = TotalAmount + content = OrderCode)
+ show the payment deadline
   │
   ▼
Customer scans the QR and transfers from a banking app
   │
   ▼
Bank webhook service calls Daisy.Api
   │
   ▼
Verify Webhook (API key / signature)
   │
   ▼
BankReference already in PaymentTransactions? ── yes ──► acknowledge, no change
   │ no
   ▼
Match Transaction
(find order by OrderCode in transfer content, check status and amount)
   │
   ▼
In one DB transaction:
  Insert PaymentTransaction (MatchResult)
  + if Matched: Update Order
    (PaymentStatus = Paid, PaidAt, TransactionCode = bank reference)
```

Related endpoints:

```http
GET   /api/orders/{id}/payment-qr

POST  /api/payments/webhook

GET   /api/admin/payment-transactions
POST  /api/admin/payment-transactions/{id}/apply
PATCH /api/admin/payment-transactions/{id}/resolution
```

### Payment Transactions and Reconciliation

Every verified incoming transfer is stored in `PaymentTransactions`, whether or not it matched an order:

```text
PaymentTransaction
├── BankReference      (unique → webhook idempotency)
├── Provider
├── AccountNumber
├── Amount
├── TransferContent
├── TransferredAt
├── ReceivedAt
├── OrderId            (matched or related order, if any)
├── MatchResult        Matched / NoOrder / Underpaid / OrderNotPayable
├── Resolution         NULL (needs review) / Applied / Refunded / Ignored
├── ResolutionNote
├── ResolvedByUserId
└── ResolvedAt
```

- `Matched`: the order was marked `Paid` automatically.
- `NoOrder`: no valid `OrderCode` in the transfer content.
- `Underpaid`: the amount is less than `TotalAmount`.
- `OrderNotPayable`: the order is already paid, canceled (e.g. expired) or COD.

A transfer that is not `Matched` and has no `Resolution` appears in the staff "Unmatched transfers" list. Staff resolve it once:

- **Applied**: apply the transfer to an `Online`, `Unpaid`, non-canceled order whose total is at most the amount; the order becomes `Paid`.
- **Refunded**: the money was returned to the sender outside Daisy.
- **Ignored**: no action needed (test or internal transfer).

A canceled order is never reopened by a late transfer; the transfer is refunded.

Staff can still confirm a payment manually on the order (AORD-05) when the webhook never arrived; a bank reference already recorded in `PaymentTransactions` must be resolved through the list instead.

### Payment Deadline

An unpaid VietQR order reserves stock, so it must not stay unpaid forever.

```text
Order placed ──► PaymentExpiresAt = CreatedAt + PaymentTimeoutMinutes (30)
                        │
                        ▼
      QR shown with a countdown; after the deadline the QR is no longer shown
                        │
                        ▼
      Grace period (ExpiryGraceMinutes = 5): late webhooks are still matched
                        │
                        ▼
      UnpaidOrderExpiryJob (every minute) cancels orders that are
      Online + Unpaid + Pending with PaymentExpiresAt + grace <= now
      → Canceled, CancelReason = 'Payment not received in time',
        stock, SoldCount and voucher UsedCount restored
```

- `UnpaidOrderExpiryJob` is a `BackgroundService` in `Daisy.Infrastructure/BackgroundJobs`. It only schedules the work; the use case is `OrderExpiryService.CancelExpiredOrdersAsync` in `Daisy.Application`, which uses `TimeProvider` so it can be tested.
- Each order is canceled in its own transaction with a conditional update (`WHERE PaymentStatus = 'Unpaid' AND OrderStatus = 'Pending'`). The webhook marks an order paid with the opposite condition (`WHERE PaymentStatus = 'Unpaid' AND OrderStatus <> 'Canceled'`), so exactly one of them wins when both run at the same moment. The job is safe to run on several API instances.
- A transfer that arrives after the order was canceled is recorded as `OrderNotPayable` and refunded by staff.
- The filtered index `IX_Orders_PaymentExpiresAt` keeps the job's query cheap.

### Reconciliation and idempotency notes

- The transfer content must contain the `OrderCode` so the order can be found. Keep `OrderCode` short, without diacritics or special characters, because some banks truncate or normalize the content.
- Mark an order as `Paid` only when the received amount is greater than or equal to `TotalAmount`.
- After showing the QR code, MVC displays a waiting state with the countdown and polls the order status.

Payment webhooks must be idempotent. `UQ_PaymentTransactions_BankReference` and `UX_Orders_TransactionCode` enforce this in the database, so even two copies of the same webhook processed at the same moment cannot both succeed.

Repeated webhooks must not repeatedly:

- Deduct stock
- Increase voucher usage
- Increase sold quantity
- Change an already completed payment

---

# 18. Inventory

Inventory is stored directly on `ProductVariant`.

```text
ProductVariant
├── StockQuantity
└── LowStockThreshold
```

Low stock is determined by:

```text
StockQuantity <= LowStockThreshold
```

No separate inventory table is required for the current scope.

Operations that modify stock should execute inside a transaction.

---

# 19. Voucher

Voucher information (`Vouchers` table):

```text
Code
Name
DiscountType        (Percent / Fixed)
DiscountValue
MaxDiscountAmount
MinOrderAmount
UsageLimit          (NULL = unlimited)
UsedCount
StartAt
EndAt
IsActive
```

Voucher validation includes:

- Active status
- Start time
- Expiration time
- Minimum order amount
- Usage limit
- Discount value

Voucher usage and stock deduction should be handled transactionally during order creation.

---

# 20. Reviews

A customer can review only a product that has been purchased from a completed order.

Application validation should ensure:

```text
Review.UserId
=
Order.UserId
```

and:

```text
Review.ProductId
=
Product belonging to OrderItem
```

Each OrderItem may only be reviewed once (`UQ_Reviews_OrderItem`).

Staff can hide a review (`Reviews.IsVisible = 0`) instead of deleting it.

When a review is created, hidden or shown again, Daisy recalculates from the **visible** reviews:

```text
Product.AverageRating
Product.ReviewCount
```

---

# 21. Daisy.Api

## Purpose

`Daisy.Api` exposes backend functionality.

```text
Daisy.Api/
├── Controllers/
├── OData/
├── Middleware/
└── Program.cs
```

---

# 22. REST Controllers

Standard business operations use REST endpoints.

```text
Controllers/
├── AuthController.cs
├── ProfileController.cs
├── ProductsController.cs
├── CategoriesController.cs
├── CartController.cs
├── WishlistController.cs
├── CheckoutController.cs
├── OrdersController.cs
├── PaymentsController.cs
├── ReviewsController.cs
└── Admin/
    ├── CategoriesController.cs
    ├── ProductsController.cs
    ├── InventoryController.cs
    ├── OrdersController.cs
    ├── PaymentTransactionsController.cs
    ├── VouchersController.cs
    ├── ReviewsController.cs
    ├── UsersController.cs
    └── DashboardController.cs
```

The full endpoint list is in [API_DESIGN.md](API_DESIGN.md).

Examples:

```http
POST /api/auth/login

POST /api/cart/items

POST /api/checkout

POST /api/orders/{id}/cancel

GET  /api/orders/{id}/payment-qr

POST /api/payments/webhook
```

Controllers should only:

1. Receive requests.
2. Validate transport-level input.
3. Call Application services.
4. Return HTTP responses.

Controllers should not contain database queries or complex business logic.

---

# 23. OData

OData is used for flexible querying.

```text
OData/
├── ProductsController.cs
├── OrdersController.cs
├── UsersController.cs
└── ReviewsController.cs
```

Typical endpoints:

```http
GET /odata/products

GET /odata/orders

GET /odata/users
```

Example:

```http
GET /odata/products
?$filter=BasePrice ge 100000
&$orderby=BasePrice desc
&$top=20
```

OData is mainly suitable for:

- Product listing
- Admin product management
- Order management
- User management
- Review moderation

Operations that execute business processes should remain normal REST endpoints.

For example:

```text
POST checkout        → REST
POST cancel order    → REST
POST login           → REST
POST payment webhook  → REST

GET filtered products → OData
GET filtered orders   → OData
```

---

# 24. Daisy.Mvc

## Purpose

`Daisy.Mvc` is the web client.

It does not access Entity Framework Core or SQL Server directly.

```text
Daisy.Mvc/
├── Areas/Admin/      (admin panel for Staff and Admin)
├── Controllers/      (storefront)
├── Infrastructure/   (auth cookie, cart session, error display)
├── Models/Api/       (copies of the API DTOs; MVC does not reference Daisy.Application)
├── Services/         (ApiClient and typed API services)
├── ViewComponents/
├── ViewModels/
├── Views/
└── wwwroot/
```

The user interface is in Vietnamese. API error codes are mapped to Vietnamese messages in the client; the API's own messages stay in English.

Communication:

```text
Browser
   │
   ▼
Daisy.Mvc
   │
   │ HTTP
   ▼
Daisy.Api
   │
   ▼
Application
   │
   ▼
Infrastructure
   │
   ▼
Database
```

---

# 25. MVC API Services

MVC communicates with the backend using `HttpClient`.

```text
Services/
├── ApiClient.cs
├── AuthApiService.cs
├── ProductApiService.cs
├── CartApiService.cs
├── OrderApiService.cs
└── PaymentApiService.cs
```

Example:

```csharp
public class ProductApiService
{
    private readonly HttpClient _httpClient;

    public ProductApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ProductViewModel?> GetByIdAsync(int id)
    {
        return _httpClient
            .GetFromJsonAsync<ProductViewModel>(
                $"/api/products/{id}");
    }
}
```

MVC must not reference:

```text
DaisyDbContext
Entity Framework Core
Daisy.Infrastructure.Persistence
```

---

# 26. Dependency Rules

Allowed dependencies:

```text
Daisy.Domain
    → Nothing

Daisy.Application
    → Daisy.Domain

Daisy.Infrastructure
    → Daisy.Application
    → Daisy.Domain

Daisy.Api
    → Daisy.Application
    → Daisy.Infrastructure

Daisy.Mvc
    → Daisy.Api through HTTP
```

Conceptually:

```text
             ┌──────────────────┐
             │    Daisy.Mvc     │
             └────────┬─────────┘
                      │ HTTP
                      ▼
             ┌──────────────────┐
             │    Daisy.Api     │
             └────────┬─────────┘
                      │
                      ▼
          ┌────────────────────────┐
          │   Daisy.Application    │
          └────────────┬───────────┘
                       │
                       ▼
             ┌──────────────────┐
             │   Daisy.Domain   │
             └──────────────────┘

          ┌────────────────────────┐
          │ Daisy.Infrastructure   │
          ├────────────────────────┤
          │ EF Core                │
          │ SQL Server             │
          │ JWT                    │
          │ Payment (VietQR)       │
          │ Background jobs        │
          │ Email                  │
          │ Storage                │
          └────────────────────────┘
```

---

# 27. Transaction Boundary

Operations involving multiple database changes must use transactions.

For example, successful checkout may involve:

```text
Create Order
+
Create OrderItems
+
Deduct Stock
+
Increase SoldCount
+
Increase Voucher UsedCount
+
Clear Cart
```

These operations must either all succeed or all fail.

```text
BEGIN TRANSACTION
       │
       ├── Create Order
       ├── Create OrderItems
       ├── Update Inventory
       ├── Update Voucher
       ├── Update Product
       └── Clear Cart
       │
       ▼
COMMIT
```

On failure:

```text
ROLLBACK
```

The same applies to:

- Canceling an order (customer, staff or payment expiry): order status + stock + `SoldCount` + voucher `UsedCount`.
- The payment webhook: insert `PaymentTransaction` + mark the order `Paid`.
- Applying an unmatched transfer to an order: mark the order `Paid` + resolve the `PaymentTransaction`.

---

# 28. Error Handling

The API should provide centralized error handling.

```text
Middleware/
└── ExceptionMiddleware.cs
```

The middleware converts application exceptions into standardized HTTP responses.

Example:

```json
{
  "status": 409,
  "code": "VOUCHER_NOT_ELIGIBLE",
  "message": "The voucher is no longer available.",
  "errors": null,
  "details": { "reason": "Expired" },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

The error codes and status mapping are defined in [API_DESIGN.md §4](API_DESIGN.md#4-error-handling).

Controllers should not repeat try/catch logic for every endpoint.

---

# 29. Dependency Injection

Each major project can expose its own dependency registration.

Infrastructure:

```csharp
services.AddInfrastructure(configuration);
```

Application:

```csharp
services.AddApplication();
```

API:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration);
```

This keeps `Program.cs` small and focused.

---

# 30. Recommended Final Structure

```text
Daisy/
│
├── Daisy.Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Common/
│
├── Daisy.Application/
│   ├── Features/          (Service, Dtos and Validator per feature)
│   │   ├── Auth/
│   │   ├── Profile/
│   │   ├── Categories/
│   │   ├── Products/
│   │   ├── Cart/
│   │   ├── Wishlist/
│   │   ├── Checkout/
│   │   ├── Payments/
│   │   ├── Orders/
│   │   ├── Reviews/
│   │   ├── Inventory/
│   │   ├── Vouchers/
│   │   ├── Users/
│   │   └── Dashboard/
│   │
│   ├── Interfaces/
│   └── Common/
│
├── Daisy.Infrastructure/
│   ├── Persistence/
│   │   ├── DaisyDbContext.cs
│   │   ├── Configurations/
│   │   └── Migrations/
│   │
│   ├── Authentication/
│   ├── Payments/
│   │   ├── SePayPaymentGateway.cs
│   │   ├── MockPaymentGateway.cs
│   │   └── VietQrOptions.cs
│   ├── BackgroundJobs/
│   │   └── UnpaidOrderExpiryJob.cs
│   ├── Storage/
│   └── Email/
│
├── Daisy.Api/
│   ├── Controllers/
│   │   └── Admin/
│   ├── OData/
│   ├── Middleware/
│   ├── Infrastructure/   (error body, JSON, current user, policies)
│   └── Program.cs
│
├── Daisy.Mvc/
│   ├── Areas/Admin/
│   ├── Controllers/
│   ├── Services/
│   ├── ViewModels/
│   ├── Views/
│   └── wwwroot/
│
├── Daisy.Tests/
│
├── infra/database/
│   ├── DaisyDB.sql
│   └── DaisySeed.sql
│
└── Daisy.slnx
```

---

# 31. Summary

Daisy uses a simplified Clean Architecture:

```text
MVC
 ↓
Web API
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Database / External Services
```

The architecture intentionally avoids unnecessary complexity.

The project does not require:

```text
Generic Repository
MediatR
Full CQRS
Separate Shared project
Separate Contracts project
Domain Events
Event Bus
Microservices
```

for the current scope.

The primary goal is to maintain:

```text
Clear responsibilities
+
Correct dependency direction
+
Testable business logic
+
Simple implementation
+
Easy future extension
```

This provides Daisy with a clean structure without over-engineering the application.