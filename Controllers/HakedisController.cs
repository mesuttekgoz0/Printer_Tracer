using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

/// <summary>
/// Sayfa-başı hakediş belgeleri: Sayaç Oku'da seçilen yazıcılar için "önceki sayaç /
/// şimdiki sayaç / fark" tablosu üretir, kalıcı kaydeder, yazdırılabilir belge + CSV verir.
/// "Önceki sayaç" bir önceki OKUMADAN değil, bu yazıcının bir önceki HAKEDİŞİNDEN alınır;
/// böylece yanlışlıkla fazladan okuma yapılması hakedişi bozmaz.
/// </summary>
public class HakedisController : Controller
{
    private readonly AppDbContext _db;
    private readonly HakedisOptions _options;

    public HakedisController(AppDbContext db, IOptions<HakedisOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.Hakedisler.AsNoTracking()
            .OrderByDescending(h => h.CreatedUtc)
            .Select(h => new HakedisListRow
            {
                Id = h.Id,
                Number = h.Number,
                CreatedUtc = h.CreatedUtc,
                PeriodStart = h.PeriodStart,
                PeriodEnd = h.PeriodEnd,
                PrinterCount = h.Lines.Count,
                TotalPages = h.Lines.Sum(l => (long?)l.Pages ?? 0),
            })
            .ToListAsync();

        return View(list);
    }

    /// <summary>Sayaç Oku'daki "Hakediş oluştur" düğmesi buraya POST eder (seçili yazıcı id'leri).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int[] printerIds)
    {
        if (printerIds.Length == 0)
        {
            TempData["Error"] = "Hakediş için önce yazıcı seçin.";
            return RedirectToAction("Index", "Readings");
        }

        var printers = await _db.Printers.AsNoTracking()
            .Where(p => printerIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new HakedisCreateViewModel
        {
            PeriodEnd = DateOnly.FromDateTime(DateTime.Now),
        };

        foreach (var printer in printers)
        {
            var latest = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.PageCount, r.TimestampUtc })
                .FirstOrDefaultAsync();

            if (latest is null)
            {
                vm.Skipped.Add(printer.Name);
                continue;
            }

            // Önceki sayaç: bu yazıcının en son hakediş satırındaki "şimdiki sayaç".
            var lastLine = await _db.HakedisLines.AsNoTracking()
                .Where(l => l.PrinterId == printer.Id)
                .OrderByDescending(l => l.Hakedis!.CreatedUtc)
                .Select(l => new { l.CurrentCounter, l.Hakedis!.PeriodEnd })
                .FirstOrDefaultAsync();

            long previousCounter;
            DateTime? previousUtc;
            bool first;

            if (lastLine is not null)
            {
                previousCounter = lastLine.CurrentCounter;
                previousUtc = lastLine.PeriodEnd.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
                first = false;
            }
            else
            {
                var earliest = await _db.PrintReadings.AsNoTracking()
                    .Where(r => r.PrinterId == printer.Id)
                    .OrderBy(r => r.TimestampUtc)
                    .Select(r => new { r.PageCount, r.TimestampUtc })
                    .FirstAsync();

                previousCounter = earliest.PageCount;
                previousUtc = earliest.TimestampUtc;
                first = true;
            }

            vm.Rows.Add(new HakedisCreateRow
            {
                PrinterId = printer.Id,
                PrinterName = printer.Name,
                Model = printer.Model,
                PreviousCounter = previousCounter,
                CurrentCounter = latest.PageCount,
                PreviousReadingUtc = previousUtc,
                CurrentReadingUtc = latest.TimestampUtc,
                FirstHakedis = first,
            });
        }

        if (vm.Rows.Count == 0)
        {
            TempData["Error"] = "Seçilen yazıcıların hiçbirinde okuma yok. Önce Sayaç Oku ile okuma alın.";
            return RedirectToAction("Index", "Readings");
        }

        var minPrev = vm.Rows
            .Select(r => r.PreviousReadingUtc ?? r.CurrentReadingUtc)
            .Min();
        vm.PeriodStart = DateOnly.FromDateTime(minPrev.ToLocalTime());

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(HakedisSaveModel model)
    {
        var rows = (model.Rows ?? new()).Where(r => r.PrinterId > 0).ToList();
        if (rows.Count == 0)
        {
            TempData["Error"] = "Hakedişe eklenecek satır yok.";
            return RedirectToAction("Index", "Readings");
        }

        var year = model.PeriodEnd == default ? DateTime.Now.Year : model.PeriodEnd.Year;
        var prefix = $"{year}-";
        var usedThisYear = await _db.Hakedisler.CountAsync(h => h.Number.StartsWith(prefix));
        var number = $"{prefix}{usedThisYear + 1:D4}";

        var hakedis = new Hakedis
        {
            Number = number,
            CreatedUtc = DateTime.UtcNow,
            PeriodStart = model.PeriodStart == default ? DateOnly.FromDateTime(DateTime.Now) : model.PeriodStart,
            PeriodEnd = model.PeriodEnd == default ? DateOnly.FromDateTime(DateTime.Now) : model.PeriodEnd,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
        };

        // Ad/model'i formdan değil, güncel yazıcı kaydından al (silinmişse forma düş).
        var ids = rows.Select(r => r.PrinterId).ToList();
        var printers = await _db.Printers.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var r in rows)
        {
            var pages = r.CurrentCounter >= r.PreviousCounter
                ? r.CurrentCounter - r.PreviousCounter
                : (long?)null;

            printers.TryGetValue(r.PrinterId, out var printer);

            hakedis.Lines.Add(new HakedisLine
            {
                PrinterId = r.PrinterId,
                PrinterName = printer?.Name
                    ?? (string.IsNullOrWhiteSpace(r.PrinterName) ? $"#{r.PrinterId}" : r.PrinterName.Trim()),
                Model = printer?.Model
                    ?? (string.IsNullOrWhiteSpace(r.Model) ? null : r.Model.Trim()),
                PreviousCounter = r.PreviousCounter,
                CurrentCounter = r.CurrentCounter,
                Pages = pages,
            });
        }

        _db.Hakedisler.Add(hakedis);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Hakediş {number} oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = hakedis.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var hakedis = await _db.Hakedisler.AsNoTracking()
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hakedis is null)
            return NotFound();

        ViewData["FromCompany"] = _options.FromCompany;
        ViewData["ToCompany"] = _options.ToCompany;
        return View(hakedis);
    }

    public async Task<IActionResult> Csv(int id)
    {
        var hakedis = await _db.Hakedisler.AsNoTracking()
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hakedis is null)
            return NotFound();

        var tr = new CultureInfo("tr-TR");
        var sb = new StringBuilder();
        sb.AppendLine($"Hakediş No;{hakedis.Number}");
        sb.AppendLine($"Düzenleme;{hakedis.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", tr)}");
        sb.AppendLine($"Dönem;{hakedis.PeriodStart:dd.MM.yyyy} - {hakedis.PeriodEnd:dd.MM.yyyy}");
        if (!string.IsNullOrWhiteSpace(hakedis.Note))
            sb.AppendLine($"Açıklama;{Escape(hakedis.Note)}");
        sb.AppendLine();
        sb.AppendLine("Sıra;Yazıcı;Marka / Model;Önceki Sayaç;Şimdiki Sayaç;Fark (Sayfa)");

        var i = 1;
        foreach (var l in hakedis.Lines)
        {
            sb.Append(i++).Append(';')
              .Append(Escape(l.PrinterName)).Append(';')
              .Append(Escape(l.Model ?? "")).Append(';')
              .Append(l.PreviousCounter).Append(';')
              .Append(l.CurrentCounter).Append(';')
              .Append(l.Pages?.ToString() ?? "").AppendLine();
        }
        sb.AppendLine($";;;;Toplam;{hakedis.TotalPages}");

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"hakedis-{hakedis.Number}.csv");

        static string Escape(string s) =>
            s.Contains(';') || s.Contains('"') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
    }
}

public class HakedisListRow
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int PrinterCount { get; set; }
    public long TotalPages { get; set; }
}

public class HakedisSaveModel
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string? Note { get; set; }
    public List<HakedisSaveRow>? Rows { get; set; }
}

public class HakedisSaveRow
{
    public int PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string? Model { get; set; }
    public long PreviousCounter { get; set; }
    public long CurrentCounter { get; set; }
}
