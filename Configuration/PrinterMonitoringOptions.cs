namespace YaziciTakip.Configuration;

/// <summary>
/// appsettings.json -> "PrinterMonitoring" bölümünün karşılığı.
/// Yazıcı listesi ve okuma ayarları buradan yönetilir; yeni yazıcı eklemek için
/// sadece "Printers" dizisine bir kayıt eklemek yeterli.
/// </summary>
public class PrinterMonitoringOptions
{
    public const string SectionName = "PrinterMonitoring";

    /// <summary>
    /// Arka planda periyodik SNMP okuması yapılsın mı? Kapalıysa (varsayılan) okumalar
    /// yalnızca arayüzdeki "Şimdi Oku" düğmesiyle, kullanıcı istediğinde yapılır.
    /// </summary>
    public bool PollingEnabled { get; set; } = false;

    /// <summary>Periyodik okuma açıksa yazıcıların kaç dakikada bir okunacağı.</summary>
    public int PollingIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Açıksa ve PrintReadings tablosu boşsa, açılışta demo amaçlı örnek okuma verisi üretilir.
    /// Sadece Development ortamında (appsettings.Development.json) açık; gerçek veriyi asla ezmez.
    /// </summary>
    public bool SeedSampleReadings { get; set; } = false;

    /// <summary>Örnek verinin kaç günlük geçmişi kapsayacağı.</summary>
    public int SampleDataDays { get; set; } = 3;

    public SnmpOptions Snmp { get; set; } = new();

    public List<PrinterConfig> Printers { get; set; } = new();
}

public class SnmpOptions
{
    /// <summary>SNMP community string. Genelde "public".</summary>
    public string Community { get; set; } = "public";

    /// <summary>Önce denenecek SNMP sürümü: "V2c" veya "V1".</summary>
    public string Version { get; set; } = "V2c";

    /// <summary>V2c başarısız olursa V1'e düşülsün mü?</summary>
    public bool FallbackToV1 { get; set; } = true;

    public int Port { get; set; } = 161;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Toplam sayfa sayacı OID'i (Printer-MIB prtMarkerLifeCount).
    /// Bazı yazıcılar farklı instance kullanabilir; gerekirse config'den değiştirilir.
    /// </summary>
    public string PageCountOid { get; set; } = "1.3.6.1.2.1.43.10.2.1.4.1.1";
}

public class PrinterConfig
{
    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;
}
