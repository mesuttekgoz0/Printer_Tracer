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
}
