using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers.Api;

/// <summary>Hakediş belgeleri JSON API'si (tedarikçi seçerek üret, dondur, indir).</summary>
[ApiController]
[Route("api/hakedisler")]
[Produces("application/json")]
public class HakedislerApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HakedisOptions _options;

    public HakedislerApiController(AppDbContext db, IOptions<HakedisOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HakedisListDto>>> GetAll()
    {
        return await _db.Hakedisler.AsNoTracking()
            .OrderByDescending(h => h.CreatedUtc)
            .Select(h => new HakedisListDto(
                h.Id, h.Number, h.TedarikciAd, h.CreatedUtc, h.PeriodStart, h.PeriodEnd,
                h.Lines.Count,
                h.Lines.Sum(l => (long?)l.Pages ?? 0),
                h.Lines.Sum(l => (decimal?)l.Amount ?? 0m)))
            .ToListAsync();
    }

    [HttpGet("tedarikci-secenekleri")]
    public async Task<ActionResult<IEnumerable<TedarikciSecDto>>> SupplierOptions()
    {
        return await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new TedarikciSecDto(t.Id, t.Ad, t.Printers.Count, t.Fiyatlar.Any()))
            .ToListAsync();
    }

    [HttpPost("taslak")]
    public async Task<ActionResult<HakedisTaslakDto>> Taslak([FromBody] TaslakRequest req)
    {
        var tedarikci = await _db.Tedarikciler.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TedarikciId);
        if (tedarikci is null) return Problem("Tedarikçi bulunamadı.", statusCode: 404);

        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .Where(p => p.TedarikciId == req.TedarikciId)
            .OrderBy(p => p.Name)
            .ToListAsync();
        if (printers.Count == 0)
            return Problem($"\"{tedarikci.Ad}\" tedarikçisine bağlı yazıcı yok.", statusCode: 400);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var hasAnyFiyat = await _db.Fiyatlar.AnyAsync(f => f.TedarikciId == req.TedarikciId);

        var warnings = new List<string>();
        var skipped = new List<string>();
        var rows = new List<TaslakRowDto>();

        if (!hasAnyFiyat)
            warnings.Add($"\"{tedarikci.Ad}\" tedarikçisi için tanımlı fiyat yok — tüm tutarlar 0 gelir.");

        foreach (var printer in printers)
        {
            var latest = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.PageCount, r.TimestampUtc })
                .FirstOrDefaultAsync();
            if (latest is null)
            {
                skipped.Add(printer.Name);
                continue;
            }

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

            decimal unitPrice = 0m;
            string? priceNote = null;
            if (printer.TurId is int tid)
                (unitPrice, priceNote) = await ResolveUnitPriceAsync(req.TedarikciId, tid, today);

            if (printer.TurId is null)
                warnings.Add($"{printer.Name}: türü belirtilmemiş — fiyat uygulanamadı (0).");
            else if (unitPrice == 0m)
                warnings.Add($"{printer.Name} (\"{printer.TurAdi}\"): {priceNote ?? "geçerli fiyat yok"} (0).");

            rows.Add(new TaslakRowDto(
                printer.Id, printer.Name, printer.Model, printer.TurId, printer.TurAdi,
                previousCounter, latest.PageCount, previousUtc, latest.TimestampUtc, unitPrice, first));
        }

        if (rows.Count == 0)
            return Problem("Bu tedarikçinin yazıcılarının hiçbirinde okuma yok.", statusCode: 400);

        var periodStart = DateOnly.FromDateTime(
            rows.Select(r => r.PreviousReadingUtc ?? r.CurrentReadingUtc).Min().ToLocalTime());

        return new HakedisTaslakDto(
            tedarikci.Id, tedarikci.Ad,
            periodStart, today, rows, skipped, warnings);
    }

    [HttpPost]
    public async Task<ActionResult<HakedisCreatedDto>> Create([FromBody] HakedisKaydetRequest req)
    {
        var rows = (req.Rows ?? new()).Where(r => r.PrinterId > 0).ToList();
        if (rows.Count == 0) return Problem("Hakedişe eklenecek satır yok.", statusCode: 400);

        var year = req.PeriodEnd == default ? DateTime.Now.Year : req.PeriodEnd.Year;
        var prefix = $"{year}-";
        // Numara adet değil, o yıl içindeki EN YÜKSEK sıra + 1'den üretilir; arada silinmiş
        // kayıt olsa bile (numarada boşluk) mevcut bir numarayı tekrar üretmeyiz.
        var mevcutNumaralar = await _db.Hakedisler
            .Where(h => h.Number.StartsWith(prefix))
            .Select(h => h.Number)
            .ToListAsync();
        var sonSira = mevcutNumaralar
            .Select(n => int.TryParse(n.Substring(prefix.Length), out var s) ? s : 0)
            .DefaultIfEmpty(0)
            .Max();
        var number = $"{prefix}{sonSira + 1:D4}";

        var tedarikciAd = req.TedarikciId > 0
            ? await _db.Tedarikciler.Where(t => t.Id == req.TedarikciId).Select(t => t.Ad).FirstOrDefaultAsync()
            : null;

        var hakedis = new Hakedis
        {
            Number = number,
            CreatedUtc = DateTime.UtcNow,
            TedarikciId = req.TedarikciId > 0 ? req.TedarikciId : null,
            TedarikciAd = tedarikciAd ?? (string.IsNullOrWhiteSpace(req.TedarikciAd) ? null : req.TedarikciAd.Trim()),
            PeriodStart = req.PeriodStart == default ? DateOnly.FromDateTime(DateTime.Now) : req.PeriodStart,
            PeriodEnd = req.PeriodEnd == default ? DateOnly.FromDateTime(DateTime.Now) : req.PeriodEnd,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        };

        var ids = rows.Select(r => r.PrinterId).ToList();
        var printers = await _db.Printers.AsNoTracking().Include(p => p.Tur)
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        foreach (var r in rows)
        {
            var pages = r.CurrentCounter >= r.PreviousCounter ? r.CurrentCounter - r.PreviousCounter : (long?)null;
            printers.TryGetValue(r.PrinterId, out var printer);
            var unitPrice = Math.Max(0m, r.UnitPrice);
            var amount = decimal.Round((pages ?? 0) * unitPrice, 2, MidpointRounding.AwayFromZero);

            hakedis.Lines.Add(new HakedisLine
            {
                PrinterId = r.PrinterId,
                PrinterName = printer?.Name ?? (string.IsNullOrWhiteSpace(r.PrinterName) ? $"#{r.PrinterId}" : r.PrinterName.Trim()),
                Model = printer?.Model ?? (string.IsNullOrWhiteSpace(r.Model) ? null : r.Model.Trim()),
                TurId = printer?.TurId ?? r.TurId,
                TurAd = printer?.TurAdi ?? Tur.Belirtilmemis,
                PreviousCounter = r.PreviousCounter,
                CurrentCounter = r.CurrentCounter,
                Pages = pages,
                UnitPrice = unitPrice,
                Amount = amount,
            });
        }

        _db.Hakedisler.Add(hakedis);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = hakedis.Id }, new HakedisCreatedDto(hakedis.Id, hakedis.Number));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HakedisDetailDto>> Get(int id)
    {
        var h = await _db.Hakedisler.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();

        return new HakedisDetailDto(
            h.Id, h.Number, h.TedarikciAd, h.CreatedUtc, h.PeriodStart, h.PeriodEnd, h.Note,
            string.IsNullOrWhiteSpace(_options.FromCompany) ? null : _options.FromCompany,
            string.IsNullOrWhiteSpace(h.TedarikciAd) && !string.IsNullOrWhiteSpace(_options.ToCompany) ? _options.ToCompany : null,
            h.Lines.Select(l => new HakedisLineDto(
                l.PrinterName, l.Model, l.TurAd ?? Tur.Belirtilmemis,
                l.PreviousCounter, l.CurrentCounter, l.Pages, l.UnitPrice, l.Amount)).ToList(),
            h.TotalPages, h.TotalAmount);
    }

    [HttpGet("{id:int}/csv")]
    public async Task<IActionResult> Csv(int id)
    {
        var h = await _db.Hakedisler.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();

        var tr = new CultureInfo("tr-TR");
        var sb = new StringBuilder();
        sb.AppendLine($"Hakediş No;{h.Number}");
        if (!string.IsNullOrWhiteSpace(h.TedarikciAd)) sb.AppendLine($"Tedarikçi;{Esc(h.TedarikciAd)}");
        sb.AppendLine($"Düzenleme;{h.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", tr)}");
        sb.AppendLine($"Dönem;{h.PeriodStart:dd.MM.yyyy} - {h.PeriodEnd:dd.MM.yyyy}");
        if (!string.IsNullOrWhiteSpace(h.Note)) sb.AppendLine($"Açıklama;{Esc(h.Note)}");
        sb.AppendLine();
        sb.AppendLine("Sıra;Yazıcı;Marka / Model;Tür;Önceki Sayaç;Şimdiki Sayaç;Fark (Sayfa);Sayfa Başı Fiyat;Tutar");
        var i = 1;
        foreach (var l in h.Lines)
        {
            sb.Append(i++).Append(';')
              .Append(Esc(l.PrinterName)).Append(';')
              .Append(Esc(l.Model ?? "")).Append(';')
              .Append(Esc(l.TurAd ?? Tur.Belirtilmemis)).Append(';')
              .Append(l.PreviousCounter).Append(';')
              .Append(l.CurrentCounter).Append(';')
              .Append(l.Pages?.ToString() ?? "").Append(';')
              .Append(l.UnitPrice.ToString("0.####", tr)).Append(';')
              .Append(l.Amount.ToString("0.00", tr)).AppendLine();
        }
        sb.AppendLine($";;;;;;Toplam;{h.TotalPages};{h.TotalAmount.ToString("0.00", tr)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"hakedis-{h.Number}.csv");

        static string Esc(string s) =>
            s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await _db.Hakedisler.FindAsync(id);
        if (h is null) return NotFound();
        _db.Hakedisler.Remove(h);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// (Tedarikçi, tür) için <paramref name="asOf"/> tarihini kapsayan fiyat detayının
    /// sayfa-başı fiyatı. Master yoksa / kapsayan detay yoksa 0. Birden çok kapsıyorsa
    /// en yeni başlangıçlı.
    /// </summary>
    /// <summary>
    /// (Tedarikçi, tür) için <paramref name="asOf"/> tarihini kapsayan fiyat detayının fiyatı.
    /// Fiyat 0 ise <c>Note</c> nedeni açıklar (tanımlı değil / tarih aralığı kapsamıyor).
    /// </summary>
    private async Task<(decimal Price, string? Note)> ResolveUnitPriceAsync(int tedarikciId, int turId, DateOnly asOf)
    {
        var fiyatId = await _db.Fiyatlar.AsNoTracking()
            .Where(f => f.TedarikciId == tedarikciId && f.TurId == turId)
            .Select(f => (int?)f.Id)
            .FirstOrDefaultAsync();
        if (fiyatId is null)
            return (0m, "bu tür için tanımlı fiyat yok");

        var detaylar = await _db.FiyatDetaylari.AsNoTracking()
            .Where(d => d.FiyatId == fiyatId)
            .OrderByDescending(d => d.BaslangicTarihi).ThenByDescending(d => d.Id)
            .Select(d => new { d.BaslangicTarihi, d.BitisTarihi, d.SayfaBasiFiyat })
            .ToListAsync();

        var match = detaylar.FirstOrDefault(d => d.BaslangicTarihi <= asOf && asOf <= d.BitisTarihi);
        if (match is not null)
            return (match.SayfaBasiFiyat, null);

        if (detaylar.Count > 0)
        {
            var ranges = string.Join(", ", detaylar.Select(d =>
                $"{d.BaslangicTarihi:dd.MM.yyyy}–{d.BitisTarihi:dd.MM.yyyy}"));
            return (0m, $"fiyat tanımlı ama tarih aralığı ({ranges}) {asOf:dd.MM.yyyy} tarihini kapsamıyor");
        }

        return (0m, "bu tür için fiyat detayı girilmemiş");
    }
}

public record HakedisListDto(
    int Id, string Number, string? TedarikciAd, DateTime CreatedUtc,
    DateOnly PeriodStart, DateOnly PeriodEnd, int PrinterCount, long TotalPages, decimal TotalAmount);

public record TedarikciSecDto(int Id, string Ad, int PrinterCount, bool HasPriceList);

public record TaslakRequest(int TedarikciId);

public record HakedisTaslakDto(
    int TedarikciId, string TedarikciAd,
    DateOnly PeriodStart, DateOnly PeriodEnd,
    IReadOnlyList<TaslakRowDto> Rows, IReadOnlyList<string> Skipped, IReadOnlyList<string> Warnings);

public record TaslakRowDto(
    int PrinterId, string PrinterName, string? Model, int? TurId, string TurAd,
    long PreviousCounter, long CurrentCounter, DateTime? PreviousReadingUtc, DateTime CurrentReadingUtc,
    decimal UnitPrice, bool FirstHakedis);

public record HakedisKaydetRequest(
    int TedarikciId, string? TedarikciAd, DateOnly PeriodStart, DateOnly PeriodEnd, string? Note,
    List<HakedisKaydetRow>? Rows);

public record HakedisKaydetRow(
    int PrinterId, string? PrinterName, string? Model, int? TurId,
    long PreviousCounter, long CurrentCounter, decimal UnitPrice);

public record HakedisCreatedDto(int Id, string Number);

public record HakedisDetailDto(
    int Id, string Number, string? TedarikciAd, DateTime CreatedUtc, DateOnly PeriodStart, DateOnly PeriodEnd,
    string? Note, string? FromCompany, string? ToCompany,
    IReadOnlyList<HakedisLineDto> Lines, long TotalPages, decimal TotalAmount);

public record HakedisLineDto(
    string PrinterName, string? Model, string TurAd,
    long PreviousCounter, long CurrentCounter, long? Pages, decimal UnitPrice, decimal Amount);
