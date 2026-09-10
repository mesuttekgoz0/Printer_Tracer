using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers.Api;

/// <summary>Ağ tarama JSON API'si — SNMP ile yerel ağdaki yazıcıları bulur.</summary>
[ApiController]
[Route("api/discovery")]
[Produces("application/json")]
public class DiscoveryApiController : ControllerBase
{
    private readonly PrinterDiscoveryService _discovery;
    private readonly AppDbContext _db;

    public DiscoveryApiController(PrinterDiscoveryService discovery, AppDbContext db)
    {
        _discovery = discovery;
        _db = db;
    }

    /// <summary>Sunucunun aktif arayüzlerinden türetilen taranabilir ağlar.</summary>
    [HttpGet("subnets")]
    public ActionResult<SubnetsDto> Subnets()
    {
        var subnets = _discovery.GetLocalSubnets()
            .Select(s => new SubnetDto(s.Cidr, s.InterfaceName, s.ServerIp, s.HostCount, s.Scannable, s.HasGateway))
            .ToList();
        var suggested = (subnets.FirstOrDefault(s => s.Scannable && s.HasGateway)
                         ?? subnets.FirstOrDefault(s => s.Scannable))?.Cidr;
        return new SubnetsDto(subnets, suggested);
    }

    /// <summary>Verilen CIDR'i (yoksa önerilen ağı) SNMP ile tarar; yazıcıları döndürür.</summary>
    [HttpPost("scan")]
    public async Task<ActionResult<IEnumerable<DiscoveredDto>>> Scan([FromBody] ScanRequest? req)
    {
        var cidr = req?.Cidr?.Trim();
        if (string.IsNullOrWhiteSpace(cidr))
        {
            var locals = _discovery.GetLocalSubnets();
            cidr = (locals.FirstOrDefault(s => s.Scannable && s.HasGateway)
                    ?? locals.FirstOrDefault(s => s.Scannable))?.Cidr;
            if (cidr is null)
                return Problem("Taranabilir bir yerel ağ bulunamadı.", statusCode: 400);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        cts.CancelAfter(TimeSpan.FromSeconds(45));

        IReadOnlyList<DiscoveredDevice> devices;
        try
        {
            devices = await _discovery.ScanAsync(cidr, cts.Token);
        }
        catch (ArgumentException ex)
        {
            return Problem(ex.Message, statusCode: 400);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Problem("Tarama zaman aşımına uğradı (45sn).", statusCode: 504);
        }

        var registered = await _db.Printers.AsNoTracking()
            .Select(p => p.IpAddress)
            .ToListAsync(HttpContext.RequestAborted);
        var regSet = new HashSet<string>(registered, StringComparer.OrdinalIgnoreCase);

        return devices.Select(d => new DiscoveredDto(
            d.IpAddress,
            d.SysName,
            d.SysDescr,
            d.SnmpVersion,
            d.HasPageCounter,
            d.DetectedBrand,
            regSet.Contains(d.IpAddress))).ToList();
    }
}

public record SubnetsDto(IReadOnlyList<SubnetDto> Subnets, string? Suggested);

public record SubnetDto(string Cidr, string InterfaceName, string ServerIp, long HostCount, bool Scannable, bool HasGateway);

public record ScanRequest(string? Cidr);

public record DiscoveredDto(
    string IpAddress, string? SysName, string? SysDescr, string SnmpVersion,
    bool HasPageCounter, string? DetectedBrand, bool AlreadyRegistered);
