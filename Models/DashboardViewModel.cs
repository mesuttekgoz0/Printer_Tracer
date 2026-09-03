namespace YaziciTakip.Models;

/// <summary>
/// Ana sayfadaki (panel) özet göstergeleri: yazıcı sayısı, bu ay / bugün basılan
/// tahmini sayfa, son okuma zamanı ve yazıcı bazlı kısa liste.
/// </summary>
public class DashboardViewModel
{
    public int PrinterCount { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public long MonthTotal { get; set; }

    public long TodayTotal { get; set; }

    public DateTime? LastReadingUtc { get; set; }

    public List<DashboardPrinterRow> Printers { get; set; } = new();
}

public class DashboardPrinterRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public long MonthPages { get; set; }

    public long? LatestCounter { get; set; }

    public DateTime? LatestReadingUtc { get; set; }
}
