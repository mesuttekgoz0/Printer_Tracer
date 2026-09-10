using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Options;
using YaziciTakip.Configuration;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

public class SnmpService : ISnmpService
{
    private readonly SnmpOptions _options;
    private readonly ILogger<SnmpService> _logger;
    private readonly int _retries;

    public SnmpService(IOptions<SnmpOptions> options, ILogger<SnmpService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _retries = Math.Max(0, _options.Retries);
    }

    public async Task<long?> GetPageCountAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            _logger.LogWarning("Geçersiz IP adresi: {Ip}", ipAddress);
            return null;
        }

        var endpoint = new IPEndPoint(ip, _options.Port);
        var primary = ParseVersion(_options.Version);

        var value = await TryGetAsync(primary, endpoint, ipAddress, cancellationToken);
        if (value is not null)
            return value;

        if (_options.FallbackToV1 && primary != VersionCode.V1)
        {
            _logger.LogInformation("{Ip}: {Primary} başarısız, V1 deneniyor.", ipAddress, primary);
            value = await TryGetAsync(VersionCode.V1, endpoint, ipAddress, cancellationToken);
        }

        return value;
    }

    public async Task<string?> GetModelAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return null;

        var endpoint = new IPEndPoint(ip, _options.Port);
        var community = new OctetString(_options.Community);

        // Config'deki sürümü önce dene, sonra diğerini (v1/v2c fallback).
        var primary = ParseVersion(_options.Version);
        var order = primary == VersionCode.V1
            ? new[] { VersionCode.V1, VersionCode.V2 }
            : new[] { VersionCode.V2, VersionCode.V1 };

        foreach (var version in order)
        {
            var value = (await GetIdentityAsync(version, endpoint, community, SysDescrOid, cancellationToken))?.Value;
            var cleaned = CleanModel(value);
            if (cleaned is not null)
                return cleaned;
        }

        return null;
    }

    /// <summary>sysDescr'i tek satıra indirger, fazla boşlukları temizler, en fazla 250 karaktere kısaltır.</summary>
    private static string? CleanModel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var oneLine = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        oneLine = oneLine.Trim();
        if (oneLine.Length == 0)
            return null;

        return oneLine.Length > 250 ? oneLine[..250] : oneLine;
    }

    private async Task<long?> TryGetAsync(
        VersionCode version, IPEndPoint endpoint, string ipAddress, CancellationToken cancellationToken)
    {
        // GetSingleAsync geçici hatalarda kendi içinde birkaç kez dener (UDP paket kaybı).
        var v = await GetSingleAsync(version, endpoint, new OctetString(_options.Community),
            _options.PageCountOid, _options.TimeoutSeconds * 1000, cancellationToken, _retries);

        if (v?.Value is null)
        {
            _logger.LogDebug("{Ip} ({Version}): sayaç yanıtı yok.", ipAddress, version);
            return null;
        }

        if (long.TryParse(v.Value, out var pageCount))
            return pageCount;

        _logger.LogDebug("{Ip} ({Version}): sayaç değeri sayıya çevrilemedi: {Raw} ({Type})",
            ipAddress, version, v.Value, v.Type);
        return null;
    }

    // Bilinen "toplam sayfa" OID adayları. Brand=null → markadan bağımsız (evrensel), her yazıcıda denenir.
    // Brand dolu olanlar SADECE sysDescr'den o marka tespit edildiğinde denenir/gösterilir
    // (ör. Olivetti bir yazıcıda HP'ye özel OID'ler artık hiç denenmez/gösterilmez).
    private static readonly (string Oid, string Label, string? Brand)[] KnownPageCountOids =
    {
        ("1.3.6.1.2.1.43.10.2.1.4.1.1", "Printer-MIB prtMarkerLifeCount (standart, tüm markalar)", null),
        ("1.3.6.1.4.1.11.2.3.9.4.2.1.1.16.1.0", "HP toplam motor sayfa sayısı", "HP"),
        ("1.3.6.1.4.1.11.2.3.9.4.2.1.4.1.2.5.0", "HP toplam basılan sayfa", "HP"),
        // Kullanıcı tarafından verildi (2026-09-03): standart prtMarkerLifeCount bu Olivetti (Kyocera
        // motoru) cihazda sadece siyah-beyaz sayfa sayısını veriyor; toplam için bu Kyocera enterprise
        // OID'i (renkli sayfa sayısı) ile toplanması gerekebilir. Doğrulama kullanıcı tarafından yapılacak.
        ("1.3.6.1.4.1.1347.42.3.1.2.1.1.1.3", "Olivetti (Kyocera motoru) renkli sayfa sayısı", "Olivetti"),
    };
    // Not (test edildi, 2026-09-03): gerçek bir Samsung SL-M4075FX'te standart Printer-MIB
    // OID'i (prtMarkerLifeCount) zaten doğru sonucu veriyor; Samsung'un tahmini/doğrulanmamış
    // private OID'i ("1.3.6.1.4.1.236.11.5.11.51.20.1.1.4.1.1") boş döndü — kaldırıldı.
    // Olivetti'de de bilinen/doğrulanmış bir private OID yok (çoğu Olivetti MFP motoru Kyocera
    // tabanlıdır ve standart Printer-MIB'i zaten tam destekler, test edilen cihazda da öyle oldu).
    // Yani şu ana kadar test edilen tüm markalarda standart OID yeterli; marka-özel liste sadece
    // ileride "standart çalışmıyor" durumu çıkarsa devreye girecek bir genişletme noktası.

    /// <summary>
    /// sysDescr/sysName içinde anahtar kelime arayarak markayı tahmin eder. Bulunamazsa null döner
    /// (bu durumda sadece markadan bağımsız/evrensel OID'ler denenir, hiçbir marka gürültüsü gösterilmez).
    private static readonly (string Keyword, string Brand)[] BrandKeywords =
    {
        ("hewlett", "HP"),
        ("hp ", "HP"),
        ("samsung", "Samsung"),
        ("olivetti", "Olivetti"),
        ("kyocera", "Kyocera"),
        ("canon", "Canon"),
        ("epson", "Epson"),
        ("brother", "Brother"),
        ("xerox", "Xerox"),
        ("ricoh", "Ricoh"),
        ("lexmark", "Lexmark"),
    };

    private static string? DetectBrand(string? sysDescr, string? sysName)
    {
        var haystack = $"{sysDescr} {sysName}".ToLowerInvariant();
        foreach (var (keyword, brand) in BrandKeywords)
        {
            if (haystack.Contains(keyword))
                return brand;
        }
        return null;
    }

    private const string MarkerLifeCountSubtree = "1.3.6.1.2.1.43.10.2.1.4";
    private const string SysDescrOid = "1.3.6.1.2.1.1.1.0";
    private const string SysNameOid = "1.3.6.1.2.1.1.5.0";

    public async Task<SnmpDiagnosticResult> DiagnoseAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var result = new SnmpDiagnosticResult { IpAddress = ipAddress };

        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            result.Messages.Add("Geçersiz IP adresi.");
            return result;
        }

        var endpoint = new IPEndPoint(ip, _options.Port);
        var community = new OctetString(_options.Community);
        var ct = cancellationToken;

        // 1) Kimlik + hangi sürüm cevap veriyor?
        foreach (var version in new[] { VersionCode.V2, VersionCode.V1 })
        {
            var descr = await GetIdentityAsync(version, endpoint, community, SysDescrOid, ct);
            if (descr is null)
                continue;

            result.Reachable = true;
            result.RespondingVersion = version == VersionCode.V2 ? "V2c" : "V1";
            result.SysDescr = descr.Value;
            result.SysName = (await GetIdentityAsync(version, endpoint, community, SysNameOid, ct))?.Value;
            result.Messages.Add($"SNMP {result.RespondingVersion} ile yanıt alındı.");
            break;
        }

        if (!result.Reachable)
        {
            result.Messages.Add($"SNMP yanıtı yok ({_options.TimeoutSeconds}sn zaman aşımı). Olası nedenler: "
                                + "yazıcı bu bilgisayardan erişilemiyor (farklı ağ/VLAN), yazıcıda SNMP kapalı, "
                                + $"community string \"{_options.Community}\" değil, ya da UDP 161 engelli.");
            return result;
        }

        var respondingVersion = result.RespondingVersion == "V1" ? VersionCode.V1 : VersionCode.V2;

        // 1.5) sysDescr/sysName'den marka tahmini.
        result.DetectedBrand = DetectBrand(result.SysDescr, result.SysName);
        if (result.DetectedBrand is not null)
            result.Messages.Add($"Tespit edilen marka: {result.DetectedBrand} (sysDescr/sysName üzerinden).");

        if (result.DetectedBrand == "Olivetti")
            result.Messages.Add("Olivetti için bilinen/doğrulanmış ayrı bir üretici-özel sayaç OID'i yok "
                                + "(çoğu Olivetti MFP motoru Kyocera tabanlıdır ve standart Printer-MIB'i tam destekler) — "
                                + "sadece standart OID ve aşağıdaki tam alt ağaç taraması kullanılıyor.");

        // 2) Bilinen tekil OID'leri dene — SADECE markadan bağımsız (Brand=null) olanlar +
        //    tespit edilen markaya ait olanlar. Başka markanın OID'leri hiç denenmez/gösterilmez.
        var relevantOids = KnownPageCountOids.Where(k =>
            k.Brand is null || (result.DetectedBrand is not null
                && string.Equals(k.Brand, result.DetectedBrand, StringComparison.OrdinalIgnoreCase)));

        foreach (var (oid, label, brand) in relevantOids)
        {
            var v = await GetSingleAsync(respondingVersion, endpoint, community, oid, ct);

            // Markadan bağımsız (evrensel) OID'i her zaman göster — "denedik, sonuç bu" bilgisi.
            // Marka-özel OID'i ise SADECE bir değer döndüyse göster; boş dönen marka OID'i satırı
            // tabloda gereksiz gürültü ("çıktısı yok" satırı) yaratıyordu.
            if (brand is not null && v?.Value is null)
                continue;

            result.Probes.Add(new SnmpOidValue
            {
                Oid = oid,
                Label = label,
                Brand = brand,
                Type = v?.Type,
                Value = v?.Value ?? "—",
                IsError = v is null || !long.TryParse(v.Value, out _),
            });
        }

        // 3) prtMarkerLifeCount alt ağacını walk et (tüm instance'lar).
        try
        {
            var walked = new List<Variable>();
            using var walkCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            walkCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds * 3)));

            if (respondingVersion == VersionCode.V2)
            {
                await Messenger.BulkWalkAsync(VersionCode.V2, endpoint, community, OctetString.Empty,
                        new ObjectIdentifier(MarkerLifeCountSubtree), walked, 10,
                        WalkMode.WithinSubtree, privacy: null!, report: null!)
                    .WaitAsync(walkCts.Token);
            }
            else
            {
                await Messenger.WalkAsync(VersionCode.V1, endpoint, community,
                        new ObjectIdentifier(MarkerLifeCountSubtree), walked, WalkMode.WithinSubtree)
                    .WaitAsync(walkCts.Token);
            }

            foreach (var variable in walked)
            {
                var raw = variable.Data.ToString();
                var isNum = long.TryParse(raw, out var num);
                result.MarkerCounters.Add(new SnmpOidValue
                {
                    Oid = variable.Id.ToString(),
                    Type = variable.Data.TypeCode.ToString(),
                    Value = raw,
                    IsError = !isNum,
                });

                if (isNum && num > (result.SuggestedValue ?? -1))
                {
                    result.SuggestedValue = num;
                    result.SuggestedOid = variable.Id.ToString();
                }
            }

            if (result.MarkerCounters.Count == 0)
                result.Messages.Add("prtMarkerLifeCount alt ağacı boş döndü.");
        }
        catch (Exception ex)
        {
            result.Messages.Add($"Walk başarısız: {ex.Message}");
        }

        // 4) Walk boşsa, çalışan tekil probe'lardan öneri üret.
        if (result.SuggestedOid is null)
        {
            var best = result.Probes
                .Where(p => !p.IsError && long.TryParse(p.Value, out _))
                .OrderByDescending(p => long.Parse(p.Value!))
                .FirstOrDefault();
            if (best is not null)
            {
                result.SuggestedOid = best.Oid;
                result.SuggestedValue = long.Parse(best.Value!);
            }
        }

        if (result.SuggestedOid is not null)
            result.Messages.Add($"Önerilen PageCountOid: {result.SuggestedOid} (değer: {result.SuggestedValue}). "
                                + "appsettings.json → PrinterMonitoring:Snmp:PageCountOid içine yazıp yeniden başlatın.");

        return result;
    }

    public async Task<SnmpOidValue?> ProbeOidAsync(string ipAddress, string oid, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip) || string.IsNullOrWhiteSpace(oid))
            return null;

        var endpoint = new IPEndPoint(ip, _options.Port);
        var community = new OctetString(_options.Community);

        foreach (var version in new[] { VersionCode.V2, VersionCode.V1 })
        {
            var result = await GetSingleAsync(version, endpoint, community, oid.Trim(), cancellationToken);
            if (result is not null)
                return result;
        }

        return null;
    }

    public async Task<SnmpQuickProbe?> QuickProbeAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return null;

        var endpoint = new IPEndPoint(ip, _options.Port);
        var community = new OctetString(_options.Community);
        var primary = ParseVersion(_options.Version);
        var order = primary == VersionCode.V1
            ? new[] { VersionCode.V1, VersionCode.V2 }
            : new[] { VersionCode.V2, VersionCode.V1 };

        foreach (var version in order)
        {
            SnmpOidValue? descr;
            try
            {
                descr = await GetSingleAsync(version, endpoint, community, SysDescrOid, timeoutMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (descr?.Value is null)
                continue; // bu sürümle yanıt yok — diğerini dene

            var sysName = (await GetSingleAsync(version, endpoint, community, SysNameOid, timeoutMs, cancellationToken))?.Value;
            var counter = await GetSingleAsync(version, endpoint, community, _options.PageCountOid, timeoutMs, cancellationToken);

            return new SnmpQuickProbe
            {
                IpAddress = ipAddress,
                RespondingVersion = version == VersionCode.V1 ? "V1" : "V2c",
                SysDescr = CleanModel(descr.Value),
                SysName = string.IsNullOrWhiteSpace(sysName) ? null : sysName.Trim(),
                HasPageCounter = counter?.Value is not null && long.TryParse(counter.Value, out _),
                DetectedBrand = DetectBrand(descr.Value, sysName),
            };
        }

        return null;
    }

    private Task<SnmpOidValue?> GetSingleAsync(
        VersionCode version, IPEndPoint endpoint, OctetString community, string oid, CancellationToken outerToken)
        => GetSingleAsync(version, endpoint, community, oid, _options.TimeoutSeconds * 1000, outerToken, _retries);

    /// <summary>sysDescr/sysName gibi kimlik OID'leri için: boş dönerse de tekrar dener.</summary>
    private Task<SnmpOidValue?> GetIdentityAsync(
        VersionCode version, IPEndPoint endpoint, OctetString community, string oid, CancellationToken outerToken)
        => GetSingleAsync(version, endpoint, community, oid, _options.TimeoutSeconds * 1000, outerToken, _retries, retryOnEmpty: true);

    /// <summary>
    /// Tek OID GET. Geçici hatalarda (UDP paket kaybı / zaman aşımı) <paramref name="retries"/>
    /// kez daha dener. Cihaz "OID yok" derse (NoSuchObject/NoSuchInstance/EndOfMibView) —
    /// bu kesin bir yanıttır — tekrar denemez, <c>null</c> döner.
    /// </summary>
    private async Task<SnmpOidValue?> GetSingleAsync(
        VersionCode version, IPEndPoint endpoint, OctetString community, string oid,
        int timeoutMs, CancellationToken outerToken, int retries = 0, bool retryOnEmpty = false)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var vars = new List<Variable> { new(new ObjectIdentifier(oid)) };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

                var res = await Messenger.GetAsync(version, endpoint, community, vars).WaitAsync(cts.Token);
                var data = res.FirstOrDefault()?.Data;
                if (data is null || data.TypeCode is SnmpType.NoSuchObject or SnmpType.NoSuchInstance or SnmpType.EndOfMibView)
                    return null; // kesin cevap: OID yok — tekrar deneme

                var text = data.ToString();

                // Bazı yazıcılar (ör. bazı Olivetti'ler) sysDescr'ı ara sıra BOŞ döndürür —
                // geçerli bir SNMP yanıtı ama işe yaramaz. retryOnEmpty ise tekrar dene.
                if (retryOnEmpty && string.IsNullOrWhiteSpace(text) && attempt < retries)
                {
                    await Task.Delay(150 * (attempt + 1), outerToken);
                    continue;
                }

                return new SnmpOidValue
                {
                    Oid = oid,
                    Type = data.TypeCode.ToString(),
                    Value = text,
                };
            }
            catch (OperationCanceledException) when (outerToken.IsCancellationRequested)
            {
                throw; // uygulama/istek iptal edildi — üst katmana bırak
            }
            catch (Exception ex) when (attempt < retries)
            {
                // Zaman aşımı / düşen UDP pakedi / geçici hata — kısa bekle, tekrar dene.
                _logger.LogDebug("{Ip} {Oid} ({Ver}) deneme {N} başarısız ({Err}) — yeniden.",
                    endpoint.Address, oid, version, attempt + 1, ex.GetType().Name);
                try { await Task.Delay(150 * (attempt + 1), outerToken); }
                catch (OperationCanceledException) { return null; }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("{Ip} {Oid} ({Ver}): yanıt yok ({Err}).",
                    endpoint.Address, oid, version, ex.GetType().Name);
                return null;
            }
        }
    }

    private static VersionCode ParseVersion(string version) => version.Trim().ToUpperInvariant() switch
    {
        "V1" => VersionCode.V1,
        "V2" or "V2C" => VersionCode.V2,
        "V3" => VersionCode.V3,
        _ => VersionCode.V2,
    };
}
