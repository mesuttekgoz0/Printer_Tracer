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
/// Sayfa-başı hakediş belgeleri. Belge bir <b>tedarikçi</b> seçilerek üretilir: o tedarikçinin
/// tüm yazıcıları için "önceki sayaç / şimdiki sayaç / fark" hesaplanır ve tedarikçinin
/// (döneme göre en güncel) fiyat listesinden tür bazlı sayfa-başı fiyatla tutar bulunur.
/// "Önceki sayaç" bir önceki OKUMADAN değil, bu yazıcının bir önceki HAKEDİŞİNDEN alınır.
/// Kaydedilince satırlardaki değerler (sayaç, tür, fiyat, tutar, tedarikçi adı) dondurulur.
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
                TedarikciAd = h.TedarikciAd,
                CreatedUtc = h.CreatedUtc,
                PeriodStart = h.PeriodStart,
                PeriodEnd = h.PeriodEnd,
                PrinterCount = h.Lines.Count,
                TotalPages = h.Lines.Sum(l => (long?)l.Pages ?? 0),
                TotalAmount = h.Lines.Sum(l => (decimal?)l.Amount ?? 0m),
            })
            .ToListAsync();

        return View(list);
    }

    /// <summary>Tedarikçi seçme ekranı ("Yeni hakediş" düğmesi buraya götürür).</summary>
    public async Task<IActionResult> New()
    {
        var suppliers = await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new TedarikciSecRow
            {
                Id = t.Id,
                Ad = t.Ad,
                PrinterCount = t.Printers.Count,
                HasPriceList = t.FiyatListeleri.Any(),
            })
            .ToListAsync();

        return View(suppliers);
    }

    /// <summary>Tedarikçi seçildikten sonra inceleme/düzeltme ekranını hazırlar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int tedarikciId)
    {
        var tedarikci = await _db.Tedarikciler.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tedarikciId);

        if (tedarikci is null)
        {
            TempData["Error"] = "Tedarikçi bulunamadı.";
            return RedirectToAction(nameof(New));
        }

        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .Where(p => p.TedarikciId == tedarikciId)
            .OrderBy(p => p.Name)
            .ToListAsync();

        if (printers.Count == 0)
        {
            TempData["Error"] = $"\"{tedarikci.Ad}\" tedarikçisine bağlı yazıcı yok. Yazıcılar sayfasından tedarikçi atayın.";
            return RedirectToAction(nameof(New));
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var priceList = await ResolvePriceListAsync(tedarikciId, today);

        var vm = new HakedisCreateViewModel
        {
            TedarikciId = tedarikci.Id,
            TedarikciAd = tedarikci.Ad,
            PeriodEnd = today,
            FiyatListesiBilgi = priceList is null
                ? null
                : $"{priceList.ListeAdi} ({priceList.Tarih:dd.MM.yyyy})",
        };

        if (priceList is null)
            vm.Warnings.Add($"\"{tedarikci.Ad}\" tedarikçisinin fiyat listesi yok — tüm tutarlar 0 gelir. Tedarikçiler sayfasından fiyat listesi ekleyin.");

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

            var unitPrice = printer.TurId is int tid ? priceList?.FiyatBul(tid) ?? 0m : 0m;

            if (printer.TurId is null)
                vm.Warnings.Add($"{printer.Name}: türü belirtilmemiş — fiyat uygulanamadı (0). Yazıcılar sayfasından türü seçin.");
            else if (priceList is not null && unitPrice == 0m)
                vm.Warnings.Add($"{printer.Name}: fiyat listesinde \"{printer.TurAdi}\" için fiyat yok (0).");

            vm.Rows.Add(new HakedisCreateRow
            {
                PrinterId = printer.Id,
                PrinterName = printer.Name,
                Model = printer.Model,
                TurId = printer.TurId,
                TurAd = printer.TurAdi,
                PreviousCounter = previousCounter,
                CurrentCounter = latest.PageCount,
                PreviousReadingUtc = previousUtc,
                CurrentReadingUtc = latest.TimestampUtc,
                UnitPrice = unitPrice,
                FirstHakedis = first,
            });
        }

        if (vm.Rows.Count == 0)
        {
            TempData["Error"] = "Bu tedarikçinin yazıcılarının hiçbirinde okuma yok. Önce Sayaç Oku ile okuma alın.";
            return RedirectToAction(nameof(New));
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
            return RedirectToAction(nameof(Index));
        }

        var year = model.PeriodEnd == default ? DateTime.Now.Year : model.PeriodEnd.Year;
        var prefix = $"{year}-";
        var usedThisYear = await _db.Hakedisler.CountAsync(h => h.Number.StartsWith(prefix));
        var number = $"{prefix}{usedThisYear + 1:D4}";

        var tedarikciAd = model.TedarikciId > 0
            ? await _db.Tedarikciler.Where(t => t.Id == model.TedarikciId).Select(t => t.Ad).FirstOrDefaultAsync()
            : null;

        var hakedis = new Hakedis
        {
            Number = number,
            CreatedUtc = DateTime.UtcNow,
            TedarikciId = model.TedarikciId > 0 ? model.TedarikciId : null,
            TedarikciAd = tedarikciAd ?? (string.IsNullOrWhiteSpace(model.TedarikciAd) ? null : model.TedarikciAd.Trim()),
            PeriodStart = model.PeriodStart == default ? DateOnly.FromDateTime(DateTime.Now) : model.PeriodStart,
            PeriodEnd = model.PeriodEnd == default ? DateOnly.FromDateTime(DateTime.Now) : model.PeriodEnd,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
        };

        // Ad/model/tür'ü formdan değil, güncel yazıcı kaydından al (silinmişse forma düş).
        var ids = rows.Select(r => r.PrinterId).ToList();
        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var r in rows)
        {
            var pages = r.CurrentCounter >= r.PreviousCounter
                ? r.CurrentCounter - r.PreviousCounter
                : (long?)null;

            printers.TryGetValue(r.PrinterId, out var printer);

            var turId = printer?.TurId ?? r.TurId;
            var turAd = printer?.TurAdi ?? Tur.Belirtilmemis;
            var unitPrice = TedarikciController.ParsePrice(r.UnitPrice);
            var amount = decimal.Round((pages ?? 0) * unitPrice, 2, MidpointRounding.AwayFromZero);

            hakedis.Lines.Add(new HakedisLine
            {
                PrinterId = r.PrinterId,
                PrinterName = printer?.Name
                    ?? (string.IsNullOrWhiteSpace(r.PrinterName) ? $"#{r.PrinterId}" : r.PrinterName.Trim()),
                Model = printer?.Model
                    ?? (string.IsNullOrWhiteSpace(r.Model) ? null : r.Model.Trim()),
                TurId = turId,
                TurAd = turAd,
                PreviousCounter = r.PreviousCounter,
                CurrentCounter = r.CurrentCounter,
                Pages = pages,
                UnitPrice = unitPrice,
                Amount = amount,
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

    /// <summary>
    /// Hakedişi tamamen siler (satırlar cascade). Silinince bu tedarikçinin yazıcıları için
    /// bir sonraki hakediş, "önceki sayaç"ı bir önceki hakedişten alır — yani silme geri alınır gibi çalışır.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hakedis = await _db.Hakedisler.FindAsync(id);
        if (hakedis is not null)
        {
            _db.Hakedisler.Remove(hakedis);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Hakediş {hakedis.Number} silindi.";
        }

        return RedirectToAction(nameof(Index));
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
        if (!string.IsNullOrWhiteSpace(hakedis.TedarikciAd))
            sb.AppendLine($"Tedarikçi;{Escape(hakedis.TedarikciAd)}");
        sb.AppendLine($"Düzenleme;{hakedis.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", tr)}");
        sb.AppendLine($"Dönem;{hakedis.PeriodStart:dd.MM.yyyy} - {hakedis.PeriodEnd:dd.MM.yyyy}");
        if (!string.IsNullOrWhiteSpace(hakedis.Note))
            sb.AppendLine($"Açıklama;{Escape(hakedis.Note)}");
        sb.AppendLine();
        sb.AppendLine("Sıra;Yazıcı;Marka / Model;Tür;Önceki Sayaç;Şimdiki Sayaç;Fark (Sayfa);Sayfa Başı Fiyat;Tutar");

        var i = 1;
        foreach (var l in hakedis.Lines)
        {
            sb.Append(i++).Append(';')
              .Append(Escape(l.PrinterName)).Append(';')
              .Append(Escape(l.Model ?? "")).Append(';')
              .Append(Escape(l.TurAd ?? Tur.Belirtilmemis)).Append(';')
              .Append(l.PreviousCounter).Append(';')
              .Append(l.CurrentCounter).Append(';')
              .Append(l.Pages?.ToString() ?? "").Append(';')
              .Append(l.UnitPrice.ToString("0.####", tr)).Append(';')
              .Append(l.Amount.ToString("0.00", tr)).AppendLine();
        }
        sb.AppendLine($";;;;;;Toplam;{hakedis.TotalPages};{hakedis.TotalAmount.ToString("0.00", tr)}");

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"hakedis-{hakedis.Number}.csv");

        static string Escape(string s) =>
            s.Contains(';') || s.Contains('"') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
    }

    /// <summary>
    /// Tedarikçinin verilen tarihe göre geçerli fiyat listesi: <c>Tarih &lt;= asOf</c> olanlardan
    /// en güncelı; öyle bir liste yoksa (ilk hakediş, listeler ileri tarihli) en erken liste.
    /// </summary>
    private async Task<FiyatListesi?> ResolvePriceListAsync(int tedarikciId, DateOnly asOf)
    {
        var list = await _db.FiyatListeleri.AsNoTracking()
            .Include(f => f.Satirlar)
            .Where(f => f.TedarikciId == tedarikciId && f.Tarih <= asOf)
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync();

        list ??= await _db.FiyatListeleri.AsNoTracking()
            .Include(f => f.Satirlar)
            .Where(f => f.TedarikciId == tedarikciId)
            .OrderBy(f => f.Tarih).ThenBy(f => f.Id)
            .FirstOrDefaultAsync();

        return list;
    }
}

public class HakedisListRow
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? TedarikciAd { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int PrinterCount { get; set; }
    public long TotalPages { get; set; }
    public decimal TotalAmount { get; set; }
}

public class TedarikciSecRow
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int PrinterCount { get; set; }
    public bool HasPriceList { get; set; }
}

public class HakedisSaveModel
{
    public int TedarikciId { get; set; }
    public string? TedarikciAd { get; set; }
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
    public int? TurId { get; set; }
    public long PreviousCounter { get; set; }
    public long CurrentCounter { get; set; }
    /// <summary>Gizli alandan invariant ("0.15") gelir; <see cref="TedarikciController.ParsePrice"/> ile çözülür.</summary>
    public string? UnitPrice { get; set; }
}
