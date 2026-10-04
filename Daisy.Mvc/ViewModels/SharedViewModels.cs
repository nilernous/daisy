namespace Daisy.Mvc.ViewModels;

/// <summary>Product photo, or a placeholder in the garment's color.</summary>
public sealed record MediaModel(string? Url, string Name, string? ColorName = null, string? ColorHex = null);

/// <summary>Price with the hang tag; the old price is struck through when on sale.</summary>
public sealed record PriceModel(decimal Price, decimal? WasPrice = null, bool Large = false);

/// <summary>Pagination links that keep the current query string.</summary>
public sealed record PagerModel(int Page, int TotalPages, Func<int, string> UrlFor);
