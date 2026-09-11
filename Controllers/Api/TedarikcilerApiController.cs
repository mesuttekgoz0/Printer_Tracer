using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Data.Repositories;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Tedarikçiler + fiyatlandırma JSON API'si.
/// Fiyat modeli: <see cref="Fiyat"/> (master: tedarikçi + tür) → <see cref="FiyatDetay"/>
/// (tarih aralığı + sayfa-başı fiyat). Veri erişimi saklı yordamlarla (<see cref="ITedarikciRepository"/>,
/// <see cref="IFiyatRepository"/>) yapılır.
/// </summary>
[ApiController]
[Route("api/tedarikciler")]
[Produces("application/json")]
public class TedarikcilerApiController : ControllerBase
{
    private readonly ITedarikciRepository _tedarikciler;
    private readonly IPrinterRepository _printers;
    private readonly ITurRepository _turler;
    private readonly IFiyatRepository _fiyatlar;

    public TedarikcilerApiController(
        ITedarikciRepository tedarikciler, IPrinterRepository printers,
        ITurRepository turler, IFiyatRepository fiyatlar)
    {
        _tedarikciler = tedarikciler;
        _printers = printers;
        _turler = turler;
        _fiyatlar = fiyatlar;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TedarikciListDto>>> GetAll()
    {
        var rows = await _tedarikciler.GetAllAsync(HttpContext.RequestAborted);
        return rows.Select(r => new TedarikciListDto(r.Id, r.Ad, r.Not, r.PrinterCount, r.FiyatSayisi, r.DetaySayisi)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TedarikciDetailDto>> Get(int id)
    {
        var ct = HttpContext.RequestAborted;
        var t = await _tedarikciler.GetByIdAsync(id, ct);
        if (t is null) return NotFound();

        var printers = await _printers.ListByTedarikciAsync(id, ct);
        var fiyatlar = await _fiyatlar.ListByTedarikciAsync(id, ct);
        var detaylar = await _fiyatlar.ListDetaylarByTedarikciAsync(id, ct);
        var turler = (await _turler.GetAllAsync(ct)).Select(x => new OptionDto(x.Id, x.Ad)).ToList();

        var today = DateOnly.FromDateTime(DateTime.Now);

        return new TedarikciDetailDto(
            t.Id, t.Ad, t.Not,
            printers.Select(p => new TedarikciPrinterDto(p.Id, p.Name, p.IpAddress, p.TurAdi)).ToList(),
            fiyatlar
                .OrderBy(f => f.TurId)
                .Select(f => new FiyatDto(
                    f.Id, f.TurId, f.Tur?.Ad ?? Tur.Belirtilmemis,
                    detaylar.Where(d => d.FiyatId == f.Id)
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

        var id = await _tedarikciler.InsertAsync(ad, Clean(req.Not), HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id }, new TedarikciListDto(id, ad, Clean(req.Not), 0, 0, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TedarikciRequest req)
    {
        var ad = (req.Ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ad))
            return Problem("Tedarikçi adı boş olamaz.", statusCode: 400);

        if (!await _tedarikciler.UpdateAsync(id, ad, Clean(req.Not), HttpContext.RequestAborted))
            return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _tedarikciler.DeleteAsync(id, HttpContext.RequestAborted))
            return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Yeni fiyat: bir tür seçilir, tarih aralığı + tek fiyat girilir.
    /// (Tedarikçi, tür) için master yoksa oluşturulur; sonra detay eklenir.
    /// </summary>
    [HttpPost("{id:int}/fiyatlar")]
    public async Task<IActionResult> AddFiyat(int id, [FromBody] AddFiyatRequest req)
    {
        var ct = HttpContext.RequestAborted;
        if (await _tedarikciler.GetByIdAsync(id, ct) is null) return NotFound();

        if (await _turler.GetByIdAsync(req.TurId, ct) is null)
            return Problem("Geçerli bir tür seçin.", statusCode: 400);
        if (req.BaslangicTarihi is not { } bas || req.BitisTarihi is not { } bit)
            return Problem("Başlangıç ve bitiş tarihi gerekli.", statusCode: 400);
        if (bas > bit)
            return Problem("Başlangıç tarihi bitiş tarihinden sonra olamaz.", statusCode: 400);

        var masterId = await _fiyatlar.FindMasterAsync(id, req.TurId, ct)
                       ?? await _fiyatlar.InsertMasterAsync(id, req.TurId, ct);

        await _fiyatlar.InsertDetayAsync(masterId, req.TurId, bas, bit, Math.Max(0m, req.SayfaBasiFiyat), ct);
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
    private readonly IFiyatRepository _fiyatlar;

    public FiyatDetaylariApiController(IFiyatRepository fiyatlar)
    {
        _fiyatlar = fiyatlar;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FiyatDetayRequest req)
    {
        var ct = HttpContext.RequestAborted;
        if (await _fiyatlar.GetDetayByIdAsync(id, ct) is null) return NotFound();

        if (req.BaslangicTarihi is not { } bas || req.BitisTarihi is not { } bit)
            return Problem("Başlangıç ve bitiş tarihi gerekli.", statusCode: 400);
        if (bas > bit)
            return Problem("Başlangıç tarihi bitiş tarihinden sonra olamaz.", statusCode: 400);

        await _fiyatlar.UpdateDetayAsync(id, bas, bit, Math.Max(0m, req.SayfaBasiFiyat), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ct = HttpContext.RequestAborted;
        var d = await _fiyatlar.GetDetayByIdAsync(id, ct);
        if (d is null) return NotFound();

        await _fiyatlar.DeleteDetayAsync(id, ct);

        // Master'ın başka detayı kalmadıysa master'ı da sil.
        if (await _fiyatlar.CountDetaylarByFiyatIdAsync(d.FiyatId, ct) == 0)
            await _fiyatlar.DeleteMasterAsync(d.FiyatId, ct);

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
