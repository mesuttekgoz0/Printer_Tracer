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

    public DbSet<FiyatListesi> FiyatListeleri => Set<FiyatListesi>();

    public DbSet<FiyatSatiri> FiyatSatirlari => Set<FiyatSatiri>();

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

        // Fiyat listesi -> Tedarikçi. Tedarikçi silinince fiyat listeleri de silinir.
        modelBuilder.Entity<FiyatListesi>()
            .HasOne(f => f.Tedarikci)
            .WithMany(t => t.FiyatListeleri)
            .HasForeignKey(f => f.TedarikciId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FiyatListesi>()
            .HasIndex(f => new { f.TedarikciId, f.Tarih });

        modelBuilder.Entity<FiyatSatiri>()
            .HasOne(s => s.FiyatListesi)
            .WithMany(f => f.Satirlar)
            .HasForeignKey(s => s.FiyatListesiId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // Fiyat listesi satırı -> Tür (zorunlu FK). Tür silinemez (Restrict).
        modelBuilder.Entity<FiyatSatiri>()
            .HasOne(s => s.Tur)
            .WithMany()
            .HasForeignKey(s => s.TurId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
