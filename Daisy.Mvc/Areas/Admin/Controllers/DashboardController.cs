using System.Globalization;
using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-67 – UC-70 (Admin). Staff land on the order list instead.</summary>
public sealed class DashboardController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index(string? preset, DateOnly? from, DateOnly? to, string? groupBy)
    {
        if (!IsAdmin)
        {
            return RedirectToAction("Index", "Orders");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        DateOnly start, end;
        (start, end, preset) = preset switch
        {
            "7" => (today.AddDays(-6), today, "7"),
            "90" => (today.AddDays(-89), today, "90"),
            "year" => (new DateOnly(today.Year, 1, 1), today, "year"),
            _ when from is not null || to is not null => (from ?? today.AddDays(-29), to ?? today, (string?)null),
            _ => (today.AddDays(-29), today, "30"),
        };
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var days = end.DayNumber - start.DayNumber;
        groupBy = groupBy is "day" or "month" ? groupBy : days > 92 ? "month" : "day";
        if (groupBy == "day" && days > 365)
        {
            groupBy = "month";
        }

        var range = $"?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}";
        var revenue = await admin.GetRevenueAsync($"{range}&groupBy={groupBy}");
        var statuses = await admin.GetOrdersByStatusAsync(range);
        var top = await admin.GetTopProductsAsync($"{range}&top=8");
        var customers = await admin.GetNewCustomersAsync($"{range}&groupBy={groupBy}");

        var revenueChart = new ColumnChartModel(
            "Doanh thu từ đơn hoàn thành",
            "đ",
            revenue.Select(r => new ChartPoint(PeriodLabel(r.Period, groupBy), r.Revenue, Display.Money(r.Revenue), $"{r.OrderCount} đơn")).ToList(),
            Compact);
        var customerChart = new ColumnChartModel(
            "Khách hàng mới",
            "khách",
            customers.Select(c => new ChartPoint(PeriodLabel(c.Period, groupBy), c.Count, $"{c.Count} khách")).ToList(),
            v => v.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")),
            WholeNumbers: true);
        var statusChart = new BarListModel(
            "Đơn hàng theo trạng thái",
            statuses.Select(s => new ChartPoint(Display.Label(s.Status), s.Count, s.Count.ToString("#,0"))).ToList());
        var topChart = new BarListModel(
            "Bán chạy nhất (số lượng)",
            top.Select(t => new ChartPoint(t.ProductName, t.QuantitySold, t.QuantitySold.ToString("#,0"), Display.Money(t.Revenue))).ToList());

        ViewData["Title"] = "Tổng quan";
        return View(new DashboardViewModel(
            start, end, groupBy, preset,
            revenue.Sum(r => r.Revenue),
            revenue.Sum(r => r.OrderCount),
            statuses.Sum(s => s.Count),
            customers.Sum(c => c.Count),
            revenueChart, statusChart, topChart, customerChart));
    }

    private static string PeriodLabel(string period, string groupBy) =>
        groupBy == "month"
            ? $"T{period[5..7].TrimStart('0')}/{period[..4]}"
            : $"{period[8..10]}/{period[5..7]}";

    /// <summary>Axis ticks in Vietnamese short form: 1,5tr, 800k.</summary>
    private static string Compact(decimal value) =>
        value >= 1_000_000 ? (value / 1_000_000).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + "tr"
        : value >= 1_000 ? (value / 1_000).ToString("0", CultureInfo.GetCultureInfo("vi-VN")) + "k"
        : value.ToString("0");
}
