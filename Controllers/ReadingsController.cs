using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers;

/// <summary>
/// "Sayaç Oku" sayfası: kullanıcı tek düğmeyle tüm yazıcıların o anki sayacını okutur.
/// Düğmeye basılmasa bile her yazıcının son okuması ve bir öncekine göre farkı tabloda durur.
/// Periyodik/otomatik okuma varsayılan olarak kapalı.
/// </summary>
public class ReadingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly PrinterReadingService _reader;

    public ReadingsController(AppDbContext db, PrinterReadingService reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadNow(int[] printerIds, string? mode)
    {
        // "Seçili yazıcıları oku" ile hiçbir satır işaretlenmemişse uyar.
        if (mode == "selected" && printerIds.Length == 0)
        {
            TempData["Error"] = "Önce okumak istediğiniz yazıcıları seçin.";
            return RedirectToAction(nameof(Index));
        }

        var selected = printerIds.Length > 0;
        var result = await _reader.ReadAllAsync(selected ? printerIds : null, HttpContext.RequestAborted);
        var noun = selected ? "seçili yazıcı" : "yazıcı";

        if (result.Total == 0)
        {
            TempData["Error"] = "Kayıtlı yazıcı yok. Önce Yazıcılar sayfasından yazıcı ekleyin.";
        }
        else if (result.FailedNames.Count == 0)
        {
            TempData["Success"] = result.TotalDelta > 0
                ? $"{result.SavedCount}/{result.Total} {noun} okundu. Önceki okumadan bu yana toplam {result.TotalDelta:N0} sayfa basılmış."
                : $"{result.SavedCount}/{result.Total} {noun} okundu. Sayaçlarda değişiklik yok.";
        }
        else
        {
            TempData["Warning"] =
                $"{result.SavedCount}/{result.Total} {noun} okundu. Ulaşılamayan: {string.Join(", ", result.FailedNames)}.";
        }

        // PRG: yenilemede tekrar okuma yapılmasın diye GET'e yönlendir.
        return RedirectToAction(nameof(Index));
    }

    private async Task<ReadingsIndexViewModel> BuildAsync()
    {
        var printers = await _db.Printers.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new ReadingsIndexViewModel { PrinterCount = printers.Count };

        foreach (var printer in printers)
        {
            var last2 = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == printer.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Take(2)
                .Select(r => new { r.PageCount, r.TimestampUtc })
                .ToListAsync();

            var count = await _db.PrintReadings.AsNoTracking()
                .CountAsync(r => r.PrinterId == printer.Id);

            var status = new PrinterReadingStatus
            {
                PrinterId = printer.Id,
                PrinterName = printer.Name,
                IpAddress = printer.IpAddress,
                Model = printer.Model,
                ReadingCount = count,
            };

            if (last2.Count > 0)
            {
                status.LatestCounter = last2[0].PageCount;
                status.LatestReadingUtc = last2[0].TimestampUtc;
            }
            if (last2.Count > 1)
            {
                status.PreviousCounter = last2[1].PageCount;
                status.PreviousReadingUtc = last2[1].TimestampUtc;
            }

            vm.Printers.Add(status);

            if (status.LatestReadingUtc is { } t && (vm.LastReadingUtc is null || t > vm.LastReadingUtc))
                vm.LastReadingUtc = t;
        }

        return vm;
    }
}
