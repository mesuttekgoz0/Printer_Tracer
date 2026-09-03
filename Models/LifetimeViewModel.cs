namespace YaziciTakip.Models;

/// <summary>
/// Yazıcıların ömür boyu (dahili) sayaç değerlerini gösteren rapor.
/// Printer-MIB sayacı (prtMarkerLifeCount) yazıcı üretildiğinden/sıfırlandığından beri
/// bastığı TÜM sayfaları kapsar — bizim izlememize başlamadan önceki dönem dahil.
/// Bu yüzden ilk okunan değer, "izleme öncesi" toplamı zaten içerir.
/// </summary>
public class LifetimeViewModel
{
    public List<PrinterLifetimeRow> Printers { get; set; } = new();

    public bool AnyData => Printers.Any(p => p.HasReadings);
}

public class PrinterLifetimeRow
{
    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public bool HasReadings { get; set; }

    /// <summary>Sistemde bu yazıcı için okunan ilk kayıt zamanı (izlemenin başladığı an).</summary>
    public DateTime? FirstReadingUtc { get; set; }

    /// <summary>
    /// İlk okumadaki ham sayaç değeri. Yazıcının dahili ömür boyu sayacı olduğu için,
    /// bu değer izlemeye başlamadan ÖNCE o yazıcının bastığı toplam sayfa sayısını da içerir.
    /// </summary>
    public long? FirstCounter { get; set; }

    public DateTime? LatestReadingUtc { get; set; }

    /// <summary>Şu anki (güncel) ham sayaç değeri — yazıcının bugüne kadar bastığı toplam sayfa.</summary>
    public long? LatestCounter { get; set; }

    /// <summary>İzleme başladığından bu yana (ilk okuma → son okuma) basılan sayfa sayısı.</summary>
    public long? PagesSinceMonitoringStarted =>
        LatestCounter.HasValue && FirstCounter.HasValue ? LatestCounter - FirstCounter : null;
}
