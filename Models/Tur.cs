using System.ComponentModel.DataAnnotations;

namespace YaziciTakip.Models;

/// <summary>
/// Yazıcı türü (siyah-beyaz / renkli). Sabit bir referans (lookup) tablosudur; açılışta
/// <c>1 = Siyah-Beyaz</c>, <c>2 = Renkli</c> olarak seed edilir. Yazıcı ve fiyat listesi
/// satırları buna foreign key ile bağlanır.
/// </summary>
public class Tur
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Ad { get; set; } = string.Empty;

    public List<Printer> Printers { get; set; } = new();

    /// <summary>Türü belirtilmemiş (null) yazıcılar için arayüzde gösterilecek metin.</summary>
    public const string Belirtilmemis = "Belirtilmemiş";
}
