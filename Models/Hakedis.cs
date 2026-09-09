using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>Hakedişin ait olduğu tedarikçi (silinmiş olabilir; ad aşağıda kopyalanır).</summary>
    public int? TedarikciId { get; set; }

    /// <summary>Tedarikçi adı, belge oluşturulduğu anki haliyle donduruldu. Belge başlığında gösterilir.</summary>
    [MaxLength(150)]
    public string? TedarikciAd { get; set; }

    public List<HakedisLine> Lines { get; set; } = new();

    public long TotalPages => Lines.Sum(l => l.Pages ?? 0);

    /// <summary>Tüm satırların tutar toplamı.</summary>
    public decimal TotalAmount => Lines.Sum(l => l.Amount);
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

    /// <summary>Yazıcının tür id'si, belge oluşturulduğu anki haliyle donduruldu (FK değil — snapshot).</summary>
    public int? TurId { get; set; }

    /// <summary>Tür adı, belge oluşturulduğu anki haliyle donduruldu. Belgede bu gösterilir.</summary>
    [MaxLength(50)]
    public string? TurAd { get; set; }

    public long PreviousCounter { get; set; }

    public long CurrentCounter { get; set; }

    /// <summary>Basılan sayfa = CurrentCounter - PreviousCounter. Sayaç geriye gittiyse null.</summary>
    public long? Pages { get; set; }

    /// <summary>Fiyat listesinden çekilen sayfa-başı fiyat, donduruldu.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Tutar = Pages × UnitPrice, donduruldu.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
