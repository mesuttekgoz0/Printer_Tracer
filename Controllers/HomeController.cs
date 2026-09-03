using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _db;

    public HomeController(ILogger<HomeController> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var tr = new CultureInfo("tr-TR");
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var todayStart = now.Date;
        var monthStartUtc = monthStart.ToUniversalTime();
        var todayStartUtc = todayStart.ToUniversalTime();

        var printers = await _db.Printers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            PrinterCount = printers.Count,
            MonthLabel = monthStart.ToString("MMMM yyyy", tr),
        };

        foreach (var printer in printers)
        {
            var readings = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderBy(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .ToListAsync();

            long monthPages = 0;
            long todayPages = 0;
            for (var i = 1; i < readings.Count; i++)
            {
                var delta = readings[i].PageCount - readings[i - 1].PageCount;
                if (delta <= 0)
                    continue;

                if (readings[i].TimestampUtc >= monthStartUtc)
                    monthPages += delta;
                if (readings[i].TimestampUtc >= todayStartUtc)
                    todayPages += delta;
            }

            var last = readings.Count > 0 ? readings[^1] : null;

            vm.MonthTotal += monthPages;
            vm.TodayTotal += todayPages;
            if (last is not null && (vm.LastReadingUtc is null || last.TimestampUtc > vm.LastReadingUtc))
                vm.LastReadingUtc = last.TimestampUtc;

            vm.Printers.Add(new DashboardPrinterRow
            {
                Id = printer.Id,
                Name = printer.Name,
                IpAddress = printer.IpAddress,
                MonthPages = monthPages,
                LatestCounter = last?.PageCount,
                LatestReadingUtc = last?.TimestampUtc,
            });
        }

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
