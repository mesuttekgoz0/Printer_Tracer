using System.ComponentModel.DataAnnotations.Schema;

namespace YaziciTakip.Models;

/// <summary>
/// Fiyat "master" kaydı: bir tedarikçinin bir yazıcı türü için fiyatlandırma izi.
/// Her (tedarikçi, tür) çifti için tek satır. Zaman içindeki fiyatlar
/// <see cref="FiyatDetay"/> satırlarında tarih aralıklarıyla tutulur.
/// </summary>
public class Fiyat
{
    public int Id { get; set; }

    public int TedarikciId { get; set; }

    public Tedarikci? Tedarikci { get; set; }

    public int TurId { get; set; }

    public Tur? Tur { get; set; }

    public List<FiyatDetay> Detaylar { get; set; } = new();
}

/// <summary>
/// Bir <see cref="Fiyat"/> master'ının belirli bir tarih aralığında geçerli
/// sayfa-başı fiyatı. Hakediş, dönem bitiş tarihini kapsayan detayı kullanır.
/// </summary>
public class FiyatDetay
{
    public int Id { get; set; }

    public int FiyatId { get; set; }

    public Fiyat? Fiyat { get; set; }

    public DateOnly BaslangicTarihi { get; set; }

    public DateOnly BitisTarihi { get; set; }

    /// <summary>Yazıcı türü (master'daki <see cref="Fiyat.TurId"/> ile aynı — istenerek kopyalandı).</summary>
    public int TurId { get; set; }

    public Tur? Tur { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SayfaBasiFiyat { get; set; }

    /// <summary>Verilen tarih bu detayın aralığında mı?</summary>
    public bool Kapsar(DateOnly tarih) => BaslangicTarihi <= tarih && tarih <= BitisTarihi;
}
