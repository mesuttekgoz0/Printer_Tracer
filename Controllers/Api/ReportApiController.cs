using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Data.Repositories;

namespace YaziciTakip.Controllers.Api;

/// <summary>"Rapor" sayfasının JSON API'si: tarih aralığındaki okumalar + farklar + toplamlar.</summary>
[ApiController]
[Route("api/report")]
[Produces("application/json")]
public class ReportApiController : ControllerBase
{
    private readonly IPrinterRepository _printers;
    private readonly IPrintReadingRepository _readings;

    public ReportApiController(IPrinterRepository printers, IPrintReadingRepository readings)
    {
        _printers = printers;
        _readings = readings;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> Summary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] bool all = false)
    {
        var ct = HttpContext.RequestAborted;
        const int recentLimit = 30;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toDay = to ?? today;
        var fromDay = from ?? new DateOnly(toDay.Year, toDay.Month, 1);
        if (fromDay > toDay) (fromDay, toDay) = (toDay, fromDay);
        if (toDay.DayNumber - fromDay.DayNumber > 366) fromDay = toDay.AddDays(-366);

        var endUtc = toDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();

        var printers = await _printers.GetAllAsync(ct);
        var log = new List<ReadingLogDto>();
        var printerTotals = new Dictionary<int, long>();
        long grand = 0;

        foreach (var printer in printers)
        {
            var readings = await _readings.ListBeforeUtcAsync(printer.Id, endUtc, ct);

            for (var i = 0; i < readings.Count; i++)
            {
                var dateLocal = DateOnly.FromDateTime(readings[i].TimestampUtc.ToLocalTime());
                if (dateLocal < fromDay || dateLocal > toDay) continue;

                long? delta = null;
                if (i > 0)
                {
                    var diff = readings[i].PageCount - readings[i - 1].PageCount;
                    delta = diff >= 0 ? diff : null;
                    if (diff > 0)
                    {
                        grand += diff;
                        printerTotals[printer.Id] = (printerTotals.TryGetValue(printer.Id, out var pt) ? pt : 0) + diff;
                    }
                }

                log.Add(new ReadingLogDto(readings[i].TimestampUtc, printer.Id, printer.Name, readings[i].PageCount, delta));
            }
        }

        log.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));

        var totals = printers
            .Where(p => printerTotals.ContainsKey(p.Id))
            .Select(p => new PrinterTotalDto(p.Id, p.Name, printerTotals[p.Id]))
            .OrderByDescending(x => x.Total)
            .ToList();

        return new ReportSummaryDto(
            fromDay, toDay, grand, totals,
            all ? log : log.Take(recentLimit).ToList(),
            log.Count, all);
    }
}

public record ReportSummaryDto(
    DateOnly From, DateOnly To, long GrandTotal,
    IReadOnlyList<PrinterTotalDto> PrinterTotals,
    IReadOnlyList<ReadingLogDto> Readings,
    int ReadingsTotal, bool ShowAll);

public record PrinterTotalDto(int PrinterId, string Name, long Total);

public record ReadingLogDto(DateTime TimestampUtc, int PrinterId, string PrinterName, long PageCount, long? Delta);
