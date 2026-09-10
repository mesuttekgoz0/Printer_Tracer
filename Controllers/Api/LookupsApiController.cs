using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Form açılır listeleri için sabit/kısa referans verileri (tür + tedarikçi adları).
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public LookupsApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<LookupsDto>> Get()
    {
        var turler = await _db.Turler.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new OptionDto(t.Id, t.Ad))
            .ToListAsync();

        var tedarikciler = await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new OptionDto(t.Id, t.Ad))
            .ToListAsync();

        return new LookupsDto(turler, tedarikciler);
    }
}

public record OptionDto(int Id, string Ad);

public record LookupsDto(IReadOnlyList<OptionDto> Turler, IReadOnlyList<OptionDto> Tedarikciler);
