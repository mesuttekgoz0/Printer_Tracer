using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Tedarikçiler + fiyatlandırma JSON API'si.
/// Fiyat modeli: <see cref="Fiyat"/> (master: tedarikçi + tür) → <see cref="FiyatDetay"/>
/// (tarih aralığı + sayfa-başı fiyat).
/// </summary>
[ApiController]
[Route("api/tedarikciler")]
[Produces("application/json")]
public class TedarikcilerApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public TedarikcilerApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TedarikciListDto>>> GetAll()
    {
        return await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new TedarikciListDto(
                t.Id, t.Ad, t.Not,
                t.Printers.Count,
                t.Fiyatlar.Count,
                t.Fiyatlar.SelectMany(f => f.Detaylar).Count()))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TedarikciDetailDto>> Get(int id)
    {
        var t = await _db.Tedarikciler.AsNoTracking()
            .Include(x => x.Printers.OrderBy(p => p.Name)).ThenInclude(p => p.Tur)
            .Include(x => x.Fiyatlar).ThenInclude(f => f.Tur)
            .Include(x => x.Fiyatlar).ThenInclude(f => f.Detaylar)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        var turler = await _db.Turler.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new OptionDto(x.Id, x.Ad)).ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);

        return new TedarikciDetailDto(
            t.Id, t.Ad, t.Not,
            t.Printers.Select(p => new TedarikciPrinterDto(p.Id, p.Name, p.IpAddress, p.TurAdi)).ToList(),
            t.Fiyatlar
                .OrderBy(f => f.TurId)
                .Select(f => new FiyatDto(
                    f.Id, f.TurId, f.Tur?.Ad ?? Tur.Belirtilmemis,
                    f.Detaylar
                        .OrderByDescending(d => d.BaslangicTarihi).ThenByDescending(d => d.Id)
                        .Select(d => new FiyatDetayDto(
                            d.Id, d.BaslangicTarihi, d.BitisTarihi, d.SayfaBasiFiyat, d.Kapsar(today)))
                        .ToList()))
                .ToList(),
            turler);
    }

    [HttpPost]
    public async Task<ActionResult<TedarikciListDto>> Create([FromBody] TedarikciRequest req)
    {
        var ad = (req.Ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ad))
            return Problem("Tedarikçi adı boş olamaz.", statusCode: 400);

        var t = new Tedarikci { Ad = ad, Not = Clean(req.Not) };
        _db.Tedarikciler.Add(t);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = t.Id },
            new TedarikciListDto(t.Id, t.Ad, t.Not, 0, 0, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TedarikciRequest req)
    {
        var ad = (req.Ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ad))
            return Problem("Tedarikçi adı boş olamaz.", statusCode: 400);

        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is null) return NotFound();
        t.Ad = ad;
        t.Not = Clean(req.Not);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is null) return NotFound();
        _db.Tedarikciler.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Yeni fiyat: bir tür seçilir, tarih aralığı + tek fiyat girilir.
    /// (Tedarikçi, tür) için master yoksa oluşturulur; sonra detay eklenir.
    /// </summary>
    [HttpPost("{id:int}/fiyatlar")]
    public async Task<IActionResult> AddFiyat(int id, [FromBody] AddFiyatRequest req)
    {
        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is null) return NotFound();

        if (!await _db.Turler.AnyAsync(x => x.Id == req.TurId))
            return Problem("Geçerli bir tür seçin.", statusCode: 400);
        if (req.BaslangicTarihi is not { } bas || req.BitisTarihi is not { } bit)
            return Problem("Başlangıç ve bitiş tarihi gerekli.", statusCode: 400);
        if (bas > bit)
            return Problem("Başlangıç tarihi bitiş tarihinden sonra olamaz.", statusCode: 400);

        var master = await _db.Fiyatlar
            .FirstOrDefaultAsync(f => f.TedarikciId == id && f.TurId == req.TurId);
        if (master is null)
        {
            master = new Fiyat { TedarikciId = id, TurId = req.TurId };
            _db.Fiyatlar.Add(master);
        }

        master.Detaylar.Add(new FiyatDetay
        {
            TurId = req.TurId,
            BaslangicTarihi = bas,
            BitisTarihi = bit,
            SayfaBasiFiyat = Math.Max(0m, req.SayfaBasiFiyat),
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Tek bir fiyat detay satırının güncellenmesi/silinmesi.</summary>
[ApiController]
[Route("api/fiyat-detaylari")]
[Produces("application/json")]
public class FiyatDetaylariApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public FiyatDetaylariApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FiyatDetayRequest req)
    {
        var d = await _db.FiyatDetaylari.FindAsync(id);
        if (d is null) return NotFound();

        if (req.BaslangicTarihi is not { } bas || req.BitisTarihi is not { } bit)
            return Problem("Başlangıç ve bitiş tarihi gerekli.", statusCode: 400);
        if (bas > bit)
            return Problem("Başlangıç tarihi bitiş tarihinden sonra olamaz.", statusCode: 400);

        d.BaslangicTarihi = bas;
        d.BitisTarihi = bit;
        d.SayfaBasiFiyat = Math.Max(0m, req.SayfaBasiFiyat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.FiyatDetaylari.FindAsync(id);
        if (d is null) return NotFound();

        _db.FiyatDetaylari.Remove(d);
        await _db.SaveChangesAsync();

        // Master'ın başka detayı kalmadıysa master'ı da sil.
        var kalan = await _db.FiyatDetaylari.CountAsync(x => x.FiyatId == d.FiyatId);
        if (kalan == 0)
        {
            var master = await _db.Fiyatlar.FindAsync(d.FiyatId);
            if (master is not null)
            {
                _db.Fiyatlar.Remove(master);
                await _db.SaveChangesAsync();
            }
        }

        return NoContent();
    }
}

public record TedarikciListDto(
    int Id, string Ad, string? Not, int PrinterCount, int FiyatSayisi, int DetaySayisi);

public record TedarikciDetailDto(
    int Id, string Ad, string? Not,
    IReadOnlyList<TedarikciPrinterDto> Printers,
    IReadOnlyList<FiyatDto> Fiyatlar,
    IReadOnlyList<OptionDto> Turler);

public record TedarikciPrinterDto(int Id, string Name, string IpAddress, string TurAd);

public record FiyatDto(int Id, int TurId, string TurAd, IReadOnlyList<FiyatDetayDto> Detaylar);

public record FiyatDetayDto(
    int Id, DateOnly BaslangicTarihi, DateOnly BitisTarihi, decimal SayfaBasiFiyat, bool IsCurrent);

public record TedarikciRequest(string? Ad, string? Not);

public record AddFiyatRequest(int TurId, DateOnly? BaslangicTarihi, DateOnly? BitisTarihi, decimal SayfaBasiFiyat);

public record FiyatDetayRequest(DateOnly? BaslangicTarihi, DateOnly? BitisTarihi, decimal SayfaBasiFiyat);
