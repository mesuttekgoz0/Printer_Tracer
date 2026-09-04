using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Kayıtlı tüm yazıcıların SNMP sayfa sayacını tek seferde okuyup SQLite'a kaydeder.
/// Arayüzdeki "Şimdi Oku" düğmesi ve (açıksa) <see cref="PrinterMonitorWorker"/> ortak kullanır.
/// </summary>
public class PrinterReadingService
{
    private readonly AppDbContext _db;
    private readonly ISnmpService _snmp;

    public PrinterReadingService(AppDbContext db, ISnmpService snmp)
    {
        _db = db;
        _snmp = snmp;
    }

    /// <summary>
    /// Yazıcıları paralel okur, ulaşılabilenler için yeni bir <see cref="PrintReading"/> ekler
    /// ve yazıcı başına önceki sayaç / yeni sayaç / fark bilgisini döndürür.
    /// <paramref name="printerIds"/> verilirse yalnızca o yazıcılar okunur; boş/null ise hepsi.
    /// </summary>
    public async Task<ManualReadResult> ReadAllAsync(
        IReadOnlyCollection<int>? printerIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ManualReadResult();

        // Tracked: aşağıda gerekirse Printer.Model güncellenip aynı SaveChanges ile yazılacak.
        var query = _db.Printers.OrderBy(p => p.Name).AsQueryable();
        if (printerIds is { Count: > 0 })
            query = query.Where(p => printerIds.Contains(p.Id));

        var printers = await query.ToListAsync(cancellationToken);

        if (printers.Count == 0)
            return result;

        // Bu okumadan ÖNCEki son sayaç (fark hesaplamak için) — yeni kayıt eklemeden önce topla.
        var previous = new Dictionary<int, (long Count, DateTime Utc)>();
        foreach (var p in printers)
        {
            var last = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == p.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.PageCount, r.TimestampUtc })
                .FirstOrDefaultAsync(cancellationToken);

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
                printer.Model = model;   // tracked → aşağıdaki SaveChanges ile kalıcı


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
                _db.PrintReadings.Add(new PrintReading
                {
                    PrinterId = printer.Id,
                    PageCount = count.Value,
                    TimestampUtc = timestamp,
                });
            }

            result.Rows.Add(row);
        }

        // Yeni okumalar ve/veya güncellenen Printer.Model.
        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);

        return result;
    }
}
