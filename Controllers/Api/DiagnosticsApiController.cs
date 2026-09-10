using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Models;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers.Api;

/// <summary>SNMP tanılama JSON API'si.</summary>
[ApiController]
[Route("api/diagnostics")]
[Produces("application/json")]
public class DiagnosticsApiController : ControllerBase
{
    private readonly ISnmpService _snmp;

    public DiagnosticsApiController(ISnmpService snmp)
    {
        _snmp = snmp;
    }

    /// <summary>
    /// <paramref name="ip"/> adresine SNMP tanılama sorgusu yapar. <paramref name="oid"/>
    /// verilmişse o OID ayrıca elle sorgulanır.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SnmpDiagnosticResult>> Get(
        [FromQuery] string? ip, [FromQuery] string? oid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return Problem("IP adresi gerekli.", statusCode: 400);

        var result = await _snmp.DiagnoseAsync(ip.Trim(), ct);
        if (!string.IsNullOrWhiteSpace(oid))
        {
            result.ManualOid = oid.Trim();
            result.ManualOidResult = await _snmp.ProbeOidAsync(ip.Trim(), oid.Trim(), ct);
        }
        return result;
    }
}
