namespace YaziciTakip.Models;

public class DailySummaryViewModel
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public List<PrinterRef> Printers { get; set; } = new();

    public List<DailySummaryRow> Days { get; set; } = new();

    public long GrandTotal { get; set; }

    public double DailyAverage { get; set; }

    public DailySummaryRow? BusiestDay { get; set; }

    /// <summary>Yazıcı bazında dönem toplamı (PrinterRef.Id -> sayfa).</summary>
    public Dictionary<int, long> PrinterTotals { get; set; } = new();

    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public bool AnyData => Days.Any(d => d.Total > 0);
}

public class PrinterRef
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class DailySummaryRow
{
    public DateOnly Date { get; set; }

    /// <summary>O gün yazıcı bazında basılan sayfa (PrinterRef.Id -> sayfa).</summary>
    public Dictionary<int, long> PerPrinter { get; set; } = new();

    public long Total { get; set; }

    public long PagesFor(int printerId) => PerPrinter.TryGetValue(printerId, out var v) ? v : 0;
}
