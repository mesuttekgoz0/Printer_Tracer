namespace YaziciTakip.Configuration;

/// <summary>
/// appsettings.json -> "Snmp" bölümünün karşılığı. Yazıcı sayfa sayacı okuması
/// (SNMP GET/WALK) için bağlantı ayarları. <see cref="Services.SnmpService"/> kullanır.
/// </summary>
public class SnmpOptions
{
    public const string SectionName = "Snmp";

    /// <summary>SNMP community string. Genelde "public".</summary>
    public string Community { get; set; } = "public";

    /// <summary>Önce denenecek SNMP sürümü: "V2c" veya "V1".</summary>
    public string Version { get; set; } = "V2c";

    /// <summary>V2c başarısız olursa V1'e düşülsün mü?</summary>
    public bool FallbackToV1 { get; set; } = true;

    public int Port { get; set; } = 161;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Geçici hata (UDP paket kaybı / zaman aşımı) durumunda ek deneme sayısı.
    /// SNMP UDP üzerinden çalışır ve yeniden gönderim yapmaz; tek pakedin düşmesi
    /// "yanıt yok" gibi görünür. 0 = tek deneme.
    /// </summary>
    public int Retries { get; set; } = 2;

    /// <summary>
    /// Toplam sayfa sayacı OID'i (Printer-MIB prtMarkerLifeCount).
    /// Bazı yazıcılar farklı instance kullanabilir; gerekirse config'den değiştirilir.
    /// </summary>
    public string PageCountOid { get; set; } = "1.3.6.1.2.1.43.10.2.1.4.1.1";
}
