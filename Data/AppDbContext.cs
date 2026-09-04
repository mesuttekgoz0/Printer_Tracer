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
    }
}
