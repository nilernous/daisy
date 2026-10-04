using System.Globalization;

namespace Daisy.Mvc.Infrastructure;

/// <summary>
/// Colors for the image placeholder: a product without photos is shown in the color of
/// its garment, so the grid still reads as clothing rather than empty boxes.
/// </summary>
public static class Swatch
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trắng"] = "#ECEBE6",
        ["Đen"] = "#24262B",
        ["Xanh navy"] = "#22304A",
        ["Be"] = "#D9C8A9",
        ["Vàng cúc"] = "#F2C230",
        ["Xanh rêu"] = "#5B6B47",
        ["Hồng phấn"] = "#E8B4B8",
        ["Xanh denim"] = "#5B7BA6",
        ["Đỏ"] = "#B23A3A",
        ["Xám"] = "#9A9EA6",
        ["Nâu"] = "#7A5A43",
    };

    private static readonly string[] Fallback = ["#D9C8A9", "#5B7BA6", "#E8B4B8", "#5B6B47", "#22304A", "#ECEBE6"];

    public static string For(string? colorName, string? colorHex, string seed)
    {
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            return colorHex;
        }

        if (colorName is not null && Named.TryGetValue(colorName.Trim(), out var hex))
        {
            return hex;
        }

        var hash = seed.Aggregate(17, (h, c) => unchecked(h * 31 + c));
        return Fallback[Math.Abs(hash % Fallback.Length)];
    }

    /// <summary>True when white text reads better than dark text on this color.</summary>
    public static bool IsDark(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        double r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
        return (0.299 * r + 0.587 * g + 0.114 * b) < 140;
    }
}
