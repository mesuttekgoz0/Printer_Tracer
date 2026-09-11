using System.Net;
using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Data.Repositories;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Yazıcı listesi JSON API'si (Next.js frontend için). Veri erişimi <see cref="IPrinterRepository"/>
/// ve <see cref="IPrintReadingRepository"/> üzerinden saklı yordamlarla yapılır.
/// </summary>
[ApiController]
[Route("api/printers")]
[Produces("application/json")]
public class PrintersApiController : ControllerBase
{
    private readonly IPrinterRepository _printers;
    private readonly IPrintReadingRepository _readings;
    private readonly ITurRepository _turler;
    private readonly ITedarikciRepository _tedarikciler;

    public PrintersApiController(
        IPrinterRepository printers, IPrintReadingRepository readings,
        ITurRepository turler, ITedarikciRepository tedarikciler)
    {
        _printers = printers;
        _readings = readings;
        _turler = turler;
        _tedarikciler = tedarikciler;
    }

    /// <summary>Kayıtlı tüm yazıcılar, tür/tedarikçi ve son okuma bilgisiyle.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrinterDto>>> GetAll()
    {
        var printers = await _printers.GetAllAsync(HttpContext.RequestAborted);
        var readingAgg = await _readings.GetAggregateAllAsync(HttpContext.RequestAborted);

        var result = new List<PrinterDto>();
        foreach (var p in printers)
        {
            long? latestCounter = null;
            DateTime? latestUtc = null;
            var count = 0;
            if (readingAgg.TryGetValue(p.Id, out var agg))
            {
                count = agg.Count;
                latestUtc = agg.LatestUtc;
                latestCounter = (await _readings.GetLatestAsync(p.Id, HttpContext.RequestAborted))?.PageCount;
            }

            result.Add(new PrinterDto(
                p.Id, p.Name, p.IpAddress, p.Model,
                p.TurId, p.TurAdi,
                p.TedarikciId, p.Tedarikci?.Ad,
                count, latestUtc, latestCounter));
        }

        return result;
    }

    /// <summary>Yeni yazıcı ekler (yalnızca DB'ye yazılır, SNMP okuması yapılmaz).</summary>
    [HttpPost]
    public async Task<ActionResult<PrinterDto>> Create([FromBody] PrinterCreateRequest req)
    {
        var ct = HttpContext.RequestAborted;
        var name = (req.Name ?? string.Empty).Trim();
        var ip = (req.IpAddress ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return Problem("Yazıcı adı boş olamaz.", statusCode: 400);
        if (!IPAddress.TryParse(ip, out _))
            return Problem($"\"{ip}\" geçerli bir IP adresi değil.", statusCode: 400);
        if (await _printers.ExistsByIpAsync(ip, ct))
            return Problem($"{ip} zaten kayıtlı bir yazıcı.", statusCode: 409);

        var turId = await ValidTurOrNull(req.TurId, ct);
        var tedarikciId = await ValidTedarikciOrNull(req.TedarikciId, ct);

        int id;
        try
        {
            id = await _printers.InsertAsync(name, ip, turId, tedarikciId, ct);
        }
        catch (DuplicateIpAddressException ex)
        {
            return Problem(ex.Message, statusCode: 409);
        }

        return CreatedAtAction(nameof(GetAll), new { id }, await ToDto(id));
    }

    [HttpPatch("{id:int}/name")]
    public async Task<IActionResult> SetName(int id, [FromBody] SetNameRequest req)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Problem("Yazıcı adı boş olamaz.", statusCode: 400);

        if (!await _printers.UpdateNameAsync(id, name, HttpContext.RequestAborted))
            return NotFound();
        return Ok(await ToDto(id));
    }

    [HttpPatch("{id:int}/type")]
    public async Task<IActionResult> SetType(int id, [FromBody] SetTurRequest req)
    {
        var ct = HttpContext.RequestAborted;
        var turId = await ValidTurOrNull(req.TurId, ct);
        if (!await _printers.UpdateTurAsync(id, turId, ct))
            return NotFound();
        return Ok(await ToDto(id));
    }

    [HttpPatch("{id:int}/supplier")]
    public async Task<IActionResult> SetSupplier(int id, [FromBody] SetTedarikciRequest req)
    {
        var ct = HttpContext.RequestAborted;
        var tedarikciId = await ValidTedarikciOrNull(req.TedarikciId, ct);
        if (!await _printers.UpdateTedarikciAsync(id, tedarikciId, ct))
            return NotFound();
        return Ok(await ToDto(id));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _printers.DeleteAsync(id, HttpContext.RequestAborted))
            return NotFound();
        return NoContent();
    }

    private async Task<int?> ValidTurOrNull(int? id, CancellationToken ct)
        => id is int x && await _turler.GetByIdAsync(x, ct) is not null ? x : null;

    private async Task<int?> ValidTedarikciOrNull(int? id, CancellationToken ct)
        => id is int x && await _tedarikciler.GetByIdAsync(x, ct) is not null ? x : null;

    private async Task<PrinterDto> ToDto(int id)
    {
        var ct = HttpContext.RequestAborted;
        var p = (await _printers.GetByIdAsync(id, ct))!;
        var count = await _readings.CountByPrinterAsync(id, ct);
        var last = await _readings.GetLatestAsync(id, ct);

        return new PrinterDto(
            p.Id, p.Name, p.IpAddress, p.Model,
            p.TurId, p.TurAdi,
            p.TedarikciId, p.Tedarikci?.Ad,
            count, last?.TimestampUtc, last?.PageCount);
    }
}

public record PrinterDto(
    int Id,
    string Name,
    string IpAddress,
    string? Model,
    int? TurId,
    string TurAd,
    int? TedarikciId,
    string? TedarikciAd,
    int ReadingCount,
    DateTime? LatestReadingUtc,
    long? LatestCounter);

public record PrinterCreateRequest(string? Name, string? IpAddress, int? TurId, int? TedarikciId);

public record SetNameRequest(string? Name);

public record SetTurRequest(int? TurId);

public record SetTedarikciRequest(int? TedarikciId);
