using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers.Api;

/// <summary>Tedarikçiler + tarihli fiyat listeleri JSON API'si.</summary>
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
        var today = DateOnly.FromDateTime(DateTime.Now);
        return await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new TedarikciListDto(
                t.Id, t.Ad, t.Not,
                t.Printers.Count,
                t.FiyatListeleri.Count,
                t.FiyatListeleri.OrderByDescending(f => f.Tarih).Select(f => (DateOnly?)f.Tarih).FirstOrDefault()))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TedarikciDetailDto>> Get(int id)
    {
        var t = await _db.Tedarikciler.AsNoTracking()
            .Include(x => x.Printers.OrderBy(p => p.Name)).ThenInclude(p => p.Tur)
            .Include(x => x.FiyatListeleri.OrderByDescending(f => f.Tarih)).ThenInclude(f => f.Satirlar)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        var turler = await _db.Turler.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new OptionDto(x.Id, x.Ad)).ToListAsync();
        var turAd = turler.ToDictionary(x => x.Id, x => x.Ad);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentListId = t.FiyatListeleri
            .Where(f => f.Tarih <= today)
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.Id)
            .Select(f => (int?)f.Id).FirstOrDefault();

        return new TedarikciDetailDto(
            t.Id, t.Ad, t.Not,
            t.Printers.Select(p => new TedarikciPrinterDto(p.Id, p.Name, p.IpAddress, p.TurAdi)).ToList(),
            t.FiyatListeleri.Select(f => new FiyatListesiDto(
                f.Id, f.ListeAdi, f.Tarih, f.Id == currentListId,
                turler.Select(tur => new FiyatSatiriDto(
                    tur.Id, tur.Ad,
                    f.Satirlar.FirstOrDefault(s => s.TurId == tur.Id)?.SayfaBasiFiyat ?? 0m)).ToList())).ToList(),
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
            new TedarikciListDto(t.Id, t.Ad, t.Not, 0, 0, null));
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

    [HttpPost("{id:int}/fiyat-listeleri")]
    public async Task<IActionResult> AddPriceList(int id, [FromBody] FiyatListesiRequest req)
    {
        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is null) return NotFound();

        var listeAdi = (req.ListeAdi ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(listeAdi))
            return Problem("Liste adı boş olamaz.", statusCode: 400);

        var liste = new FiyatListesi
        {
            TedarikciId = id,
            ListeAdi = listeAdi,
            Tarih = req.Tarih ?? DateOnly.FromDateTime(DateTime.Now),
        };
        foreach (var f in req.Fiyatlar ?? Enumerable.Empty<FiyatGirdiDto>())
            liste.Satirlar.Add(new FiyatSatiri { TurId = f.TurId, SayfaBasiFiyat = Math.Max(0m, f.Fiyat) });

        _db.FiyatListeleri.Add(liste);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Fiyat listesi güncelleme/silme — id kendi başına benzersiz olduğu için ayrı route.</summary>
[ApiController]
[Route("api/fiyat-listeleri")]
[Produces("application/json")]
public class FiyatListeleriApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public FiyatListeleriApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FiyatListesiRequest req)
    {
        var liste = await _db.FiyatListeleri.Include(f => f.Satirlar).FirstOrDefaultAsync(f => f.Id == id);
        if (liste is null) return NotFound();

        var listeAdi = (req.ListeAdi ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(listeAdi))
            return Problem("Liste adı boş olamaz.", statusCode: 400);

        liste.ListeAdi = listeAdi;
        if (req.Tarih is { } t) liste.Tarih = t;

        foreach (var f in req.Fiyatlar ?? Enumerable.Empty<FiyatGirdiDto>())
        {
            var fiyat = Math.Max(0m, f.Fiyat);
            var row = liste.Satirlar.FirstOrDefault(s => s.TurId == f.TurId);
            if (row is null) liste.Satirlar.Add(new FiyatSatiri { TurId = f.TurId, SayfaBasiFiyat = fiyat });
            else row.SayfaBasiFiyat = fiyat;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var liste = await _db.FiyatListeleri.FindAsync(id);
        if (liste is null) return NotFound();
        _db.FiyatListeleri.Remove(liste);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record TedarikciListDto(int Id, string Ad, string? Not, int PrinterCount, int PriceListCount, DateOnly? LatestPriceListDate);

public record TedarikciDetailDto(
    int Id, string Ad, string? Not,
    IReadOnlyList<TedarikciPrinterDto> Printers,
    IReadOnlyList<FiyatListesiDto> FiyatListeleri,
    IReadOnlyList<OptionDto> Turler);

public record TedarikciPrinterDto(int Id, string Name, string IpAddress, string TurAd);

public record FiyatListesiDto(int Id, string ListeAdi, DateOnly Tarih, bool IsCurrent, IReadOnlyList<FiyatSatiriDto> Satirlar);

public record FiyatSatiriDto(int TurId, string TurAd, decimal SayfaBasiFiyat);

public record TedarikciRequest(string? Ad, string? Not);

public record FiyatListesiRequest(string? ListeAdi, DateOnly? Tarih, List<FiyatGirdiDto>? Fiyatlar);

public record FiyatGirdiDto(int TurId, decimal Fiyat);
