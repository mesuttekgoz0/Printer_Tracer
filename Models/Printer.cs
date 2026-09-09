using System.ComponentModel.DataAnnotations;

namespace YaziciTakip.Models;

/// <summary>
/// Ağa doğrudan bağlı bir yazıcıyı temsil eder. Kayıtlar <see cref="appsettings.json"/>
/// içindeki listeden senkronize edilir (IP adresi benzersiz anahtar gibi kullanılır).
/// </summary>
public class Printer
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>SNMP sorgusunun yapılacağı IPv4 adresi. Yazıcı listesinde benzersizdir.</summary>
    [Required]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Yazıcının marka/model bilgisi (SNMP <c>sysDescr</c> — OID 1.3.6.1.2.1.1.1.0).
    /// İlk başarılı "Sayaç Oku" sırasında otomatik doldurulur/güncellenir. Okunamazsa null.
    /// </summary>
    [MaxLength(250)]
    public string? Model { get; set; }

    /// <summary>
    /// Yazıcının çıktı türü (<see cref="Tur"/> tablosuna FK). Elle belirlenir; SNMP'den okunmaz.
    /// null = belirtilmemiş.
    /// </summary>
    public int? TurId { get; set; }

    public Tur? Tur { get; set; }

    /// <summary>Bu yazıcının servis/bayi firması (tedarikçi). Hakediş bu bağ üzerinden üretilir.</summary>
    public int? TedarikciId { get; set; }

    public Tedarikci? Tedarikci { get; set; }

    public ICollection<PrintReading> Readings { get; set; } = new List<PrintReading>();

    /// <summary>Arayüzde gösterilecek tür metni ("Belirtilmemiş" fallback).</summary>
    public string TurAdi => Tur?.Ad ?? Models.Tur.Belirtilmemis;
}
