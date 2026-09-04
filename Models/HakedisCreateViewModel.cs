namespace YaziciTakip.Models;

/// <summary>
/// "Hakediş Oluştur" inceleme ekranı: seçilen yazıcılar için önceden hesaplanmış
/// önceki/şimdiki sayaç değerleri (kullanıcı kaydetmeden önce düzeltebilir).
/// </summary>
public class HakedisCreateViewModel
{
    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public string? Note { get; set; }

    public List<HakedisCreateRow> Rows { get; set; } = new();

    /// <summary>Okuması olmadığı için hakedişe eklenemeyen yazıcı adları.</summary>
    public List<string> Skipped { get; set; } = new();
}

public class HakedisCreateRow
{
    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string? Model { get; set; }

    public long PreviousCounter { get; set; }

    public long CurrentCounter { get; set; }

    public DateTime? PreviousReadingUtc { get; set; }

    public DateTime CurrentReadingUtc { get; set; }

    /// <summary>Bu yazıcının daha önce hiç hakedişi yok mu (önceki sayaç ilk okumadan tahmin edildi).</summary>
    public bool FirstHakedis { get; set; }

    public long? Pages =>
        CurrentCounter >= PreviousCounter ? CurrentCounter - PreviousCounter : null;
}
