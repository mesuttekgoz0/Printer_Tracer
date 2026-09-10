using Microsoft.EntityFrameworkCore;
using YaziciTakip.Models;

namespace YaziciTakip.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Printer> Printers => Set<Printer>();

    public DbSet<PrintReading> PrintReadings => Set<PrintReading>();

    public DbSet<Hakedis> Hakedisler => Set<Hakedis>();

    public DbSet<HakedisLine> HakedisLines => Set<HakedisLine>();

    public DbSet<Tedarikci> Tedarikciler => Set<Tedarikci>();

    public DbSet<Fiyat> Fiyatlar => Set<Fiyat>();

    public DbSet<FiyatDetay> FiyatDetaylari => Set<FiyatDetay>();

    public DbSet<Tur> Turler => Set<Tur>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Printer>()
            .HasIndex(p => p.IpAddress)
            .IsUnique();

        modelBuilder.Entity<PrintReading>()
            .HasOne(r => r.Printer)
            .WithMany(p => p.Readings)
            .HasForeignKey(r => r.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Yazıcı + zaman bazlı raporlama sorgularını hızlandırmak için.
        modelBuilder.Entity<PrintReading>()
            .HasIndex(r => new { r.PrinterId, r.TimestampUtc });

        modelBuilder.Entity<Hakedis>()
            .HasIndex(h => h.Number)
            .IsUnique();

        modelBuilder.Entity<HakedisLine>()
            .HasOne(l => l.Hakedis)
            .WithMany(h => h.Lines)
            .HasForeignKey(l => l.HakedisId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Bu yazıcının en son hakedişi" sorgusu için.
        modelBuilder.Entity<HakedisLine>()
            .HasIndex(l => l.PrinterId);

        // Yazıcı -> Tedarikçi (opsiyonel). Tedarikçi silinince yazıcı kalır, bağ boşalır.
        modelBuilder.Entity<Printer>()
            .HasOne(p => p.Tedarikci)
            .WithMany(t => t.Printers)
            .HasForeignKey(p => p.TedarikciId)
            .OnDelete(DeleteBehavior.SetNull);

        // Fiyat master -> Tedarikçi. Tedarikçi silinince fiyatları da silinir.
        modelBuilder.Entity<Fiyat>()
            .HasOne(f => f.Tedarikci)
            .WithMany(t => t.Fiyatlar)
            .HasForeignKey(f => f.TedarikciId)
            .OnDelete(DeleteBehavior.Cascade);

        // Her (tedarikçi, tür) için tek master.
        modelBuilder.Entity<Fiyat>()
            .HasIndex(f => new { f.TedarikciId, f.TurId })
            .IsUnique();

        modelBuilder.Entity<FiyatDetay>()
            .HasOne(d => d.Fiyat)
            .WithMany(f => f.Detaylar)
            .HasForeignKey(d => d.FiyatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FiyatDetay>()
            .HasIndex(d => new { d.FiyatId, d.BaslangicTarihi });

        // Sabit tür (lookup) tablosu: 1 = Siyah-Beyaz, 2 = Renkli.
        modelBuilder.Entity<Tur>().HasData(
            new Tur { Id = 1, Ad = "Siyah-Beyaz" },
            new Tur { Id = 2, Ad = "Renkli" });

        // Yazıcı -> Tür (opsiyonel FK). Tür silinse yazıcı kalır (pratikte tür silinmez).
        modelBuilder.Entity<Printer>()
            .HasOne(p => p.Tur)
            .WithMany(t => t.Printers)
            .HasForeignKey(p => p.TurId)
            .OnDelete(DeleteBehavior.SetNull);

        // Fiyat master/detay -> Tür (zorunlu FK). Tür silinemez (Restrict).
        modelBuilder.Entity<Fiyat>()
            .HasOne(f => f.Tur)
            .WithMany()
            .HasForeignKey(f => f.TurId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FiyatDetay>()
            .HasOne(d => d.Tur)
            .WithMany()
            .HasForeignKey(d => d.TurId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
