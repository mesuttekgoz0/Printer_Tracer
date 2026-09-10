using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers.Api;

/// <summary>
/// Yazıcı listesi JSON API'si (Next.js frontend için). Mevcut <see cref="PrintersController"/>
/// (Razor) ile aynı iş mantığını kullanır; sadece JSON döner.
/// </summary>
[ApiController]
[Route("api/printers")]
[Produces("application/json")]
public class PrintersApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public PrintersApiController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Kayıtlı tüm yazıcılar, tür/tedarikçi ve son okuma bilgisiyle.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrinterDto>>> GetAll()
    {
        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .Include(p => p.Tedarikci)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var readingAgg = await _db.PrintReadings.AsNoTracking()
            .GroupBy(r => r.PrinterId)
            .Select(g => new
            {
                PrinterId = g.Key,
                Count = g.Count(),
                LatestUtc = g.Max(r => r.TimestampUtc),
            })
            .ToDictionaryAsync(x => x.PrinterId);

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
                latestCounter = await _db.PrintReadings.AsNoTracking()
                    .Where(r => r.PrinterId == p.Id)
                    .OrderByDescending(r => r.TimestampUtc)
                    .Select(r => (long?)r.PageCount)
                    .FirstOrDefaultAsync();
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
        var name = (req.Name ?? string.Empty).Trim();
        var ip = (req.IpAddress ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return Problem("Yazıcı adı boş olamaz.", statusCode: 400);
        if (!IPAddress.TryParse(ip, out _))
            return Problem($"\"{ip}\" geçerli bir IP adresi değil.", statusCode: 400);
        if (await _db.Printers.AnyAsync(p => p.IpAddress == ip))
            return Problem($"{ip} zaten kayıtlı bir yazıcı.", statusCode: 409);

        var turId = await ValidLookupOrNull(req.TurId, id => _db.Turler.AnyAsync(t => t.Id == id));
        var tedarikciId = await ValidLookupOrNull(req.TedarikciId, id => _db.Tedarikciler.AnyAsync(t => t.Id == id));

        var printer = new Printer { Name = name, IpAddress = ip, TurId = turId, TedarikciId = tedarikciId };
        _db.Printers.Add(printer);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Problem($"{ip} zaten kayıtlı bir yazıcı.", statusCode: 409);
        }

        return CreatedAtAction(nameof(GetAll), new { id = printer.Id }, await ToDto(printer.Id));
    }

    [HttpPatch("{id:int}/name")]
    public async Task<IActionResult> SetName(int id, [FromBody] SetNameRequest req)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Problem("Yazıcı adı boş olamaz.", statusCode: 400);

        var printer = await _db.Printers.FindAsync(id);
        if (printer is null)
            return NotFound();

        printer.Name = name;
        await _db.SaveChangesAsync();
        return Ok(await ToDto(id));
    }

    [HttpPatch("{id:int}/type")]
    public async Task<IActionResult> SetType(int id, [FromBody] SetTurRequest req)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is null)
            return NotFound();

        printer.TurId = await ValidLookupOrNull(req.TurId, x => _db.Turler.AnyAsync(t => t.Id == x));
        await _db.SaveChangesAsync();
        return Ok(await ToDto(id));
    }

    [HttpPatch("{id:int}/supplier")]
    public async Task<IActionResult> SetSupplier(int id, [FromBody] SetTedarikciRequest req)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is null)
            return NotFound();

        printer.TedarikciId = await ValidLookupOrNull(req.TedarikciId, x => _db.Tedarikciler.AnyAsync(t => t.Id == x));
        await _db.SaveChangesAsync();
        return Ok(await ToDto(id));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is null)
            return NotFound();

        _db.Printers.Remove(printer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static async Task<int?> ValidLookupOrNull(int? id, Func<int, Task<bool>> exists)
        => id is int x && await exists(x) ? x : null;

    private async Task<PrinterDto> ToDto(int id)
    {
        var p = await _db.Printers.AsNoTracking()
            .Include(x => x.Tur)
            .Include(x => x.Tedarikci)
            .FirstAsync(x => x.Id == id);

        var count = await _db.PrintReadings.AsNoTracking().CountAsync(r => r.PrinterId == id);
        var last = await _db.PrintReadings.AsNoTracking()
            .Where(r => r.PrinterId == id)
            .OrderByDescending(r => r.TimestampUtc)
            .Select(r => new { r.TimestampUtc, r.PageCount })
            .FirstOrDefaultAsync();

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
