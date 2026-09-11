using YaziciTakip.Data.Repositories;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Kayıtlı tüm yazıcıların (veya verilen alt kümenin) SNMP sayfa sayacını tek seferde
/// okuyup kaydeder. Arayüzdeki "Sayaç Oku" (POST /api/readings/read) tetikler.
/// </summary>
public class PrinterReadingService
{
    private readonly IPrinterRepository _printers;
    private readonly IPrintReadingRepository _readings;
    private readonly ISnmpService _snmp;

    public PrinterReadingService(IPrinterRepository printers, IPrintReadingRepository readings, ISnmpService snmp)
    {
        _printers = printers;
        _readings = readings;
        _snmp = snmp;
    }

    /// <summary>
    /// Yazıcıları paralel okur, ulaşılabilenler için yeni bir okuma ekler ve yazıcı başına
    /// önceki sayaç / yeni sayaç / fark bilgisini döndürür. <paramref name="printerIds"/>
    /// verilirse yalnızca o yazıcılar okunur; boş/null ise hepsi.
    /// </summary>
    public async Task<ManualReadResult> ReadAllAsync(
        IReadOnlyCollection<int>? printerIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ManualReadResult();

        var all = await _printers.GetAllAsync(cancellationToken);
        var printers = (printerIds is { Count: > 0 } ? all.Where(p => printerIds.Contains(p.Id)) : all)
            .OrderBy(p => p.Name)
            .ToList();

        if (printers.Count == 0)
            return result;

        // Bu okumadan ÖNCEki son sayaç (fark hesaplamak için) — yeni kayıt eklemeden önce topla.
        var previous = new Dictionary<int, (long Count, DateTime Utc)>();
        foreach (var p in printers)
        {
            var last = await _readings.GetLatestAsync(p.Id, cancellationToken);
            if (last is not null)
                previous[p.Id] = (last.PageCount, last.TimestampUtc);
        }

        // Ulaşılamayan yazıcılarda timeout beklememek için paralel oku.
        // Marka/model (sysDescr) yalnızca daha önce alınmamış yazıcılar için sorgulanır.
        var timestamp = DateTime.UtcNow;
        var reads = await Task.WhenAll(printers.Select(async p =>
        {
            var countTask = _snmp.GetPageCountAsync(p.IpAddress, cancellationToken);
            var modelTask = string.IsNullOrWhiteSpace(p.Model)
                ? _snmp.GetModelAsync(p.IpAddress, cancellationToken)
                : Task.FromResult<string?>(null);
            await Task.WhenAll(countTask, modelTask);
            return (Printer: p, Count: countTask.Result, Model: modelTask.Result);
        }));

        foreach (var (printer, count, model) in reads)
        {
            if (!string.IsNullOrWhiteSpace(model) && !string.Equals(printer.Model, model, StringComparison.Ordinal))
                await _printers.UpdateModelAsync(printer.Id, model, cancellationToken);

            var row = new ManualReadRow
            {
                PrinterId = printer.Id,
                PrinterName = printer.Name,
                IpAddress = printer.IpAddress,
            };

            if (previous.TryGetValue(printer.Id, out var prev))
            {
                row.PreviousCounter = prev.Count;
                row.PreviousReadingUtc = prev.Utc;
            }

            if (count is null)
            {
                row.Reachable = false;
            }
            else
            {
                row.Reachable = true;
                row.NewCounter = count.Value;
                await _readings.InsertAsync(printer.Id, count.Value, timestamp, cancellationToken);
            }

            result.Rows.Add(row);
        }

        return result;
    }
}
