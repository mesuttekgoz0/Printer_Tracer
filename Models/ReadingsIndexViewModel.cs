namespace YaziciTakip.Models;

/// <summary>
/// "Sayaç Oku" sayfası. Düğmeye basılmasa bile her yazıcının son iki okuması ve
/// aradaki fark tablo olarak sürekli gösterilir (veritabanından okunur).
/// </summary>
public class ReadingsIndexViewModel
{
    public int PrinterCount { get; set; }

    public DateTime? LastReadingUtc { get; set; }

    public List<PrinterReadingStatus> Printers { get; set; } = new();

    /// <summary>Son okumaya göre tüm yazıcılarda toplam fark (pozitif deltalar).</summary>
    public long TotalDelta => Printers.Sum(p => p.Delta ?? 0);
}

public class PrinterReadingStatus
{
    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Marka/model (SNMP sysDescr). İlk okumaya kadar null.</summary>
    public string? Model { get; set; }

    /// <summary>Yazıcının tür id'si (Tur tablosuna FK). null = belirtilmemiş.</summary>
    public int? TurId { get; set; }

    /// <summary>Tür adı; belirtilmemişse "Belirtilmemiş".</summary>
    public string TurAd { get; set; } = Tur.Belirtilmemis;

    /// <summary>Bu yazıcı için kayıtlı toplam okuma sayısı.</summary>
    public int ReadingCount { get; set; }

    public long? LatestCounter { get; set; }

    public DateTime? LatestReadingUtc { get; set; }

    public long? PreviousCounter { get; set; }

    public DateTime? PreviousReadingUtc { get; set; }

    /// <summary>Son iki okuma arasındaki fark. Önceki okuma yoksa veya sayaç geriye gittiyse null.</summary>
    public long? Delta =>
        LatestCounter is { } l && PreviousCounter is { } p && l >= p ? l - p : null;

    public bool HasReading => LatestReadingUtc is not null;
}
