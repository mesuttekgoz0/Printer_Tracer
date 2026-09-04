using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Açılışta yalnızca DB migration + config yazıcı eşitlemesini yapar; <b>sayaç okuması yapmaz</b>.
/// <c>PrinterMonitoring:PollingEnabled=true</c> ise periyodik olarak (config: PollingIntervalMinutes)
/// tüm yazıcıların SNMP sayfa sayacını okur — ilk okuma bir tam aralık sonra, program açılır açılmaz değil.
/// Varsayılan olarak periyodik okuma kapalıdır; okumalar yalnızca arayüzdeki "Sayaç Oku" ile yapılır.
/// </summary>
public class PrinterMonitorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PrinterMonitoringOptions _options;
    private readonly ILogger<PrinterMonitorWorker> _logger;

    public PrinterMonitorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PrinterMonitoringOptions> options,
        ILogger<PrinterMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
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

        if (!_options.PollingEnabled)
        {
            _logger.LogInformation(
                "Periyodik SNMP okuma kapalı (PrinterMonitoring:PollingEnabled=false). " +
                "Okumalar yalnızca arayüzdeki 'Şimdi Oku' düğmesiyle yapılacak.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.PollingIntervalMinutes));
        _logger.LogInformation(
            "Periyodik yazıcı izleme aktif. Okuma aralığı: {Interval}. " +
            "Açılışta okuma yapılmaz; ilk okuma bir aralık sonra başlar.", interval);

        // Program açılır açılmaz okuma YAPMA: ilk okumayı bir tam aralık bekledikten sonra yap.
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
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
        var reader = scope.ServiceProvider.GetRequiredService<PrinterReadingService>();

        var result = await reader.ReadAllAsync(printerIds: null, cancellationToken);

        if (result.Total == 0)
        {
            _logger.LogWarning("Kayıtlı yazıcı yok; okuma atlandı.");
            return;
        }

        if (result.FailedNames.Count == 0)
            _logger.LogInformation("Okuma tamamlandı: {Saved}/{Total} yazıcı kaydedildi.",
                result.SavedCount, result.Total);
        else
            _logger.LogWarning("Okuma tamamlandı: {Saved}/{Total} kaydedildi. Ulaşılamayan: {Failed}",
                result.SavedCount, result.Total, string.Join(", ", result.FailedNames));
    }
}
