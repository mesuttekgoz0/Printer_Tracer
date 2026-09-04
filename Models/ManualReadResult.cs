namespace YaziciTakip.Models;

/// <summary>
/// Bir "tüm yazıcıları şimdi oku" işleminin sonucu: yazıcı başına önceki sayaç,
/// yeni sayaç ve aradaki fark. Hem arayüzdeki düğme hem de (açıksa) periyodik worker kullanır.
/// </summary>
public class ManualReadResult
{
    public List<ManualReadRow> Rows { get; } = new();

    public int Total => Rows.Count;

    /// <summary>Sayacı başarıyla okunup kaydedilen yazıcı sayısı.</summary>
    public int SavedCount => Rows.Count(r => r.Reachable);

    /// <summary>Ulaşılamayan yazıcıların adları.</summary>
    public IReadOnlyList<string> FailedNames =>
        Rows.Where(r => !r.Reachable).Select(r => r.PrinterName).ToList();

    /// <summary>Bu okuma ile önceki okuma arasında basıldığı hesaplanan toplam sayfa.</summary>
    public long TotalDelta => Rows.Sum(r => r.Delta ?? 0);
}

public class ManualReadRow
{
    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Yazıcıya SNMP ile ulaşılıp sayaç okunabildi mi?</summary>
    public bool Reachable { get; set; }

    /// <summary>Bu okumadan önceki son sayaç değeri (hiç okuma yoksa null).</summary>
    public long? PreviousCounter { get; set; }

    public DateTime? PreviousReadingUtc { get; set; }

    /// <summary>Bu okumada alınan güncel sayaç (ulaşılamadıysa null).</summary>
    public long? NewCounter { get; set; }

    /// <summary>
    /// Önceki okumadan bu yana basılan sayfa. Önceki okuma yoksa veya sayaç
    /// geriye gitmişse (sıfırlama vb.) null.
    /// </summary>
    public long? Delta =>
        Reachable && NewCounter is { } n && PreviousCounter is { } p && n >= p
            ? n - p
            : null;

    /// <summary>Bu yazıcı için ilk okuma mı (kıyaslanacak önceki değer yok).</summary>
    public bool IsFirstReading => Reachable && PreviousCounter is null;
}
