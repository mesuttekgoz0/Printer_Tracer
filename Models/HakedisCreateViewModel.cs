using System.Globalization;

namespace YaziciTakip.Models;

/// <summary>
/// "Hakediş Oluştur" inceleme ekranı: seçilen tedarikçinin yazıcıları için önceden
/// hesaplanmış önceki/şimdiki sayaç ve fiyat listesinden çekilen tutarlar
/// (kullanıcı kaydetmeden önce sayaçları düzeltebilir).
/// </summary>
public class HakedisCreateViewModel
{
    public int TedarikciId { get; set; }

    public string TedarikciAd { get; set; } = string.Empty;

    /// <summary>Uygulanan fiyat listesinin adı + tarihi (bilgi amaçlı). Liste yoksa null.</summary>
    public string? FiyatListesiBilgi { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public string? Note { get; set; }

    public List<HakedisCreateRow> Rows { get; set; } = new();

    /// <summary>Okuması olmadığı için hakedişe eklenemeyen yazıcı adları.</summary>
    public List<string> Skipped { get; set; } = new();

    /// <summary>Bilgilendirme/uyarı mesajları (tür seçili değil, fiyat listesi yok vb.).</summary>
    public List<string> Warnings { get; set; } = new();

    public decimal TotalAmount => Rows.Sum(r => r.Amount);
}

public class HakedisCreateRow
{
    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    public string? Model { get; set; }

    public int? TurId { get; set; }

    public string TurAd { get; set; } = Tur.Belirtilmemis;

    public long PreviousCounter { get; set; }

    public long CurrentCounter { get; set; }

    public DateTime? PreviousReadingUtc { get; set; }

    public DateTime CurrentReadingUtc { get; set; }

    /// <summary>Fiyat listesinden çekilen sayfa-başı fiyat (tür için fiyat yoksa 0).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Bu yazıcının daha önce hiç hakedişi yok mu (önceki sayaç ilk okumadan tahmin edildi).</summary>
    public bool FirstHakedis { get; set; }

    public long? Pages =>
        CurrentCounter >= PreviousCounter ? CurrentCounter - PreviousCounter : null;

    public decimal Amount => (Pages ?? 0) * UnitPrice;

    /// <summary>Gizli form alanı için fiyatı noktalı (invariant) yaz — bind sorununu önler.</summary>
    public string UnitPriceInvariant => UnitPrice.ToString(CultureInfo.InvariantCulture);
}
