using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Services;

/// <summary>
/// Demo/geliştirme amaçlı örnek okuma verisi üretir: yazıcı başına, geçmişe dönük,
/// okuma aralığı adımlarıyla artan bir sayfa sayacı. Ofis kullanımına benzer bir
/// dağılım kullanır (mesai saatleri yoğun, gece/hafta sonu az).
/// Sadece PrintReadings tablosu BOŞ ise çalışır — gerçek veriyi asla değiştirmez.
/// </summary>
public class SampleDataSeeder
{
    private readonly ILogger<SampleDataSeeder> _logger;

    public SampleDataSeeder(ILogger<SampleDataSeeder> logger)
    {
        _logger = logger;
    }

    public async Task SeedAsync(AppDbContext db, int days, int intervalMinutes, CancellationToken cancellationToken)
    {
        if (await db.PrintReadings.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Örnek veri atlandı: PrintReadings tablosunda zaten kayıt var.");
            return;
        }

        var printers = await db.Printers.AsNoTracking().ToListAsync(cancellationToken);
        if (printers.Count == 0)
        {
            _logger.LogWarning("Örnek veri atlandı: kayıtlı yazıcı yok.");
            return;
        }

        var rng = new Random(20260902);
        var step = TimeSpan.FromMinutes(Math.Max(5, intervalMinutes));
        var now = DateTime.UtcNow;
        var start = now.AddDays(-Math.Max(1, days));

        var rows = new List<PrintReading>();

        foreach (var printer in printers)
        {
            long counter = rng.Next(10_000, 60_000); // yazıcının o ana kadarki ömür sayacı

            for (var t = start; t <= now; t = t.Add(step))
            {
                counter += PagesInInterval(t.ToLocalTime(), rng);
                rows.Add(new PrintReading
                {
                    PrinterId = printer.Id,
                    PageCount = counter,
                    TimestampUtc = t,
                });
            }
        }

        db.PrintReadings.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Örnek veri eklendi: {Rows} okuma, {Printers} yazıcı, {Days} gün.",
            rows.Count, printers.Count, days);
    }

    /// <summary>Bir okuma aralığında (adımda) basıldığı varsayılan sayfa sayısı.</summary>
    private static int PagesInInterval(DateTime local, Random rng)
    {
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return rng.Next(0, 3);

        var hour = local.Hour;
        if (hour is < 8 or >= 19)
            return rng.Next(0, 2);

        var peak = hour is (>= 9 and <= 11) or (>= 13 and <= 16);
        return peak ? rng.Next(8, 40) : rng.Next(2, 15);
    }
}
