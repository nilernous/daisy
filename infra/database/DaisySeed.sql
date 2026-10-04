/*
Daisy - development seed data
Run AFTER DaisyDB.sql (or `dotnet ef database update`) on an EMPTY [Daisy] database.

Accounts (registration on the website always creates a Customer, BR-07,
so the Staff and Admin accounts exist only through this seed or an Admin role change):

    Role      Email                  Password
    Admin     admin@daisy.local      Admin@123
    Staff     staff@daisy.local      Staff@123
    Customer  customer@daisy.local   Customer@123

Change these passwords before using the data anywhere but a local machine.

Contents: 11 categories, 12 products with variants (3 on flash sale, some low on stock),
3 vouchers, 3 orders of the sample customer (2 completed, 1 shipping), 1 review,
1 matched and 1 unmatched bank transfer (for the reconciliation screen, UC-71).
Times are relative to SYSDATETIME(), so sales and orders look current whenever the script runs.
Products have no images; upload them in the admin panel (UC-48).
*/

USE [Daisy];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM [dbo].[Users]) OR EXISTS (SELECT 1 FROM [dbo].[Products])
BEGIN
    RAISERROR(N'The database already has data. Run DaisySeed.sql only on an empty database.', 16, 1);
    SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

DECLARE @now DATETIME2 = SYSDATETIME();

/* Users */
SET IDENTITY_INSERT [dbo].[Users] ON;
INSERT INTO [dbo].[Users] ([UserId], [FullName], [Email], [PasswordHash], [Phone], [Role], [Status], [Gender], [DateOfBirth], [CreatedAt]) VALUES
    (1, N'Quản trị Daisy', N'admin@daisy.local', N'AQAAAAIAAYagAAAAEMraAfbPBle6wNyMYSO8v+FK89I0IawOgKYFOyBSRjl2OgFUuJn0wd7aZzKWYkUNLQ==', N'0900000001', N'Admin', N'Active', NULL, NULL, DATEADD(DAY, -90, @now)),
    (2, N'Nhân viên Daisy', N'staff@daisy.local', N'AQAAAAIAAYagAAAAEDbjrqnYBaLxuSdCFpeANGMG/KP6YVqRnLDR0+CfU+HnfOBIELYq5yMI/avlVg36Ig==', N'0900000002', N'Staff', N'Active', NULL, NULL, DATEADD(DAY, -60, @now)),
    (3, N'Nguyễn Văn An', N'customer@daisy.local', N'AQAAAAIAAYagAAAAEEdPemGgrSGvONEMG2XIP9TLTbC0M/fl63RpeOe+dhcGPVBqYd8skO9+jWGS4C3jmA==', N'0901234567', N'Customer', N'Active', N'Male', '1998-05-20', DATEADD(DAY, -14, @now));
SET IDENTITY_INSERT [dbo].[Users] OFF;

/* Address book of the sample customer */
INSERT INTO [dbo].[UserAddresses] ([UserId], [ReceiverName], [ReceiverPhone], [Province], [District], [Ward], [StreetAddress], [IsDefault], [CreatedAt]) VALUES
    (3, N'Nguyễn Văn An', N'0901234567', N'TP. Hồ Chí Minh', N'Quận 1', N'Phường Bến Nghé', N'12 Lê Lợi', 1, DATEADD(DAY, -14, @now)),
    (3, N'Trần Thị Bình', N'0912345678', N'Hà Nội', N'Quận Hoàn Kiếm', N'Phường Hàng Bạc', N'45 Hàng Bạc', 0, DATEADD(DAY, -7, @now));

/* Categories */
SET IDENTITY_INSERT [dbo].[Categories] ON;
INSERT INTO [dbo].[Categories] ([CategoryId], [ParentCategoryId], [CategoryName], [Slug], [SortOrder], [IsActive]) VALUES
    (1, NULL, N'Nam', N'nam', 0, 1),
    (2, NULL, N'Nữ', N'nu', 1, 1),
    (3, NULL, N'Phụ kiện', N'phu-kien', 2, 1),
    (4, 1, N'Áo thun nam', N'nam-ao-thun', 0, 1),
    (5, 1, N'Áo sơ mi nam', N'nam-ao-so-mi', 1, 1),
    (6, 1, N'Quần nam', N'nam-quan', 2, 1),
    (7, 2, N'Váy & đầm', N'nu-vay-dam', 0, 1),
    (8, 2, N'Áo nữ', N'nu-ao', 1, 1),
    (9, 2, N'Quần nữ', N'nu-quan', 2, 1),
    (10, 3, N'Túi xách', N'phu-kien-tui', 0, 1),
    (11, 3, N'Mũ nón', N'phu-kien-mu', 1, 1);
SET IDENTITY_INSERT [dbo].[Categories] OFF;

/* Products. Flash sales: products 1, 5 and 10 (BR-14); product 4 has a permanent sale price. */
SET IDENTITY_INSERT [dbo].[Products] ON;
INSERT INTO [dbo].[Products] ([ProductId], [CategoryId], [ProductName], [Slug], [Brand], [Description], [Material], [GenderTarget], [BasePrice], [SalePrice], [SaleStartAt], [SaleEndAt], [AverageRating], [ReviewCount], [SoldCount], [IsFeatured], [IsActive], [CreatedAt]) VALUES
    (1, 4, N'Áo thun cotton basic', N'ao-thun-cotton-basic', N'Daisy', N'Áo thun cổ tròn dệt từ cotton 100%, mềm và thấm hút tốt. Form suông vừa, mặc hằng ngày hay phối áo khoác đều hợp.', N'Cotton 100%', N'Unisex', 199000, 149000, DATEADD(DAY, -1, @now), DATEADD(DAY, 3, @now), 5.00, 1, 2, 1, 1, DATEADD(DAY, -30, @now)),
    (2, 4, N'Áo thun oversize in hoa cúc', N'ao-thun-oversize-hoa-cuc', N'Daisy', N'Form oversize rộng rãi, hình in hoa cúc thêu nổi ở ngực trái. Vải dày vừa, không bai cổ sau nhiều lần giặt.', N'Cotton 100%', N'Unisex', 259000, NULL, NULL, NULL, 0, 0, 0, 1, 1, DATEADD(DAY, -5, @now)),
    (3, 5, N'Áo sơ mi linen tay dài', N'ao-so-mi-linen-tay-dai', N'Daisy Studio', N'Sơ mi linen thoáng mát cho mùa hè, cổ bẻ, cúc gỗ. Có thể mặc sơ vin hoặc để ngoài.', N'Linen', N'Men', 449000, NULL, NULL, NULL, 0, 0, 1, 0, 1, DATEADD(DAY, -20, @now)),
    (4, 6, N'Quần kaki ống đứng', N'quan-kaki-ong-dung', N'Daisy', N'Quần kaki ống đứng, co giãn nhẹ, cạp vừa. Dễ phối với áo thun lẫn sơ mi.', N'Kaki co giãn', N'Men', 399000, 339000, NULL, NULL, 0, 0, 0, 0, 1, DATEADD(DAY, -40, @now)),
    (5, 7, N'Đầm midi hoa nhí', N'dam-midi-hoa-nhi', N'Daisy Studio', N'Đầm midi voan hai lớp, họa tiết hoa nhí, eo chun, tay phồng nhẹ. Dài qua gối.', N'Voan', N'Women', 559000, 449000, DATEADD(DAY, -2, @now), DATEADD(DAY, 2, @now), 0, 0, 0, 1, 1, DATEADD(DAY, -3, @now)),
    (6, 8, N'Áo kiểu cổ nơ', N'ao-kieu-co-no', N'Daisy', N'Áo lụa cổ thắt nơ, tay dài bo chun, phù hợp đi làm.', N'Lụa', N'Women', 329000, NULL, NULL, NULL, 0, 0, 0, 0, 1, DATEADD(DAY, -12, @now)),
    (7, 9, N'Quần jean ống rộng', N'quan-jean-ong-rong', N'Daisy Denim', N'Jean ống rộng lưng cao, wash nhạt, tôn dáng và che khuyết điểm chân.', N'Denim', N'Women', 489000, NULL, NULL, NULL, 0, 0, 1, 1, 1, DATEADD(DAY, -25, @now)),
    (8, 7, N'Chân váy xếp ly', N'chan-vay-xep-ly', N'Daisy', N'Chân váy xếp ly dáng chữ A, dài ngang bắp chân, có lót trong.', N'Polyester', N'Women', 289000, NULL, NULL, NULL, 0, 0, 0, 0, 1, DATEADD(DAY, -1, @now)),
    (9, 10, N'Túi tote canvas', N'tui-tote-canvas', N'Daisy', N'Túi tote canvas dày, có ngăn trong và khóa kéo, vừa laptop 14 inch.', N'Canvas', N'Unisex', 179000, NULL, NULL, NULL, 0, 0, 1, 0, 1, DATEADD(DAY, -15, @now)),
    (10, 11, N'Mũ bucket kaki', N'mu-bucket-kaki', N'Daisy', N'Mũ bucket vành vừa, vải kaki đứng form, chống nắng tốt.', N'Kaki', N'Unisex', 149000, 99000, DATEADD(DAY, 0, @now), DATEADD(DAY, 5, @now), 0, 0, 0, 0, 1, DATEADD(DAY, -8, @now)),
    (11, 5, N'Áo sơ mi oxford', N'ao-so-mi-oxford', N'Daisy', N'Sơ mi oxford cổ button-down, dày dặn, giữ form tốt.', N'Oxford cotton', N'Men', 389000, NULL, NULL, NULL, 0, 0, 0, 0, 1, DATEADD(DAY, -2, @now)),
    (12, 8, N'Áo len dệt kim mỏng', N'ao-len-det-kim', N'Daisy', N'Áo len dệt kim mỏng, cổ tròn, mặc được cả trong phòng máy lạnh.', N'Len pha cotton', N'Women', 359000, NULL, NULL, NULL, 0, 0, 0, 0, 1, DATEADD(DAY, -60, @now));
SET IDENTITY_INSERT [dbo].[Products] OFF;

/* Variants (BR-12: unique SKU and size + color per product; XL costs 20.000 more) */
SET IDENTITY_INSERT [dbo].[ProductVariants] ON;
INSERT INTO [dbo].[ProductVariants] ([VariantId], [ProductId], [Sku], [Size], [Color], [ColorHex], [ExtraPrice], [StockQuantity], [LowStockThreshold], [IsActive]) VALUES
    (1, 1, N'TS-BASIC-S-WHT', N'S', N'Trắng', N'#F5F5F2', 0, 11, 5, 1),
    (2, 1, N'TS-BASIC-M-WHT', N'M', N'Trắng', N'#F5F5F2', 0, 16, 5, 1),
    (3, 1, N'TS-BASIC-L-WHT', N'L', N'Trắng', N'#F5F5F2', 0, 21, 5, 1),
    (4, 1, N'TS-BASIC-XL-WHT', N'XL', N'Trắng', N'#F5F5F2', 20000, 26, 5, 1),
    (5, 1, N'TS-BASIC-S-BLK', N'S', N'Đen', N'#1C1C1C', 0, 22, 5, 1),
    (6, 1, N'TS-BASIC-M-BLK', N'M', N'Đen', N'#1C1C1C', 0, 27, 5, 1),
    (7, 1, N'TS-BASIC-L-BLK', N'L', N'Đen', N'#1C1C1C', 0, 32, 5, 1),
    (8, 1, N'TS-BASIC-XL-BLK', N'XL', N'Đen', N'#1C1C1C', 20000, 37, 5, 1),
    (9, 1, N'TS-BASIC-S-NVY', N'S', N'Xanh navy', N'#22304A', 0, 3, 5, 1),
    (10, 1, N'TS-BASIC-M-NVY', N'M', N'Xanh navy', N'#22304A', 0, 38, 5, 1),
    (11, 1, N'TS-BASIC-L-NVY', N'L', N'Xanh navy', N'#22304A', 0, 6, 5, 1),
    (12, 1, N'TS-BASIC-XL-NVY', N'XL', N'Xanh navy', N'#22304A', 20000, 11, 5, 1),
    (13, 2, N'TS-DAISY-M-WHT', N'M', N'Trắng', N'#F5F5F2', 0, 18, 5, 1),
    (14, 2, N'TS-DAISY-L-WHT', N'L', N'Trắng', N'#F5F5F2', 0, 23, 5, 1),
    (15, 2, N'TS-DAISY-XL-WHT', N'XL', N'Trắng', N'#F5F5F2', 20000, 28, 5, 1),
    (16, 2, N'TS-DAISY-M-YLW', N'M', N'Vàng cúc', N'#F2C230', 0, 29, 5, 1),
    (17, 2, N'TS-DAISY-L-YLW', N'L', N'Vàng cúc', N'#F2C230', 0, 34, 5, 1),
    (18, 2, N'TS-DAISY-XL-YLW', N'XL', N'Vàng cúc', N'#F2C230', 20000, 39, 5, 1),
    (19, 3, N'SM-LINEN-M-WHT', N'M', N'Trắng', N'#F5F5F2', 0, 25, 5, 1),
    (20, 3, N'SM-LINEN-L-WHT', N'L', N'Trắng', N'#F5F5F2', 0, 30, 5, 1),
    (21, 3, N'SM-LINEN-XL-WHT', N'XL', N'Trắng', N'#F5F5F2', 20000, 35, 5, 1),
    (22, 3, N'SM-LINEN-M-BEI', N'M', N'Be', N'#D9C8A9', 0, 36, 5, 1),
    (23, 3, N'SM-LINEN-L-BEI', N'L', N'Be', N'#D9C8A9', 0, 4, 5, 1),
    (24, 3, N'SM-LINEN-XL-BEI', N'XL', N'Be', N'#D9C8A9', 20000, 9, 5, 1),
    (25, 4, N'QK-STRAIGHT-29-BEI', N'29', N'Be', N'#D9C8A9', 0, 32, 5, 1),
    (26, 4, N'QK-STRAIGHT-30-BEI', N'30', N'Be', N'#D9C8A9', 0, 37, 5, 1),
    (27, 4, N'QK-STRAIGHT-31-BEI', N'31', N'Be', N'#D9C8A9', 0, 5, 5, 1),
    (28, 4, N'QK-STRAIGHT-32-BEI', N'32', N'Be', N'#D9C8A9', 0, 10, 5, 1),
    (29, 4, N'QK-STRAIGHT-29-NVY', N'29', N'Xanh navy', N'#22304A', 0, 6, 5, 1),
    (30, 4, N'QK-STRAIGHT-30-NVY', N'30', N'Xanh navy', N'#22304A', 0, 11, 5, 1),
    (31, 4, N'QK-STRAIGHT-31-NVY', N'31', N'Xanh navy', N'#22304A', 0, 16, 5, 1),
    (32, 4, N'QK-STRAIGHT-32-NVY', N'32', N'Xanh navy', N'#22304A', 0, 21, 5, 1),
    (33, 5, N'DM-FLORAL-S-YLW', N'S', N'Vàng cúc', N'#F2C230', 0, 39, 5, 1),
    (34, 5, N'DM-FLORAL-M-YLW', N'M', N'Vàng cúc', N'#F2C230', 0, 7, 5, 1),
    (35, 5, N'DM-FLORAL-L-YLW', N'L', N'Vàng cúc', N'#F2C230', 0, 12, 5, 1),
    (36, 5, N'DM-FLORAL-S-PNK', N'S', N'Hồng phấn', N'#E8B4B8', 0, 13, 5, 1),
    (37, 5, N'DM-FLORAL-M-PNK', N'M', N'Hồng phấn', N'#E8B4B8', 0, 18, 5, 1),
    (38, 5, N'DM-FLORAL-L-PNK', N'L', N'Hồng phấn', N'#E8B4B8', 0, 4, 5, 1),
    (39, 6, N'AK-BOW-S-WHT', N'S', N'Trắng', N'#F5F5F2', 0, 9, 5, 1),
    (40, 6, N'AK-BOW-M-WHT', N'M', N'Trắng', N'#F5F5F2', 0, 14, 5, 1),
    (41, 6, N'AK-BOW-L-WHT', N'L', N'Trắng', N'#F5F5F2', 0, 19, 5, 1),
    (42, 6, N'AK-BOW-S-PNK', N'S', N'Hồng phấn', N'#E8B4B8', 0, 20, 5, 1),
    (43, 6, N'AK-BOW-M-PNK', N'M', N'Hồng phấn', N'#E8B4B8', 0, 25, 5, 1),
    (44, 6, N'AK-BOW-L-PNK', N'L', N'Hồng phấn', N'#E8B4B8', 0, 30, 5, 1),
    (45, 7, N'QJ-WIDE-S-DNM', N'S', N'Xanh denim', N'#5B7BA6', 0, 16, 5, 1),
    (46, 7, N'QJ-WIDE-M-DNM', N'M', N'Xanh denim', N'#5B7BA6', 0, 21, 5, 1),
    (47, 7, N'QJ-WIDE-L-DNM', N'L', N'Xanh denim', N'#5B7BA6', 0, 26, 5, 1),
    (48, 7, N'QJ-WIDE-S-BLK', N'S', N'Đen', N'#1C1C1C', 0, 27, 5, 1),
    (49, 7, N'QJ-WIDE-M-BLK', N'M', N'Đen', N'#1C1C1C', 0, 3, 5, 1),
    (50, 7, N'QJ-WIDE-L-BLK', N'L', N'Đen', N'#1C1C1C', 0, 37, 5, 1),
    (51, 8, N'CV-PLEAT-S-BEI', N'S', N'Be', N'#D9C8A9', 0, 23, 5, 1),
    (52, 8, N'CV-PLEAT-M-BEI', N'M', N'Be', N'#D9C8A9', 0, 28, 5, 1),
    (53, 8, N'CV-PLEAT-S-BLK', N'S', N'Đen', N'#1C1C1C', 0, 34, 5, 1),
    (54, 8, N'CV-PLEAT-M-BLK', N'M', N'Đen', N'#1C1C1C', 0, 39, 5, 1),
    (55, 9, N'TT-CANVAS-BEI', NULL, N'Be', N'#D9C8A9', 0, 30, 5, 1),
    (56, 9, N'TT-CANVAS-BLK', NULL, N'Đen', N'#1C1C1C', 0, 4, 5, 1),
    (57, 10, N'MB-KAKI-BEI', NULL, N'Be', N'#D9C8A9', 0, 37, 5, 1),
    (58, 10, N'MB-KAKI-OLV', NULL, N'Xanh rêu', N'#5B6B47', 0, 3, 5, 1),
    (59, 10, N'MB-KAKI-BLK', NULL, N'Đen', N'#1C1C1C', 0, 22, 5, 1),
    (60, 11, N'SM-OXFORD-M-WHT', N'M', N'Trắng', N'#F5F5F2', 0, 7, 5, 1),
    (61, 11, N'SM-OXFORD-L-WHT', N'L', N'Trắng', N'#F5F5F2', 0, 12, 5, 1),
    (62, 11, N'SM-OXFORD-XL-WHT', N'XL', N'Trắng', N'#F5F5F2', 20000, 17, 5, 1),
    (63, 11, N'SM-OXFORD-M-NVY', N'M', N'Xanh navy', N'#22304A', 0, 18, 5, 1),
    (64, 11, N'SM-OXFORD-L-NVY', N'L', N'Xanh navy', N'#22304A', 0, 23, 5, 1),
    (65, 11, N'SM-OXFORD-XL-NVY', N'XL', N'Xanh navy', N'#22304A', 20000, 28, 5, 1),
    (66, 12, N'AL-KNIT-S-BEI', N'S', N'Be', N'#D9C8A9', 0, 14, 5, 1),
    (67, 12, N'AL-KNIT-M-BEI', N'M', N'Be', N'#D9C8A9', 0, 19, 5, 1),
    (68, 12, N'AL-KNIT-L-BEI', N'L', N'Be', N'#D9C8A9', 0, 24, 5, 1),
    (69, 12, N'AL-KNIT-S-OLV', N'S', N'Xanh rêu', N'#5B6B47', 0, 25, 5, 1),
    (70, 12, N'AL-KNIT-M-OLV', N'M', N'Xanh rêu', N'#5B6B47', 0, 30, 5, 1),
    (71, 12, N'AL-KNIT-L-OLV', N'L', N'Xanh rêu', N'#5B6B47', 0, 35, 5, 1);
SET IDENTITY_INSERT [dbo].[ProductVariants] OFF;

/* Vouchers (BR-25). DAISY10 was used once by order 2. */
SET IDENTITY_INSERT [dbo].[Vouchers] ON;
INSERT INTO [dbo].[Vouchers] ([VoucherId], [Code], [Name], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartAt], [EndAt], [IsActive]) VALUES
    (1, N'DAISY10', N'Giảm 10% tối đa 50.000đ', N'Percent', 10, 50000, 200000, NULL, 1, DATEADD(DAY, -30, @now), DATEADD(DAY, 60, @now), 1),
    (2, N'FREESHIP30', N'Giảm 30.000đ cho đơn từ 300.000đ', N'Fixed', 30000, NULL, 300000, 500, 0, DATEADD(DAY, -7, @now), DATEADD(DAY, 30, @now), 1),
    (3, N'WELCOME50K', N'Chào bạn mới: giảm 50.000đ', N'Fixed', 50000, NULL, 500000, 100, 0, DATEADD(DAY, -1, @now), DATEADD(DAY, 90, @now), 1);
SET IDENTITY_INSERT [dbo].[Vouchers] OFF;

/* Orders of the sample customer, with snapshots (BR-28) */
SET IDENTITY_INSERT [dbo].[Orders] ON;
INSERT INTO [dbo].[Orders] ([OrderId], [UserId], [OrderCode], [VoucherId], [ReceiverName], [ReceiverPhone], [ShippingAddress], [OrderStatus], [PaymentMethod], [PaymentStatus], [TransactionCode], [SubtotalAmount], [DiscountAmount], [ShippingFee], [TotalAmount], [CreatedAt], [PaidAt], [CompletedAt]) VALUES
    (1, 3, N'DS' + FORMAT(DATEADD(DAY, -10, @now), 'yyMMdd') + N'A7K2', NULL, N'Nguyễn Văn An', N'0901234567', N'12 Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', N'Completed', N'COD', N'Paid', NULL, 398000, 0, 30000, 428000, DATEADD(DAY, -10, @now), DATEADD(DAY, -7, @now), DATEADD(DAY, -7, @now)),
    (2, 3, N'DS' + FORMAT(DATEADD(DAY, -6, @now), 'yyMMdd') + N'Q9MZ', 1, N'Nguyễn Văn An', N'0901234567', N'12 Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', N'Completed', N'Online', N'Paid', N'FT26SEED0001', 668000, 50000, 30000, 648000, DATEADD(DAY, -6, @now), DATEADD(DAY, -6, @now), DATEADD(DAY, -3, @now)),
    (3, 3, N'DS' + FORMAT(DATEADD(DAY, -1, @now), 'yyMMdd') + N'H3XP', NULL, N'Nguyễn Văn An', N'0901234567', N'12 Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', N'Shipping', N'COD', N'Unpaid', NULL, 449000, 0, 30000, 479000, DATEADD(DAY, -1, @now), NULL, NULL);
SET IDENTITY_INSERT [dbo].[Orders] OFF;

SET IDENTITY_INSERT [dbo].[OrderItems] ON;
INSERT INTO [dbo].[OrderItems] ([OrderItemId], [OrderId], [VariantId], [ProductNameSnapshot], [SizeSnapshot], [ColorSnapshot], [ImageUrlSnapshot], [UnitPrice], [Quantity]) VALUES
    (1, 1, 2, N'Áo thun cotton basic', N'M', N'Trắng', NULL, 199000, 2),
    (2, 2, 46, N'Quần jean ống rộng', N'M', N'Xanh denim', NULL, 489000, 1),
    (3, 2, 55, N'Túi tote canvas', NULL, N'Be', NULL, 179000, 1),
    (4, 3, 20, N'Áo sơ mi linen tay dài', N'L', N'Trắng', NULL, 449000, 1);
SET IDENTITY_INSERT [dbo].[OrderItems] OFF;

/* One review on order 1 (BR-37); product 1 rating = 5.00 from 1 visible review (BR-38) */
INSERT INTO [dbo].[Reviews] ([UserId], [ProductId], [OrderItemId], [Rating], [Comment], [IsVisible], [CreatedAt]) VALUES
    (3, 1, 1, 5, N'Vải mát, mặc rất thoải mái. Giao hàng nhanh.', 1, DATEADD(DAY, -6, @now));

/* Bank transfers (BR-42): one matched to order 2, one unmatched waiting for staff (UC-71) */
INSERT INTO [dbo].[PaymentTransactions] ([BankReference], [Provider], [AccountNumber], [Amount], [TransferContent], [TransferredAt], [ReceivedAt], [OrderId], [MatchResult]) VALUES
    (N'FT26SEED0001', N'Mock', N'0123456789', 648000, (SELECT N'Thanh toan ' + [OrderCode] FROM [dbo].[Orders] WHERE [OrderId] = 2), DATEADD(DAY, -6, @now), DATEADD(DAY, -6, @now), 2, N'Matched'),
    (N'FT26SEED0002', N'Mock', N'0123456789', 150000, N'chuyen khoan mua ao', DATEADD(DAY, -2, @now), DATEADD(DAY, -2, @now), NULL, N'NoOrder');

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO

PRINT N'Daisy seed data inserted.';
GO
