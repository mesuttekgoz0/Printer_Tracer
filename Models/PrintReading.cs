namespace YaziciTakip.Models;

/// <summary>
/// Bir yazıcıdan belirli bir anda SNMP ile okunan toplam sayfa sayacı değeri.
/// İki ardışık okumanın <see cref="PageCount"/> farkı, o aralıkta basılan sayfa sayısını verir.
/// </summary>
public class PrintReading
{
    public int Id { get; set; }

    public int PrinterId { get; set; }

    public Printer? Printer { get; set; }

    /// <summary>Yazıcının ömür boyu toplam bastığı sayfa sayısı (Printer-MIB sayacı).</summary>
    public long PageCount { get; set; }

    /// <summary>Okumanın yapıldığı an (UTC).</summary>
    public DateTime TimestampUtc { get; set; }
}
