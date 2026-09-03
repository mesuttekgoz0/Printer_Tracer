using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Arka planda periyodik olarak (config: PollingIntervalMinutes) tüm yazıcıların
/// SNMP sayfa sayacını okur ve SQLite'a zaman damgasıyla kaydeder.
/// </summary>
public class PrinterMonitorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISnmpService _snmpService;
    private readonly PrinterMonitoringOptions _options;
    private readonly ILogger<PrinterMonitorWorker> _logger;

    public PrinterMonitorWorker(
        IServiceScopeFactory scopeFactory,
        ISnmpService snmpService,
        IOptions<PrinterMonitoringOptions> options,
        ILogger<PrinterMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _snmpService = snmpService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Host'un başlatmasını (Kestrel "Now listening..." mesajı dahil) bloklamamak için
        // hemen geri dönüş ver; ağır işleri arka planda yap.
        await Task.Yield();

        try
        {
            await SyncPrintersFromConfigAsync(stoppingToken);
            await SeedSampleDataIfEnabledAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Başlangıç hazırlığı başarısız (DB/migration/seed).");
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.PollingIntervalMinutes));
        _logger.LogInformation(
            "Yazıcı izleme aktif. Okuma aralığı: {Interval}. İlk okuma birkaç saniye içinde başlayacak.", interval);

        // Konsol başlangıç loglarının oturması için kısa bir gecikme, sonra ilk okuma.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await PollAllPrintersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Okuma döngüsünde beklenmeyen hata.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// appsettings.json'daki yazıcı listesini DB ile eşitler: IP'ye göre eşleşmeyenleri ekler,
    /// ismi değişenleri günceller. (Config'den silinen yazıcılar DB'de bırakılır ki geçmiş veri kaybolmasın.)
    /// </summary>
    private async Task SyncPrintersFromConfigAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        var configured = _options.Printers
            .Where(p => !string.IsNullOrWhiteSpace(p.IpAddress))
            .ToList();

        if (configured.Count == 0)
        {
            _logger.LogWarning("Config'de tanımlı yazıcı yok. appsettings.json -> PrinterMonitoring:Printers");
            return;
        }

        var existing = await db.Printers.ToDictionaryAsync(p => p.IpAddress, cancellationToken);

        foreach (var cfg in configured)
        {
            if (existing.TryGetValue(cfg.IpAddress, out var printer))
            {
                if (!string.IsNullOrWhiteSpace(cfg.Name) && printer.Name != cfg.Name)
                    printer.Name = cfg.Name;
            }
            else
            {
                db.Printers.Add(new Printer
                {
                    Name = string.IsNullOrWhiteSpace(cfg.Name) ? cfg.IpAddress : cfg.Name,
                    IpAddress = cfg.IpAddress,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Config'de <c>SeedSampleReadings=true</c> ise (yalnız Development) ve tablo boşsa
    /// demo amaçlı örnek okuma verisi üretir.
    /// </summary>
    private async Task SeedSampleDataIfEnabledAsync(CancellationToken cancellationToken)
    {
        if (!_options.SeedSampleReadings)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<SampleDataSeeder>();

        await seeder.SeedAsync(db, _options.SampleDataDays, _options.PollingIntervalMinutes, cancellationToken);
    }

    private async Task PollAllPrintersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var printers = await db.Printers.AsNoTracking().ToListAsync(cancellationToken);
        if (printers.Count == 0)
        {
            _logger.LogWarning("Kayıtlı yazıcı yok; okuma atlandı.");
            return;
        }

        // Tüm yazıcıları paralel oku: ulaşılamayanlar için 3 yazıcı ~30 sn yerine ~10 sn sürer.
        var timestamp = DateTime.UtcNow;
        var reads = await Task.WhenAll(printers.Select(async printer =>
        {
            var pageCount = await _snmpService.GetPageCountAsync(printer.IpAddress, cancellationToken);
            return (printer, pageCount);
        }));

        var failed = new List<string>();
        foreach (var (printer, pageCount) in reads)
        {
            if (pageCount is null)
            {
                failed.Add(printer.Name);
                _logger.LogDebug("{Name} ({Ip}): sayaç okunamadı.", printer.Name, printer.IpAddress);
                continue;
            }

            db.PrintReadings.Add(new PrintReading
            {
                PrinterId = printer.Id,
                PageCount = pageCount.Value,
                TimestampUtc = timestamp,
            });
            _logger.LogDebug("{Name} ({Ip}): sayaç = {Count}", printer.Name, printer.IpAddress, pageCount.Value);
        }

        var saved = reads.Count(r => r.pageCount is not null);
        if (saved > 0)
            await db.SaveChangesAsync(cancellationToken);

        if (failed.Count == 0)
            _logger.LogInformation("Okuma tamamlandı: {Saved}/{Total} yazıcı kaydedildi.", saved, printers.Count);
        else
            _logger.LogWarning("Okuma tamamlandı: {Saved}/{Total} kaydedildi. Ulaşılamayan: {Failed}",
                saved, printers.Count, string.Join(", ", failed));
    }
}
