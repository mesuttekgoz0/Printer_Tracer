using System.ComponentModel.DataAnnotations;

namespace YaziciTakip.Models;

/// <summary>
/// Yazıcı servis/bayi firması (tedarikçi). Her yazıcının en fazla bir tedarikçisi vardır;
/// bir tedarikçinin bir veya daha fazla yazıcısı olabilir. Hakediş bir tedarikçi seçilerek
/// üretilir ve o tedarikçinin fiyat listesinden tür bazlı sayfa-başı fiyat uygulanır.
/// </summary>
public class Tedarikci
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Not { get; set; }

    public List<Printer> Printers { get; set; } = new();

    public List<FiyatListesi> FiyatListeleri { get; set; } = new();
}
