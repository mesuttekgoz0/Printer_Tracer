using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Models;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers;

public class DiagnosticsController : Controller
{
    private readonly ISnmpService _snmpService;

    public DiagnosticsController(ISnmpService snmpService)
    {
        _snmpService = snmpService;
    }

    /// <summary>
    /// Bir yazıcı IP'sine SNMP tanılama sorgusu yapar; doğru sayfa sayacı OID'ini bulmaya yarar.
    /// <paramref name="oid"/> verilmişse, o OID de ayrıca (bilinen listeden/marka tespitinden
    /// bağımsız) elle sorgulanır — sysDescr boş dönen ya da marka tanınmayan cihazlarda
    /// bilinmeyen bir OID'i denemek için kullanılır.
    /// </summary>
    public async Task<IActionResult> Index(string? ip, string? oid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return View(model: null);

        var result = await _snmpService.DiagnoseAsync(ip.Trim(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(oid))
        {
            result.ManualOid = oid.Trim();
            result.ManualOidResult = await _snmpService.ProbeOidAsync(ip.Trim(), oid.Trim(), cancellationToken);
        }

        return View(result);
    }
}
