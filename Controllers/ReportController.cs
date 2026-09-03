using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

public class ReportController : Controller
{
    private readonly AppDbContext _db;

    public ReportController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Bir tarih aralığı için gün gün toplam basılan sayfa özeti (varsayılan son 30 gün).
    /// Her günün deltası, o güne ait ardışık okuma farklarının toplamıdır.
    /// </summary>
    public async Task<IActionResult> Summary(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toDay = to ?? today;
        var fromDay = from ?? toDay.AddDays(-29);

        if (fromDay > toDay)
            (fromDay, toDay) = (toDay, fromDay);

        // Aralığı en fazla ~1 yıl ile sınırla.
        if (toDay.DayNumber - fromDay.DayNumber > 366)
            fromDay = toDay.AddDays(-366);

        var startLocal = fromDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var endLocal = toDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var endUtc = endLocal.ToUniversalTime();

        var printers = await _db.Printers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new DailySummaryViewModel
        {
            From = fromDay,
            To = toDay,
            Printers = printers.Select(p => new PrinterRef { Id = p.Id, Name = p.Name }).ToList(),
        };

        var dayMap = new Dictionary<DateOnly, DailySummaryRow>();
        for (var d = fromDay; d <= toDay; d = d.AddDays(1))
            dayMap[d] = new DailySummaryRow { Date = d };

        foreach (var printer in printers)
        {
            // İlk günün deltasını hesaplayabilmek için aralık başından önceki okumayı da al.
            var readings = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id && r.TimestampUtc < endUtc)
                .OrderBy(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .ToListAsync();

            for (var i = 1; i < readings.Count; i++)
            {
                var delta = readings[i].PageCount - readings[i - 1].PageCount;
                if (delta <= 0)
                    continue;

                var dateLocal = DateOnly.FromDateTime(readings[i].TimestampUtc.ToLocalTime());
                if (dateLocal < fromDay || dateLocal > toDay)
                    continue;

                var row = dayMap[dateLocal];
                row.PerPrinter[printer.Id] = row.PagesFor(printer.Id) + delta;
                row.Total += delta;

                vm.PrinterTotals[printer.Id] = (vm.PrinterTotals.TryGetValue(printer.Id, out var pt) ? pt : 0) + delta;
            }
        }

        vm.Days = dayMap.Values.OrderBy(d => d.Date).ToList();
        vm.GrandTotal = vm.Days.Sum(d => d.Total);
        vm.DailyAverage = vm.DayCount > 0 ? (double)vm.GrandTotal / vm.DayCount : 0;
        vm.BusiestDay = vm.Days.Where(d => d.Total > 0).OrderByDescending(d => d.Total).FirstOrDefault();

        return View(vm);
    }

    /// <summary>
    /// Yazıcıların ömür boyu (dahili) sayaç değerlerini gösterir. Printer-MIB sayacı
    /// yazıcı üretildiğinden/sıfırlandığından beri bastığı TÜM sayfaları içerdiği için,
    /// izlemeye başlamadan önceki dönemin de bu sayaca dahil olduğunu gösterir.
    /// </summary>
    public async Task<IActionResult> Lifetime()
    {
        var printers = await _db.Printers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new LifetimeViewModel();

        foreach (var printer in printers)
        {
            var first = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderBy(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .FirstOrDefaultAsync();

            var last = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .FirstOrDefaultAsync();

            vm.Printers.Add(new PrinterLifetimeRow
            {
                PrinterId = printer.Id,
                PrinterName = printer.Name,
                IpAddress = printer.IpAddress,
                HasReadings = first is not null,
                FirstReadingUtc = first?.TimestampUtc,
                FirstCounter = first?.PageCount,
                LatestReadingUtc = last?.TimestampUtc,
                LatestCounter = last?.PageCount,
            });
        }

        return View(vm);
    }
}
