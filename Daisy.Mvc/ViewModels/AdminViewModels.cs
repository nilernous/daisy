using System.ComponentModel.DataAnnotations;
using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;

namespace Daisy.Mvc.ViewModels;

// ---- Dashboard -----------------------------------------------------------------------------

/// <summary>One mark of a bar or column chart: label, value, and the text shown for it.</summary>
public sealed record ChartPoint(string Label, decimal Value, string Display, string? Detail = null);

public sealed record ColumnChartModel(string Title, string Unit, IReadOnlyList<ChartPoint> Points, Func<decimal, string> FormatTick, bool WholeNumbers = false)
{
    /// <summary>A clean axis maximum with four equal steps (0, ¼, ½, ¾, max).</summary>
    public (decimal Max, decimal Step) Scale()
    {
        var max = Points.Count == 0 ? 0 : Points.Max(p => p.Value);
        if (max <= 0)
        {
            return (4, 1);
        }

        var rough = (double)max / 4;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(f => f * magnitude).First(s => s >= rough);
        if (WholeNumbers)
        {
            // Counts: steps of at least 1, never 0.25 of a customer.
            step = Math.Max(1, Math.Ceiling(step));
        }
        return ((decimal)(step * 4), (decimal)step);
    }
}

public sealed record BarListModel(string Title, IReadOnlyList<ChartPoint> Points);

public sealed record DashboardViewModel(
    DateOnly From,
    DateOnly To,
    string GroupBy,
    string? Preset,
    decimal Revenue,
    int CompletedOrders,
    int NewOrders,
    int NewCustomers,
    ColumnChartModel RevenueChart,
    BarListModel StatusChart,
    BarListModel TopProducts,
    ColumnChartModel CustomerChart);

// ---- Catalog ---------------------------------------------------------------------------------

public sealed class CategoryForm
{
    [Required(ErrorMessage = "Nhập tên danh mục.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập slug.")]
    [StringLength(200)]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug chỉ gồm chữ thường không dấu, số và dấu gạch ngang.")]
    public string Slug { get; set; } = string.Empty;

    public int? ParentCategoryId { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Range(0, 10000, ErrorMessage = "Thứ tự phải từ 0 trở lên.")]
    public int SortOrder { get; set; }

    public object ToRequest() => new { Name, Slug, ParentCategoryId, ImageUrl, SortOrder };
}

public sealed record CategoriesViewModel(IReadOnlyList<CategoryAdminNode> Tree, CategoryForm Form, int? EditingId);

public sealed class AdminProductFilter
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsFeatured { get; set; }

    public bool? IsOnSale { get; set; }

    public bool LowStock { get; set; }

    public int Page { get; set; } = 1;

    public string ToQuery(int? page = null)
    {
        var parts = new List<string> { $"page={page ?? Page}", "pageSize=20" };
        if (!string.IsNullOrWhiteSpace(Search)) parts.Add("search=" + Uri.EscapeDataString(Search));
        if (CategoryId is not null) parts.Add($"categoryId={CategoryId}");
        if (IsActive is not null) parts.Add($"isActive={IsActive.ToString()!.ToLowerInvariant()}");
        if (IsFeatured is not null) parts.Add($"isFeatured={IsFeatured.ToString()!.ToLowerInvariant()}");
        if (IsOnSale is not null) parts.Add($"isOnSale={IsOnSale.ToString()!.ToLowerInvariant()}");
        if (LowStock) parts.Add("lowStock=true");
        return "?" + string.Join('&', parts);
    }
}

public sealed record ProductsAdminViewModel(AdminProductFilter Filter, PagedResult<ProductAdminListItem> Products, IReadOnlyList<CategoryAdminNode> Categories);

public sealed class ProductForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn danh mục.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Nhập tên sản phẩm.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập slug.")]
    [StringLength(250)]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug chỉ gồm chữ thường không dấu, số và dấu gạch ngang.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Material { get; set; }

    public string? GenderTarget { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Giá gốc phải từ 0 trở lên.")]
    public decimal BasePrice { get; set; }

    public static ProductForm From(ProductAdmin p) => new()
    {
        CategoryId = p.CategoryId,
        Name = p.Name,
        Slug = p.Slug,
        Brand = p.Brand,
        Description = p.Description,
        Material = p.Material,
        GenderTarget = p.GenderTarget,
        BasePrice = p.BasePrice,
    };

    public object ToRequest() => new
    {
        CategoryId,
        Name,
        Slug,
        Brand,
        Description,
        Material,
        GenderTarget = string.IsNullOrEmpty(GenderTarget) ? null : GenderTarget,
        BasePrice,
    };
}

public sealed class VariantForm
{
    [Required(ErrorMessage = "Nhập SKU.")]
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Size { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Mã màu dạng #1A2B3C.")]
    public string? ColorHex { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal ExtraPrice { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Tồn kho phải từ 0 trở lên.")]
    public int StockQuantity { get; set; }

    [Range(0, 1_000_000)]
    public int LowStockThreshold { get; set; } = 5;

    public object ToCreateRequest() => new { Sku, Size, Color, ColorHex = string.IsNullOrEmpty(ColorHex) ? null : ColorHex, ExtraPrice, StockQuantity, LowStockThreshold };

    public object ToUpdateRequest() => new { Sku, Size, Color, ColorHex = string.IsNullOrEmpty(ColorHex) ? null : ColorHex, ExtraPrice, LowStockThreshold };
}

public sealed class SaleForm
{
    [Range(0, 1_000_000_000, ErrorMessage = "Giá khuyến mãi phải từ 0 trở lên.")]
    public decimal SalePrice { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }
}

public sealed record ProductEditViewModel(
    ProductAdmin? Product,
    ProductForm Form,
    VariantForm NewVariant,
    SaleForm Sale,
    IReadOnlyList<CategoryAdminNode> Categories,
    bool IsAdmin);

// ---- Orders and payments ------------------------------------------------------------------------

public sealed class AdminOrderFilter
{
    public OrderStatus? Status { get; set; }

    public PaymentStatus? Payment { get; set; }

    public PaymentMethod? Method { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? Code { get; set; }

    public int Page { get; set; } = 1;

    public ODataQuery ToOData(int pageSize)
    {
        var q = new ODataQuery().OrderBy("createdAt desc").Page(Page, pageSize);
        if (Status is not null) q.Filter($"orderStatus eq '{Status}'");
        if (Payment is not null) q.Filter($"paymentStatus eq '{Payment}'");
        if (Method is not null) q.Filter($"paymentMethod eq '{Method}'");
        if (From is not null) q.Filter($"createdAt ge {From:yyyy-MM-dd}T00:00:00+07:00");
        if (To is not null) q.Filter($"createdAt lt {To.Value.AddDays(1):yyyy-MM-dd}T00:00:00+07:00");
        if (!string.IsNullOrWhiteSpace(Code)) q.Filter($"contains(orderCode,{ODataQuery.Literal(Code.Trim().ToUpperInvariant())}) or contains(tolower(customerName),{ODataQuery.Literal(Code.Trim().ToLowerInvariant())})");
        return q;
    }

    public string PageUrl(int page)
    {
        var parts = new List<string> { $"page={page}" };
        if (Status is not null) parts.Add($"status={Status}");
        if (Payment is not null) parts.Add($"payment={Payment}");
        if (Method is not null) parts.Add($"method={Method}");
        if (From is not null) parts.Add($"from={From:yyyy-MM-dd}");
        if (To is not null) parts.Add($"to={To:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(Code)) parts.Add("code=" + Uri.EscapeDataString(Code));
        return "/admin/orders?" + string.Join('&', parts);
    }
}

public sealed record OrdersAdminViewModel(AdminOrderFilter Filter, IReadOnlyList<OrderAdminListItem> Orders, int TotalCount, int TotalPages);

public sealed record TransactionsViewModel(PagedResult<PaymentTransaction> Transactions, bool NeedsReview, int Page);

// ---- Vouchers ----------------------------------------------------------------------------------------

public sealed class VoucherForm
{
    [Required(ErrorMessage = "Nhập mã.")]
    [RegularExpression("^[A-Za-z0-9]{3,50}$", ErrorMessage = "Mã gồm 3–50 chữ cái hoặc số, không dấu.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập tên chương trình.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; } = DiscountType.Percent;

    [Range(0.01, 1_000_000_000, ErrorMessage = "Mức giảm phải lớn hơn 0.")]
    public decimal DiscountValue { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal MinOrderAmount { get; set; }

    [Range(1, 10_000_000, ErrorMessage = "Giới hạn lượt dùng phải từ 1 trở lên.")]
    public int? UsageLimit { get; set; }

    [Required(ErrorMessage = "Chọn thời gian bắt đầu.")]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "Chọn thời gian kết thúc.")]
    public DateTime? EndAt { get; set; }

    public bool IsActive { get; set; } = true;

    public static VoucherForm From(Voucher v) => new()
    {
        Code = v.Code,
        Name = v.Name,
        DiscountType = v.DiscountType,
        DiscountValue = v.DiscountValue,
        MaxDiscountAmount = v.MaxDiscountAmount,
        MinOrderAmount = v.MinOrderAmount,
        UsageLimit = v.UsageLimit,
        StartAt = Display.InVietnam(v.StartAt).DateTime,
        EndAt = Display.InVietnam(v.EndAt).DateTime,
        IsActive = v.IsActive,
    };

    public object ToRequest() => new
    {
        Code = Code.Trim().ToUpperInvariant(),
        Name,
        DiscountType,
        DiscountValue,
        MaxDiscountAmount = DiscountType == DiscountType.Percent ? MaxDiscountAmount : null,
        MinOrderAmount,
        UsageLimit,
        StartAt = Display.FromInput(StartAt),
        EndAt = Display.FromInput(EndAt),
        IsActive,
    };
}

public sealed record VoucherEditViewModel(int? Id, Voucher? Voucher, VoucherForm Form);

// ---- Reviews and users ---------------------------------------------------------------------------------

public sealed record ReviewsAdminViewModel(IReadOnlyList<ReviewAdminItem> Reviews, int TotalCount, int Page, int TotalPages, int? Rating, bool? Visible, string? Product);

public sealed record UsersAdminViewModel(IReadOnlyList<UserListItem> Users, int TotalCount, int Page, int TotalPages, string? Q, UserRole? Role, UserStatus? Status);
