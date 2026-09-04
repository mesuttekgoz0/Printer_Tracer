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
    /// Bir tarih aralığındaki (varsayılan: içinde bulunulan ayın 1'inden bugüne) tek tek SNMP
    /// okumaları ve her okumanın bir öncekine göre farkı; ayrıca dönem toplamı ve yazıcı bazlı toplamlar.
    /// </summary>
    public async Task<IActionResult> Summary(DateOnly? from, DateOnly? to, bool all = false)
    {
        const int RecentReadingsLimit = 30;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toDay = to ?? today;
        var fromDay = from ?? new DateOnly(toDay.Year, toDay.Month, 1);

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

        var log = new List<ReadingLogRow>();

        foreach (var printer in printers)
        {
            // İlk okumanın deltasını hesaplayabilmek için aralık başından önceki okumayı da al.
            var readings = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id && r.TimestampUtc < endUtc)
                .OrderBy(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .ToListAsync();

            for (var i = 0; i < readings.Count; i++)
            {
                var dateLocal = DateOnly.FromDateTime(readings[i].TimestampUtc.ToLocalTime());
                if (dateLocal < fromDay || dateLocal > toDay)
                    continue;

                long? delta = null;
                if (i > 0)
                {
                    var diff = readings[i].PageCount - readings[i - 1].PageCount;
                    delta = diff >= 0 ? diff : null;

                    if (diff > 0)
                    {
                        vm.GrandTotal += diff;
                        vm.PrinterTotals[printer.Id] =
                            (vm.PrinterTotals.TryGetValue(printer.Id, out var pt) ? pt : 0) + diff;
                    }
                }

                log.Add(new ReadingLogRow
                {
                    TimestampUtc = readings[i].TimestampUtc,
                    PrinterId = printer.Id,
                    PrinterName = printer.Name,
                    PageCount = readings[i].PageCount,
                    Delta = delta,
                });
            }
        }

        log.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));
        vm.ReadingsTotal = log.Count;
        vm.ShowAllReadings = all;
        vm.Readings = all ? log : log.Take(RecentReadingsLimit).ToList();

        return View(vm);
    }
}
