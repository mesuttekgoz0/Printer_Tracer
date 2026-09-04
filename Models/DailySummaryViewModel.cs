namespace YaziciTakip.Models;

/// <summary>
/// "Rapor" sayfası (route: Report/Summary): seçilen tarih aralığındaki tek tek SNMP
/// okumaları ve her okumanın bir öncekine göre farkı + dönem/yazıcı bazlı toplamlar.
/// </summary>
public class DailySummaryViewModel
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public List<PrinterRef> Printers { get; set; } = new();

    /// <summary>Aralıktaki tüm pozitif farkların toplamı (dönemde basılan sayfa).</summary>
    public long GrandTotal { get; set; }

    /// <summary>Yazıcı bazında dönem toplamı (PrinterRef.Id -> sayfa).</summary>
    public Dictionary<int, long> PrinterTotals { get; set; } = new();

    /// <summary>Aralıktaki tek tek okumalar (en yeni önce), her biri bir önceki okumaya göre farkıyla.</summary>
    public List<ReadingLogRow> Readings { get; set; } = new();

    /// <summary>Aralıkta toplam kaç okuma var (Readings kısaltılmış olabilir).</summary>
    public int ReadingsTotal { get; set; }

    /// <summary>true ise tüm okumalar listeleniyor; false ise yalnızca en son birkaçı.</summary>
    public bool ShowAllReadings { get; set; }

    public int DayCount => To.DayNumber - From.DayNumber + 1;
}

/// <summary>"Okumalar" listesinde tek bir SNMP okuması.</summary>
public class ReadingLogRow
{
    public DateTime TimestampUtc { get; set; }

    public int PrinterId { get; set; }

    public string PrinterName { get; set; } = string.Empty;

    /// <summary>O okumadaki ham sayaç değeri (yazıcının ömür boyu toplamı).</summary>
    public long PageCount { get; set; }

    /// <summary>Bir önceki okumaya göre fark. İlk okumada veya sayaç geriye gittiyse null.</summary>
    public long? Delta { get; set; }
}

public class PrinterRef
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
