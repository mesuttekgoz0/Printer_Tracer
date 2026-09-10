using YaziciTakip.Models;

namespace YaziciTakip.Services;

public interface ISnmpService
{
    /// <summary>
    /// Verilen IP'deki yazıcıdan toplam sayfa sayacını SNMP GET ile okur.
    /// Config'e göre önce V2c, gerekirse V1 denenir. Ulaşılamazsa <c>null</c> döner.
    /// </summary>
    Task<long?> GetPageCountAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yazıcının marka/model bilgisini (<c>sysDescr</c>, OID 1.3.6.1.2.1.1.1.0) tek bir SNMP GET ile okur.
    /// Uzun/çok satırlı dönerse kısaltılır. Ulaşılamazsa/boşsa <c>null</c> döner.
    /// </summary>
    Task<string?> GetModelAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yazıcıya tanılama amaçlı SNMP sorgusu yapar: kimlik bilgisi, config'deki OID,
    /// bilinen HP OID'leri ve Printer-MIB sayaç alt ağacının walk sonucu.
    /// Doğru <c>PageCountOid</c> değerini belirlemek için kullanılır.
    /// </summary>
    Task<SnmpDiagnosticResult> DiagnoseAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen tek bir OID'i doğrudan sorgular (marka tespitinden/bilinen listeden bağımsız).
    /// sysDescr boş dönen ya da marka otomatik tespit edilemeyen cihazlarda, bilinen olmayan
    /// bir OID'i elle denemek için kullanılır. Ulaşılamazsa/OID yoksa <c>null</c> döner.
    /// </summary>
    Task<SnmpOidValue?> ProbeOidAsync(string ipAddress, string oid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ağ taraması için hızlı yoklama: kısa timeout ile önce <c>sysDescr</c>, yanıt varsa
    /// <c>sysName</c> + standart sayfa-sayacı OID'i sorgulanır. SNMP'ye hiç yanıt yoksa <c>null</c>.
    /// </summary>
    Task<SnmpQuickProbe?> QuickProbeAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken = default);
}
