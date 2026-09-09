using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YaziciTakip.Models;

/// <summary>
/// Bir tedarikçinin belirli bir tarihten itibaren geçerli sayfa-başı fiyat listesi.
/// Bir tedarikçinin birden çok tarihli listesi olabilir; hakediş, dönem bitişine göre
/// en güncel listeyi kullanır. Her liste, yazıcı türü başına bir <see cref="FiyatSatiri"/> içerir.
/// </summary>
public class FiyatListesi
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string ListeAdi { get; set; } = string.Empty;

    /// <summary>Listenin geçerlilik başlangıç tarihi.</summary>
    public DateOnly Tarih { get; set; }

    public int TedarikciId { get; set; }

    public Tedarikci? Tedarikci { get; set; }

    public List<FiyatSatiri> Satirlar { get; set; } = new();

    /// <summary>Verilen tür id'si için sayfa-başı fiyat (satır yoksa 0).</summary>
    public decimal FiyatBul(int turId) =>
        Satirlar.FirstOrDefault(s => s.TurId == turId)?.SayfaBasiFiyat ?? 0m;
}

/// <summary>Fiyat listesinde bir yazıcı türü için sayfa-başı fiyat. <see cref="TurId"/> → <see cref="Tur"/> FK.</summary>
public class FiyatSatiri
{
    public int Id { get; set; }

    public int FiyatListesiId { get; set; }

    public FiyatListesi? FiyatListesi { get; set; }

    /// <summary>Yazıcı türü (<see cref="Tur"/> tablosuna FK).</summary>
    public int TurId { get; set; }

    public Tur? Tur { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SayfaBasiFiyat { get; set; }
}
