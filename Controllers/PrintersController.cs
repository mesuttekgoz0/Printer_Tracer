using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

/// <summary>
/// Yazıcı listesini (Ad + IP) doğrudan arayüzden yönetmeye yarar.
/// Buradan eklenen/silinen yazıcılar yalnızca veritabanına yazılır; sayaç okuması
/// yapılmaz — ilk okuma "Sayaç Oku" sayfasından elle alınır.
/// appsettings.json'daki liste ise sadece açılışta bir kere DB ile eşitlenir (ekleme/güncelleme,
/// silme yapmaz).
/// </summary>
public class PrintersController : Controller
{
    private readonly AppDbContext _db;

    public PrintersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .Include(p => p.Tedarikci)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var suppliers = await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new SupplierOption { Id = t.Id, Ad = t.Ad })
            .ToListAsync();

        var types = await _db.Turler.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new TurOption { Id = t.Id, Ad = t.Ad })
            .ToListAsync();

        var vm = new PrintersIndexViewModel { Suppliers = suppliers, Types = types };
        foreach (var p in printers)
        {
            var readingCount = await _db.PrintReadings.AsNoTracking().CountAsync(r => r.PrinterId == p.Id);
            var last = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == p.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .FirstOrDefaultAsync();

            vm.Printers.Add(new PrinterListRow
            {
                Id = p.Id,
                Name = p.Name,
                IpAddress = p.IpAddress,
                TurId = p.TurId,
                TurAd = p.TurAdi,
                TedarikciId = p.TedarikciId,
                TedarikciAd = p.Tedarikci?.Ad,
                ReadingCount = readingCount,
                LatestReadingUtc = last?.TimestampUtc,
                LatestCounter = last?.PageCount,
            });
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string ipAddress, int? turId, int? tedarikciId)
    {
        name = (name ?? string.Empty).Trim();
        ipAddress = (ipAddress ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Yazıcı adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            TempData["Error"] = $"\"{ipAddress}\" geçerli bir IP adresi değil.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _db.Printers.AnyAsync(p => p.IpAddress == ipAddress);
        if (exists)
        {
            TempData["Error"] = $"{ipAddress} zaten kayıtlı bir yazıcı.";
            return RedirectToAction(nameof(Index));
        }

        var supplierExists = tedarikciId is int sid && await _db.Tedarikciler.AnyAsync(t => t.Id == sid);
        var typeExists = turId is int tid && await _db.Turler.AnyAsync(t => t.Id == tid);
        var printer = new Printer
        {
            Name = name,
            IpAddress = ipAddress,
            TurId = typeExists ? turId : null,
            TedarikciId = supplierExists ? tedarikciId : null,
        };
        _db.Printers.Add(printer);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = $"{ipAddress} zaten kayıtlı bir yazıcı.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"{name} ({ipAddress}) eklendi. İlk sayaç değeri için “Sayaç Oku” sayfasından okuma alın.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, string name)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Yazıcı adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var printer = await _db.Printers.FindAsync(id);
        if (printer is not null)
        {
            printer.Name = name;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Yazıcı adı güncellendi.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetType(int id, int? turId)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is not null)
        {
            var typeExists = turId is int tid && await _db.Turler.AnyAsync(t => t.Id == tid);
            printer.TurId = typeExists ? turId : null;
            await _db.SaveChangesAsync();

            var ad = printer.TurId is null
                ? Tur.Belirtilmemis
                : await _db.Turler.Where(t => t.Id == printer.TurId).Select(t => t.Ad).FirstAsync();
            TempData["Success"] = $"{printer.Name} türü \"{ad}\" olarak güncellendi.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSupplier(int id, int? tedarikciId)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is not null)
        {
            printer.TedarikciId = tedarikciId;
            await _db.SaveChangesAsync();

            var ad = tedarikciId is null
                ? null
                : await _db.Tedarikciler.Where(t => t.Id == tedarikciId).Select(t => t.Ad).FirstOrDefaultAsync();
            TempData["Success"] = ad is null
                ? $"{printer.Name} tedarikçi bağı kaldırıldı."
                : $"{printer.Name} tedarikçisi \"{ad}\" olarak ayarlandı.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var printer = await _db.Printers.FindAsync(id);
        if (printer is not null)
        {
            _db.Printers.Remove(printer);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{printer.Name} ({printer.IpAddress}) ve geçmiş okumaları silindi.";
        }

        return RedirectToAction(nameof(Index));
    }
}

public class PrintersIndexViewModel
{
    public List<PrinterListRow> Printers { get; set; } = new();

    public List<SupplierOption> Suppliers { get; set; } = new();

    public List<TurOption> Types { get; set; } = new();
}

public class SupplierOption
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;
}

public class TurOption
{
    public int Id { get; set; }

    public string Ad { get; set; } = string.Empty;
}

public class PrinterListRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int? TurId { get; set; }

    public string TurAd { get; set; } = Tur.Belirtilmemis;

    public int? TedarikciId { get; set; }

    public string? TedarikciAd { get; set; }

    public int ReadingCount { get; set; }

    public DateTime? LatestReadingUtc { get; set; }

    public long? LatestCounter { get; set; }
}
