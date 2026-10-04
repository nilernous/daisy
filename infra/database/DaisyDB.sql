/*
Daisy - Smart Fashion E-commerce Platform
Database version: 13 tables (optimized)
Target DBMS: Microsoft SQL Server

IMPORTANT:
- This script DROPS and RECREATES database [Daisy].
- Use on a fresh development database.

Optimization notes:
- Removed monitoring / tracking data: audit logs, email logs, stock movement logs,
  report export logs, chatbot message logs, search history, recently viewed,
  AI request logs, IP address, view count, last login time.
- Removed AI features (size / outfit suggestion, color search, chatbot) and their
  columns: Users.PreferredSize, Users.PreferredStyle, ProductImages.DominantColorHex.
- Replaced the generic "catch-all" tables (CustomerActivities, CommerceRecords,
  InteractionLogs) with dedicated tables: Reviews, Wishlists, Vouchers.
- Payment info is stored directly on Orders (1 order = 1 payment).
- Incoming VietQR bank transfers are recorded in PaymentTransactions (webhook
  idempotency + manual reconciliation of unmatched transfers).
- Unpaid VietQR orders expire at Orders.PaymentExpiresAt and are canceled by a
  background job, which restores their stock.
- Flash sale / hot deal is stored on Products (SalePrice + SaleStartAt/SaleEndAt).
- Removed redundant columns (duplicated size/color/price, unused file metadata).
*/

USE [master];
GO

IF DB_ID(N'Daisy') IS NOT NULL
BEGIN
    ALTER DATABASE [Daisy] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Daisy];
END
GO

CREATE DATABASE [Daisy];
GO

USE [Daisy];
GO

-- Required for filtered indexes and persisted computed columns.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================
   1. Users
   Handles: register, login, JWT refresh token, logout,
   forgot/reset password, role and profile.
   ========================================================= */
CREATE TABLE [dbo].[Users] (
    [UserId] INT IDENTITY(1,1) NOT NULL,
    [FullName] NVARCHAR(150) NOT NULL,
    [Email] NVARCHAR(255) NOT NULL,
    [PasswordHash] NVARCHAR(255) NOT NULL,
    [Phone] NVARCHAR(20) NULL,
    [AvatarUrl] NVARCHAR(500) NULL,
    [Role] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Users_Role] DEFAULT N'Customer',
    [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Users_Status] DEFAULT N'Active',
    [Gender] NVARCHAR(20) NULL,
    [DateOfBirth] DATE NULL,
    [ResetPasswordToken] NVARCHAR(255) NULL,
    [ResetPasswordExpiresAt] DATETIME2 NULL,
    [RefreshToken] NVARCHAR(500) NULL,
    [RefreshTokenExpiresAt] DATETIME2 NULL,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT SYSDATETIME(),
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
    CONSTRAINT [UQ_Users_Email] UNIQUE ([Email]),
    CONSTRAINT [CK_Users_Role] CHECK ([Role] IN (N'Customer', N'Staff', N'Admin')),
    CONSTRAINT [CK_Users_Status] CHECK ([Status] IN (N'Active', N'Locked'))
);
GO

/* =========================================================
   2. UserAddresses
   Handles: address book and checkout shipping address.
   ========================================================= */
CREATE TABLE [dbo].[UserAddresses] (
    [AddressId] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [ReceiverName] NVARCHAR(150) NOT NULL,
    [ReceiverPhone] NVARCHAR(20) NOT NULL,
    [Province] NVARCHAR(100) NOT NULL,
    [District] NVARCHAR(100) NOT NULL,
    [Ward] NVARCHAR(100) NOT NULL,
    [StreetAddress] NVARCHAR(255) NOT NULL,
    [IsDefault] BIT NOT NULL CONSTRAINT [DF_UserAddresses_IsDefault] DEFAULT 0,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserAddresses_CreatedAt] DEFAULT SYSDATETIME(),
    CONSTRAINT [PK_UserAddresses] PRIMARY KEY ([AddressId]),
    CONSTRAINT [FK_UserAddresses_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
);
GO

/* =========================================================
   3. Categories
   Handles: category list (parent/child) and admin category CRUD.
   ========================================================= */
CREATE TABLE [dbo].[Categories] (
    [CategoryId] INT IDENTITY(1,1) NOT NULL,
    [ParentCategoryId] INT NULL,
    [CategoryName] NVARCHAR(150) NOT NULL,
    [Slug] NVARCHAR(200) NOT NULL,
    [ImageUrl] NVARCHAR(500) NULL,
    [SortOrder] INT NOT NULL CONSTRAINT [DF_Categories_SortOrder] DEFAULT 0,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Categories_IsActive] DEFAULT 1,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId]),
    CONSTRAINT [UQ_Categories_Slug] UNIQUE ([Slug]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentCategoryId]) REFERENCES [dbo].[Categories]([CategoryId])
);
GO

/* =========================================================
   4. Products
   Handles: product list, detail, search, filter, sort,
   featured, best seller, new arrival, flash sale / hot deal.
   SalePrice is applied only between SaleStartAt and SaleEndAt
   (both NULL = permanent sale price).
   ========================================================= */
CREATE TABLE [dbo].[Products] (
    [ProductId] INT IDENTITY(1,1) NOT NULL,
    [CategoryId] INT NOT NULL,
    [ProductName] NVARCHAR(200) NOT NULL,
    [Slug] NVARCHAR(250) NOT NULL,
    [Brand] NVARCHAR(100) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [Material] NVARCHAR(100) NULL,
    [GenderTarget] NVARCHAR(30) NULL,
    [BasePrice] DECIMAL(18,2) NOT NULL,
    [SalePrice] DECIMAL(18,2) NULL,
    [SaleStartAt] DATETIME2 NULL,
    [SaleEndAt] DATETIME2 NULL,
    [AverageRating] DECIMAL(3,2) NOT NULL CONSTRAINT [DF_Products_AverageRating] DEFAULT 0,
    [ReviewCount] INT NOT NULL CONSTRAINT [DF_Products_ReviewCount] DEFAULT 0,
    [SoldCount] INT NOT NULL CONSTRAINT [DF_Products_SoldCount] DEFAULT 0,
    [IsFeatured] BIT NOT NULL CONSTRAINT [DF_Products_IsFeatured] DEFAULT 0,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Products_IsActive] DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Products_CreatedAt] DEFAULT SYSDATETIME(),
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId]),
    CONSTRAINT [UQ_Products_Slug] UNIQUE ([Slug]),
    CONSTRAINT [FK_Products_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([CategoryId]),
    CONSTRAINT [CK_Products_BasePrice] CHECK ([BasePrice] >= 0),
    CONSTRAINT [CK_Products_SalePrice] CHECK ([SalePrice] IS NULL OR ([SalePrice] >= 0 AND [SalePrice] <= [BasePrice])),
    CONSTRAINT [CK_Products_SalePeriod] CHECK ([SaleStartAt] IS NULL OR [SaleEndAt] IS NULL OR [SaleStartAt] < [SaleEndAt]),
    CONSTRAINT [CK_Products_Rating] CHECK ([AverageRating] >= 0 AND [AverageRating] <= 5)
);
GO

/* =========================================================
   5. ProductVariants
   Handles: size, color, SKU, stock and low-stock warning
   for each product option.
   ========================================================= */
CREATE TABLE [dbo].[ProductVariants] (
    [VariantId] INT IDENTITY(1,1) NOT NULL,
    [ProductId] INT NOT NULL,
    [Sku] NVARCHAR(100) NOT NULL,
    [Size] NVARCHAR(20) NULL,
    [Color] NVARCHAR(50) NULL,
    [ColorHex] NVARCHAR(20) NULL,
    [ExtraPrice] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_ProductVariants_ExtraPrice] DEFAULT 0,
    [StockQuantity] INT NOT NULL CONSTRAINT [DF_ProductVariants_StockQuantity] DEFAULT 0,
    [LowStockThreshold] INT NOT NULL CONSTRAINT [DF_ProductVariants_LowStockThreshold] DEFAULT 5,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_ProductVariants_IsActive] DEFAULT 1,
    CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([VariantId]),
    CONSTRAINT [UQ_ProductVariants_Sku] UNIQUE ([Sku]),
    CONSTRAINT [UQ_ProductVariants_Option] UNIQUE ([ProductId], [Size], [Color]),
    CONSTRAINT [FK_ProductVariants_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]) ON DELETE CASCADE,
    CONSTRAINT [CK_ProductVariants_Stock] CHECK ([StockQuantity] >= 0),
    CONSTRAINT [CK_ProductVariants_LowStock] CHECK ([LowStockThreshold] >= 0)
);
GO

/* =========================================================
   6. ProductImages
   Handles: image gallery, thumbnail and image by variant/color.
   ========================================================= */
CREATE TABLE [dbo].[ProductImages] (
    [ImageId] INT IDENTITY(1,1) NOT NULL,
    [ProductId] INT NOT NULL,
    [VariantId] INT NULL,
    [ImageUrl] NVARCHAR(500) NOT NULL,
    [IsThumbnail] BIT NOT NULL CONSTRAINT [DF_ProductImages_IsThumbnail] DEFAULT 0,
    [SortOrder] INT NOT NULL CONSTRAINT [DF_ProductImages_SortOrder] DEFAULT 0,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([ImageId]),
    CONSTRAINT [FK_ProductImages_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductImages_ProductVariants] FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariants]([VariantId])
);
GO

/* =========================================================
   7. CartItems
   Handles: add to cart, view cart, update quantity,
   remove item and guest cart by SessionId.
   Price is read live from Products/ProductVariants.
   ========================================================= */
CREATE TABLE [dbo].[CartItems] (
    [CartItemId] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NULL,
    [SessionId] NVARCHAR(100) NULL,
    [VariantId] INT NOT NULL,
    [Quantity] INT NOT NULL,
    [IsSelected] BIT NOT NULL CONSTRAINT [DF_CartItems_IsSelected] DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_CartItems_CreatedAt] DEFAULT SYSDATETIME(),
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([CartItemId]),
    CONSTRAINT [FK_CartItems_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_CartItems_ProductVariants] FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariants]([VariantId]) ON DELETE CASCADE,
    CONSTRAINT [CK_CartItems_Quantity] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_CartItems_UserOrSession] CHECK ([UserId] IS NOT NULL OR [SessionId] IS NOT NULL)
);
GO

/* =========================================================
   8. Vouchers
   Handles: discount codes applied at checkout.
   ========================================================= */
CREATE TABLE [dbo].[Vouchers] (
    [VoucherId] INT IDENTITY(1,1) NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [DiscountType] NVARCHAR(30) NOT NULL,
    [DiscountValue] DECIMAL(18,2) NOT NULL,
    [MaxDiscountAmount] DECIMAL(18,2) NULL,
    [MinOrderAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Vouchers_MinOrderAmount] DEFAULT 0,
    [UsageLimit] INT NULL,
    [UsedCount] INT NOT NULL CONSTRAINT [DF_Vouchers_UsedCount] DEFAULT 0,
    [StartAt] DATETIME2 NOT NULL,
    [EndAt] DATETIME2 NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Vouchers_IsActive] DEFAULT 1,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([VoucherId]),
    CONSTRAINT [UQ_Vouchers_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_Vouchers_DiscountType] CHECK ([DiscountType] IN (N'Percent', N'Fixed')),
    CONSTRAINT [CK_Vouchers_DiscountValue] CHECK ([DiscountValue] > 0 AND ([DiscountType] = N'Fixed' OR [DiscountValue] <= 100)),
    CONSTRAINT [CK_Vouchers_UsedCount] CHECK ([UsageLimit] IS NULL OR [UsedCount] <= [UsageLimit]),
    CONSTRAINT [CK_Vouchers_Period] CHECK ([StartAt] < [EndAt])
);
GO

/* =========================================================
   9. Orders
   Handles: checkout, payment (COD / online), voucher usage,
   order history, order detail, order tracking, cancel order
   and admin order management.
   Receiver/address columns are a snapshot taken at checkout.
   PaymentExpiresAt: payment deadline of an Online (VietQR) order;
   NULL for COD. Unpaid orders past it are canceled automatically.
   TransactionCode: bank reference of the transfer that paid the order.
   ========================================================= */
CREATE TABLE [dbo].[Orders] (
    [OrderId] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [OrderCode] NVARCHAR(50) NOT NULL,
    [VoucherId] INT NULL,
    [ReceiverName] NVARCHAR(150) NOT NULL,
    [ReceiverPhone] NVARCHAR(20) NOT NULL,
    [ShippingAddress] NVARCHAR(500) NOT NULL,
    [OrderStatus] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Orders_OrderStatus] DEFAULT N'Pending',
    [PaymentMethod] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Orders_PaymentMethod] DEFAULT N'COD',
    [PaymentStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Orders_PaymentStatus] DEFAULT N'Unpaid',
    [TransactionCode] NVARCHAR(100) NULL,
    [SubtotalAmount] DECIMAL(18,2) NOT NULL,
    [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_DiscountAmount] DEFAULT 0,
    [ShippingFee] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Orders_ShippingFee] DEFAULT 0,
    [TotalAmount] DECIMAL(18,2) NOT NULL,
    [CustomerNote] NVARCHAR(500) NULL,
    [CancelReason] NVARCHAR(500) NULL,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Orders_CreatedAt] DEFAULT SYSDATETIME(),
    [PaymentExpiresAt] DATETIME2 NULL,
    [PaidAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,
    [CanceledAt] DATETIME2 NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
    CONSTRAINT [UQ_Orders_OrderCode] UNIQUE ([OrderCode]),
    CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]),
    CONSTRAINT [FK_Orders_Vouchers] FOREIGN KEY ([VoucherId]) REFERENCES [dbo].[Vouchers]([VoucherId]),
    CONSTRAINT [CK_Orders_OrderStatus] CHECK ([OrderStatus] IN (N'Pending', N'Confirmed', N'Shipping', N'Completed', N'Canceled')),
    CONSTRAINT [CK_Orders_PaymentMethod] CHECK ([PaymentMethod] IN (N'COD', N'Online')),
    CONSTRAINT [CK_Orders_PaymentStatus] CHECK ([PaymentStatus] IN (N'Unpaid', N'Paid', N'Failed', N'Refunded')),
    CONSTRAINT [CK_Orders_Amounts] CHECK ([SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [ShippingFee] >= 0 AND [TotalAmount] >= 0),
    CONSTRAINT [CK_Orders_PaymentExpiresAt] CHECK ([PaymentExpiresAt] IS NULL OR [PaymentMethod] = N'Online')
);
GO

/* =========================================================
   10. OrderItems
   Handles: products inside an order. Snapshot columns keep old
   order data correct even if product information changes later.
   ========================================================= */
CREATE TABLE [dbo].[OrderItems] (
    [OrderItemId] INT IDENTITY(1,1) NOT NULL,
    [OrderId] INT NOT NULL,
    [VariantId] INT NOT NULL,
    [ProductNameSnapshot] NVARCHAR(200) NOT NULL,
    [SizeSnapshot] NVARCHAR(20) NULL,
    [ColorSnapshot] NVARCHAR(50) NULL,
    [ImageUrlSnapshot] NVARCHAR(500) NULL,
    [UnitPrice] DECIMAL(18,2) NOT NULL,
    [Quantity] INT NOT NULL,
    [LineTotal] AS ([UnitPrice] * [Quantity]) PERSISTED,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([OrderItemId]),
    CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([OrderId]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_ProductVariants] FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariants]([VariantId]),
    CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_OrderItems_UnitPrice] CHECK ([UnitPrice] >= 0)
);
GO

/* =========================================================
   11. Reviews
   Handles: rating and comment for a purchased order item
   (one review per order item).
   ========================================================= */
CREATE TABLE [dbo].[Reviews] (
    [ReviewId] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [ProductId] INT NOT NULL,
    [OrderItemId] INT NOT NULL,
    [Rating] TINYINT NOT NULL,
    [Comment] NVARCHAR(1000) NULL,
    [IsVisible] BIT NOT NULL CONSTRAINT [DF_Reviews_IsVisible] DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Reviews_CreatedAt] DEFAULT SYSDATETIME(),
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([ReviewId]),
    CONSTRAINT [UQ_Reviews_OrderItem] UNIQUE ([OrderItemId]),
    CONSTRAINT [FK_Reviews_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]),
    CONSTRAINT [FK_Reviews_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]),
    CONSTRAINT [FK_Reviews_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems]([OrderItemId]),
    CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] BETWEEN 1 AND 5)
);
GO

/* =========================================================
   12. Wishlists
   Handles: add / remove / view favorite products.
   ========================================================= */
CREATE TABLE [dbo].[Wishlists] (
    [UserId] INT NOT NULL,
    [ProductId] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Wishlists_CreatedAt] DEFAULT SYSDATETIME(),
    CONSTRAINT [PK_Wishlists] PRIMARY KEY ([UserId], [ProductId]),
    CONSTRAINT [FK_Wishlists_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Wishlists_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]) ON DELETE CASCADE
);
GO

/* =========================================================
   13. PaymentTransactions
   Handles: every verified incoming VietQR transfer reported by the
   bank webhook service. BankReference is unique, so a repeated
   webhook can never be processed twice.
   MatchResult: outcome of automatic matching.
     Matched         - the order was marked Paid
     NoOrder         - no OrderCode found in the transfer content
     Underpaid       - Amount < Orders.TotalAmount
     OrderNotPayable - order already Paid, Canceled (e.g. expired) or COD
   Resolution: staff decision for a transfer that was not Matched
   (NULL = waiting for staff).
     Applied  - applied to an order, which became Paid
     Refunded - money returned to the sender outside Daisy
     Ignored  - no action needed (e.g. test or internal transfer)
   ========================================================= */
CREATE TABLE [dbo].[PaymentTransactions] (
    [PaymentTransactionId] INT IDENTITY(1,1) NOT NULL,
    [BankReference] NVARCHAR(100) NOT NULL,
    [Provider] NVARCHAR(30) NOT NULL,
    [AccountNumber] NVARCHAR(30) NOT NULL,
    [Amount] DECIMAL(18,2) NOT NULL,
    [TransferContent] NVARCHAR(500) NULL,
    [TransferredAt] DATETIME2 NOT NULL,
    [ReceivedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentTransactions_ReceivedAt] DEFAULT SYSDATETIME(),
    [OrderId] INT NULL,
    [MatchResult] NVARCHAR(30) NOT NULL,
    [Resolution] NVARCHAR(30) NULL,
    [ResolutionNote] NVARCHAR(500) NULL,
    [ResolvedByUserId] INT NULL,
    [ResolvedAt] DATETIME2 NULL,
    CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([PaymentTransactionId]),
    CONSTRAINT [UQ_PaymentTransactions_BankReference] UNIQUE ([BankReference]),
    CONSTRAINT [FK_PaymentTransactions_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([OrderId]),
    CONSTRAINT [FK_PaymentTransactions_Users] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [dbo].[Users]([UserId]),
    CONSTRAINT [CK_PaymentTransactions_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_PaymentTransactions_MatchResult] CHECK ([MatchResult] IN (N'Matched', N'NoOrder', N'Underpaid', N'OrderNotPayable')),
    CONSTRAINT [CK_PaymentTransactions_Resolution] CHECK ([Resolution] IS NULL OR ([Resolution] IN (N'Applied', N'Refunded', N'Ignored') AND [MatchResult] <> N'Matched')),
    CONSTRAINT [CK_PaymentTransactions_Resolved] CHECK (
        ([Resolution] IS NULL AND [ResolvedAt] IS NULL AND [ResolvedByUserId] IS NULL)
        OR ([Resolution] IS NOT NULL AND [ResolvedAt] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL)),
    CONSTRAINT [CK_PaymentTransactions_Matched] CHECK ([MatchResult] <> N'Matched' OR [OrderId] IS NOT NULL),
    CONSTRAINT [CK_PaymentTransactions_Applied] CHECK ([Resolution] IS NULL OR [Resolution] <> N'Applied' OR [OrderId] IS NOT NULL)
);
GO

/* =========================================================
   Indexes for common queries
   ========================================================= */
CREATE INDEX [IX_UserAddresses_UserId] ON [dbo].[UserAddresses]([UserId]);
CREATE UNIQUE INDEX [UX_UserAddresses_Default] ON [dbo].[UserAddresses]([UserId]) WHERE [IsDefault] = 1;
CREATE INDEX [IX_Categories_ParentCategoryId] ON [dbo].[Categories]([ParentCategoryId]);
CREATE INDEX [IX_Products_CategoryId] ON [dbo].[Products]([CategoryId]) WHERE [IsActive] = 1;
CREATE INDEX [IX_Products_CreatedAt] ON [dbo].[Products]([CreatedAt] DESC) WHERE [IsActive] = 1;
CREATE INDEX [IX_Products_SoldCount] ON [dbo].[Products]([SoldCount] DESC) WHERE [IsActive] = 1;
CREATE INDEX [IX_ProductImages_ProductId] ON [dbo].[ProductImages]([ProductId]);
CREATE UNIQUE INDEX [UX_CartItems_User_Variant] ON [dbo].[CartItems]([UserId], [VariantId]) WHERE [UserId] IS NOT NULL;
CREATE UNIQUE INDEX [UX_CartItems_Session_Variant] ON [dbo].[CartItems]([SessionId], [VariantId]) WHERE [SessionId] IS NOT NULL;
CREATE INDEX [IX_Orders_UserId] ON [dbo].[Orders]([UserId], [CreatedAt] DESC);
CREATE INDEX [IX_Orders_OrderStatus] ON [dbo].[Orders]([OrderStatus], [CreatedAt] DESC);
CREATE UNIQUE INDEX [UX_Orders_TransactionCode] ON [dbo].[Orders]([TransactionCode]) WHERE [TransactionCode] IS NOT NULL;
CREATE INDEX [IX_Orders_PaymentExpiresAt] ON [dbo].[Orders]([PaymentExpiresAt]) WHERE [PaymentExpiresAt] IS NOT NULL AND [PaymentStatus] = N'Unpaid' AND [OrderStatus] = N'Pending';
CREATE INDEX [IX_OrderItems_OrderId] ON [dbo].[OrderItems]([OrderId]);
CREATE INDEX [IX_Reviews_ProductId] ON [dbo].[Reviews]([ProductId]) WHERE [IsVisible] = 1;
CREATE INDEX [IX_Wishlists_ProductId] ON [dbo].[Wishlists]([ProductId]);
CREATE INDEX [IX_PaymentTransactions_OrderId] ON [dbo].[PaymentTransactions]([OrderId]) WHERE [OrderId] IS NOT NULL;
CREATE INDEX [IX_PaymentTransactions_NeedsReview] ON [dbo].[PaymentTransactions]([ReceivedAt] DESC) WHERE [MatchResult] <> N'Matched' AND [Resolution] IS NULL;
-- Foreign key lookups (also created by the EF Core model).
CREATE INDEX [IX_ProductImages_VariantId] ON [dbo].[ProductImages]([VariantId]);
CREATE INDEX [IX_CartItems_VariantId] ON [dbo].[CartItems]([VariantId]);
CREATE INDEX [IX_Orders_VoucherId] ON [dbo].[Orders]([VoucherId]);
CREATE INDEX [IX_OrderItems_VariantId] ON [dbo].[OrderItems]([VariantId]);
CREATE INDEX [IX_Reviews_UserId] ON [dbo].[Reviews]([UserId]);
CREATE INDEX [IX_PaymentTransactions_ResolvedByUserId] ON [dbo].[PaymentTransactions]([ResolvedByUserId]);
GO

PRINT N'Daisy database created successfully (13 tables, optimized).';
GO
