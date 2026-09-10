namespace YaziciTakip.Models;

/// <summary>
/// Bir yazıcıya SNMP tanılama sorgusunun sonucu: kimlik bilgisi, denenen OID'ler ve
/// sayaç adayı OID'ler. "Doğru" sayfa sayacı OID'ini bulmak için kullanılır.
/// </summary>
public class SnmpDiagnosticResult
{
    public string IpAddress { get; set; } = string.Empty;

    public bool Reachable { get; set; }

    public string? SysDescr { get; set; }

    public string? SysName { get; set; }

    /// <summary>
    /// sysDescr/sysName içinden anahtar kelimeyle tespit edilen marka (ör. "HP", "Samsung", "Olivetti").
    /// Tespit edilemezse null — bu durumda sadece markadan bağımsız (evrensel) OID'ler denenir.
    /// </summary>
    public string? DetectedBrand { get; set; }

    /// <summary>SNMP'ye yanıt veren sürüm ("V2c" / "V1" / null).</summary>
    public string? RespondingVersion { get; set; }

    /// <summary>Genel hata / bilgi mesajları.</summary>
    public List<string> Messages { get; } = new();

    /// <summary>Denenen tekil OID GET sonuçları (config'deki OID + bilinen HP OID'leri).</summary>
    public List<SnmpOidValue> Probes { get; } = new();

    /// <summary>Printer-MIB sayaç alt ağacının (1.3.6.1.2.1.43.10.2.1.4) walk sonucu.</summary>
    public List<SnmpOidValue> MarkerCounters { get; } = new();

    /// <summary>Sayfa sayacı olmaya en uygun görünen OID (en büyük pozitif tamsayı).</summary>
    public string? SuggestedOid { get; set; }

    public long? SuggestedValue { get; set; }

    /// <summary>Kullanıcının elle sorguladığı OID (varsa) — bilinen listeden/marka tespitinden bağımsız.</summary>
    public string? ManualOid { get; set; }

    /// <summary>Elle sorgulanan OID'in sonucu. Null ise yanıt yok/OID mevcut değil.</summary>
    public SnmpOidValue? ManualOidResult { get; set; }
}

public class SnmpOidValue
{
    public string Oid { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string? Type { get; set; }

    public string? Value { get; set; }

    public bool IsError { get; set; }

    /// <summary>Bu OID'in ait olduğu marka (ör. "HP", "Samsung"). null ise markadan bağımsız (evrensel).</summary>
    public string? Brand { get; set; }
}

/// <summary>Ağ taramasında tek bir cihazın hızlı SNMP yoklaması sonucu.</summary>
public class SnmpQuickProbe
{
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>SNMP'ye yanıt veren sürüm ("V2c" / "V1").</summary>
    public string RespondingVersion { get; set; } = string.Empty;

    public string? SysName { get; set; }

    public string? SysDescr { get; set; }

    /// <summary>Standart Printer-MIB sayfa sayacı OID'ine (prtMarkerLifeCount) yanıt verdi mi?</summary>
    public bool HasPageCounter { get; set; }

    /// <summary>sysDescr/sysName'den tespit edilen marka (varsa).</summary>
    public string? DetectedBrand { get; set; }
}
