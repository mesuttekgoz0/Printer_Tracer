using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Data.Repositories;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Form açılır listeleri için sabit/kısa referans verileri (tür + tedarikçi adları).
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsApiController : ControllerBase
{
    private readonly ITurRepository _turler;
    private readonly ITedarikciRepository _tedarikciler;

    public LookupsApiController(ITurRepository turler, ITedarikciRepository tedarikciler)
    {
        _turler = turler;
        _tedarikciler = tedarikciler;
    }

    [HttpGet]
    public async Task<ActionResult<LookupsDto>> Get()
    {
        var ct = HttpContext.RequestAborted;
        var turler = (await _turler.GetAllAsync(ct)).Select(t => new OptionDto(t.Id, t.Ad)).ToList();
        var tedarikciler = (await _tedarikciler.GetAllAsync(ct)).Select(t => new OptionDto(t.Id, t.Ad)).ToList();

        return new LookupsDto(turler, tedarikciler);
    }
}

public record OptionDto(int Id, string Ad);

public record LookupsDto(IReadOnlyList<OptionDto> Turler, IReadOnlyList<OptionDto> Tedarikciler);
