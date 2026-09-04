using System.ComponentModel.DataAnnotations;

namespace YaziciTakip.Models;

/// <summary>
/// Yazıcı markasının servis/bayi firmasına gönderilen sayfa-başı hakediş belgesi.
/// Oluşturulduğu anda satırlardaki sayaç değerleri dondurulur (kayıt = fatura dayanağı).
/// </summary>
public class Hakedis
{
    public int Id { get; set; }

    /// <summary>Yıl bazlı sıra numarası, ör. "2026-0001".</summary>
    [Required]
    [MaxLength(20)]
    public string Number { get; set; } = string.Empty;

    /// <summary>Belgenin düzenlendiği an (UTC).</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>Dönem başlangıcı (bir önceki hakediş tarihi ya da ilk okuma). Belge başlığında gösterilir.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Dönem bitişi (genelde belgenin düzenlendiği gün).</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Serbest açıklama: dönem adı, sözleşme no, firma vb.</summary>
    [MaxLength(500)]
    public string? Note { get; set; }

    public List<HakedisLine> Lines { get; set; } = new();

    public long TotalPages => Lines.Sum(l => l.Pages ?? 0);
}

/// <summary>Hakediş belgesinde tek bir yazıcının satırı. Yazıcı adı/modeli o anki haliyle kopyalanır.</summary>
public class HakedisLine
{
    public int Id { get; set; }

    public int HakedisId { get; set; }

    public Hakedis? Hakedis { get; set; }

    /// <summary>Kaynak yazıcı (silinmiş olabilir; rapor için ad/model aşağıda kopyalanır).</summary>
    public int? PrinterId { get; set; }

    [Required]
    [MaxLength(100)]
    public string PrinterName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Model { get; set; }

    public long PreviousCounter { get; set; }

    public long CurrentCounter { get; set; }

    /// <summary>Basılan sayfa = CurrentCounter - PreviousCounter. Sayaç geriye gittiyse null.</summary>
    public long? Pages { get; set; }
}
