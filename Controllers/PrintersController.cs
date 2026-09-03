using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers;

/// <summary>
/// Yazıcı listesini (Ad + IP) doğrudan arayüzden yönetmeye yarar.
/// Buradan eklenen/silinen yazıcılar veritabanına yazılır; <c>PrinterMonitorWorker</c>
/// bir sonraki okuma döngüsünde otomatik dikkate alır (uygulama yeniden başlatmaya gerek yok).
/// appsettings.json'daki liste ise sadece açılışta bir kere DB ile eşitlenir (ekleme/güncelleme,
/// silme yapmaz) — bu yüzden ikisi çakışmaz.
/// </summary>
public class PrintersController : Controller
{
    private readonly AppDbContext _db;
    private readonly ISnmpService _snmp;

    public PrintersController(AppDbContext db, ISnmpService snmp)
    {
        _db = db;
        _snmp = snmp;
    }

    public async Task<IActionResult> Index()
    {
        var printers = await _db.Printers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new List<PrinterListRow>();
        foreach (var p in printers)
        {
            var readingCount = await _db.PrintReadings.AsNoTracking().CountAsync(r => r.PrinterId == p.Id);
            var last = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == p.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Select(r => new { r.TimestampUtc, r.PageCount })
                .FirstOrDefaultAsync();

            vm.Add(new PrinterListRow
            {
                Id = p.Id,
                Name = p.Name,
                IpAddress = p.IpAddress,
                ReadingCount = readingCount,
                LatestReadingUtc = last?.TimestampUtc,
                LatestCounter = last?.PageCount,
            });
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string ipAddress)
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

        var printer = new Printer { Name = name, IpAddress = ipAddress };
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

        // Kaydedilir edilmez bir kez SNMP okuması yap; başarılıysa ilk kaydı oluştur.
        var pageCount = await _snmp.GetPageCountAsync(printer.IpAddress, HttpContext.RequestAborted);
        if (pageCount is not null)
        {
            _db.PrintReadings.Add(new PrintReading
            {
                PrinterId = printer.Id,
                PageCount = pageCount.Value,
                TimestampUtc = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{name} ({ipAddress}) eklendi. İlk okuma yapıldı: sayaç = {pageCount.Value}.";
        }
        else
        {
            TempData["Success"] = $"{name} ({ipAddress}) eklendi, ancak ilk SNMP okuması yapılamadı (yazıcıya ulaşılamadı). Bir sonraki okuma döngüsünde tekrar denenecek.";
        }

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

public class PrinterListRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int ReadingCount { get; set; }

    public DateTime? LatestReadingUtc { get; set; }

    public long? LatestCounter { get; set; }
}
