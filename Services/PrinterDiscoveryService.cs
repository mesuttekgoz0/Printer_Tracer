using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Sunucunun bulunduğu yerel ağ(lar)ı SNMP ile tarayıp yazıcıları bulur.
/// IP adresini elle bulmak istemeyen kullanıcı için "Ağı Tara" özelliğini besler.
/// </summary>
public class PrinterDiscoveryService
{
    private readonly ISnmpService _snmp;
    private readonly ILogger<PrinterDiscoveryService> _logger;

    /// <summary>Aynı anda yoklanacak host sayısı.</summary>
    private const int Concurrency = 64;

    /// <summary>Tarama sırasında her host için SNMP timeout'u (kısa tutulur).</summary>
    private const int ProbeTimeoutMs = 700;

    /// <summary>Bundan geniş bir maske reddedilir (çok fazla host).</summary>
    private const int MinPrefix = 22; // /22 = 1022 host

    /// <summary>sysDescr'ında bunlardan biri geçen SNMP cihazı (sayfa sayacı yoksa) yazıcı sayılmaz.</summary>
    private static readonly string[] NonPrinterKeywords =
    {
        "switch", "router", "access point", "firewall", "gateway", "wireless controller",
        "camera", "nvr", "ip cam", "storage", "nas ",
    };

    public PrinterDiscoveryService(ISnmpService snmp, ILogger<PrinterDiscoveryService> logger)
    {
        _snmp = snmp;
        _logger = logger;
    }

    /// <summary>Sunucunun aktif IPv4 arayüzlerinden türetilen taranabilir CIDR listesi.</summary>
    public IReadOnlyList<DiscoverySubnet> GetLocalSubnets()
    {
        var result = new List<DiscoverySubnet>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var ipProps = nic.GetIPProperties();
            // Ağ geçidi olan arayüz "gerçek" ağdır (VirtualBox host-only vb. geçitsizdir).
            var hasGateway = ipProps.GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

            foreach (var ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;

                var prefix = ua.PrefixLength;
                if (prefix is <= 0 or >= 32) continue;

                var addrBytes = ua.Address.GetAddressBytes();
                // 169.254.x.x (APIPA) atla
                if (addrBytes[0] == 169 && addrBytes[1] == 254) continue;

                var network = ToUInt(addrBytes) & PrefixMask(prefix);
                var cidr = $"{ToIp(network)}/{prefix}";
                var hostCount = HostCount(prefix);

                result.Add(new DiscoverySubnet
                {
                    Cidr = cidr,
                    InterfaceName = nic.Name,
                    ServerIp = ua.Address.ToString(),
                    HostCount = hostCount,
                    Scannable = prefix >= MinPrefix,
                    HasGateway = hasGateway,
                });
            }
        }

        return result
            .GroupBy(s => s.Cidr)
            .Select(g => g.First())
            // Ağ geçidi olan + taranabilir olanlar önce (önerilen bunlardan seçilir).
            .OrderByDescending(s => s.Scannable)
            .ThenByDescending(s => s.HasGateway)
            .ToList();
    }

    /// <summary>Verilen CIDR'deki tüm host'ları SNMP ile yoklar; yazıcı olanları döndürür.</summary>
    public async Task<IReadOnlyList<DiscoveredDevice>> ScanAsync(string cidr, CancellationToken ct)
    {
        var (network, prefix) = ParseCidr(cidr);
        if (prefix < MinPrefix)
            throw new ArgumentException($"Ağ çok geniş (/{prefix}). En küçük /{MinPrefix} taranabilir.");

        var count = HostCount(prefix);
        var mask = PrefixMask(prefix);
        var baseAddr = network & mask;

        var found = new List<DiscoveredDevice>();
        var gate = new SemaphoreSlim(Concurrency);
        var tasks = new List<Task>();

        for (uint i = 1; i <= count; i++)
        {
            var addrUint = baseAddr + i;
            var ip = ToIp(addrUint).ToString();

            await gate.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var probe = await _snmp.QuickProbeAsync(ip, ProbeTimeoutMs, ct);
                    if (probe is null) return;

                    var descr = probe.SysDescr ?? string.Empty;
                    var isNonPrinter = NonPrinterKeywords
                        .Any(k => descr.Contains(k, StringComparison.OrdinalIgnoreCase));

                    // Yazıcı ölçütü: standart sayfa sayacına yanıt (kesin), YA DA
                    // (marka/anahtar kelime eşleşmesi VE switch/router gibi bir cihaz olmaması).
                    var looksLikePrinter =
                        probe.HasPageCounter
                        || (!isNonPrinter && (probe.DetectedBrand is not null
                            || descr.Contains("print", StringComparison.OrdinalIgnoreCase)));
                    if (!looksLikePrinter) return;

                    lock (found)
                    {
                        found.Add(new DiscoveredDevice
                        {
                            IpAddress = ip,
                            SysName = probe.SysName,
                            SysDescr = probe.SysDescr,
                            SnmpVersion = probe.RespondingVersion,
                            HasPageCounter = probe.HasPageCounter,
                            DetectedBrand = probe.DetectedBrand,
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "{Ip} yoklaması başarısız.", ip);
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        return found
            .OrderBy(d => ToUInt(IPAddress.Parse(d.IpAddress).GetAddressBytes()))
            .ToList();
    }

    // ---- CIDR / IP yardımcıları ----

    private static (uint Network, int Prefix) ParseCidr(string cidr)
    {
        var parts = (cidr ?? string.Empty).Trim().Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var addr)
            || addr.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out var prefix)
            || prefix is < 0 or > 32)
        {
            throw new ArgumentException($"Geçersiz CIDR: \"{cidr}\". Örnek: 192.168.1.0/24");
        }

        var net = ToUInt(addr.GetAddressBytes()) & PrefixMask(prefix);
        return (net, prefix);
    }

    private static uint HostCount(int prefix) => prefix >= 31 ? 0u : (1u << (32 - prefix)) - 2;

    private static uint PrefixMask(int prefix) => prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);

    private static uint ToUInt(byte[] b) => ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];

    private static IPAddress ToIp(uint v) =>
        new(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
}

public class DiscoverySubnet
{
    public string Cidr { get; set; } = string.Empty;
    public string InterfaceName { get; set; } = string.Empty;
    public string ServerIp { get; set; } = string.Empty;
    public long HostCount { get; set; }
    public bool Scannable { get; set; }
    public bool HasGateway { get; set; }
}

public class DiscoveredDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string? SysName { get; set; }
    public string? SysDescr { get; set; }
    public string SnmpVersion { get; set; } = string.Empty;
    public bool HasPageCounter { get; set; }
    public string? DetectedBrand { get; set; }
}
