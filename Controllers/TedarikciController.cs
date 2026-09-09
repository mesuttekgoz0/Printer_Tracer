using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

/// <summary>
/// Tedarikçiler (yazıcı servis/bayi firmaları) ve her tedarikçinin tarihli sayfa-başı
/// fiyat listeleri. Yazıcı–tedarikçi eşleşmesi Yazıcılar sayfasından yapılır.
/// Hakediş, bir tedarikçi seçilerek üretilir (<see cref="HakedisController"/>).
/// </summary>
public class TedarikciController : Controller
{
    private readonly AppDbContext _db;

    public TedarikciController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _db.Tedarikciler.AsNoTracking()
            .OrderBy(t => t.Ad)
            .Select(t => new TedarikciListRow
            {
                Id = t.Id,
                Ad = t.Ad,
                Not = t.Not,
                PrinterCount = t.Printers.Count,
                PriceListCount = t.FiyatListeleri.Count,
                LatestPriceListDate = t.FiyatListeleri
                    .OrderByDescending(f => f.Tarih)
                    .Select(f => (DateOnly?)f.Tarih)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string ad, string? not)
    {
        ad = (ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ad))
        {
            TempData["Error"] = "Tedarikçi adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        _db.Tedarikciler.Add(new Tedarikci { Ad = ad, Not = Clean(not) });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{ad} eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string ad, string? not)
    {
        ad = (ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ad))
        {
            TempData["Error"] = "Tedarikçi adı boş olamaz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is not null)
        {
            t.Ad = ad;
            t.Not = Clean(not);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Tedarikçi güncellendi.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.Tedarikciler.FindAsync(id);
        if (t is not null)
        {
            _db.Tedarikciler.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{t.Ad} silindi. Bağlı yazıcıların tedarikçi bağı kaldırıldı; geçmiş hakedişler etkilenmedi.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await _db.Tedarikciler.AsNoTracking()
            .Include(x => x.Printers.OrderBy(p => p.Name)).ThenInclude(p => p.Tur)
            .Include(x => x.FiyatListeleri.OrderByDescending(f => f.Tarih))
                .ThenInclude(f => f.Satirlar)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t is null)
            return NotFound();

        var turler = await _db.Turler.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return View(new TedarikciDetailsViewModel { Tedarikci = t, Turler = turler });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPriceList(int tedarikciId, string listeAdi, DateOnly? tarih,
        int[] turId, string[] fiyat)
    {
        var t = await _db.Tedarikciler.FindAsync(tedarikciId);
        if (t is null)
            return NotFound();

        listeAdi = (listeAdi ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(listeAdi))
        {
            TempData["Error"] = "Liste adı boş olamaz.";
            return RedirectToAction(nameof(Details), new { id = tedarikciId });
        }

        var liste = new FiyatListesi
        {
            TedarikciId = tedarikciId,
            ListeAdi = listeAdi,
            Tarih = tarih ?? DateOnly.FromDateTime(DateTime.Now),
        };

        for (var i = 0; i < turId.Length; i++)
        {
            liste.Satirlar.Add(new FiyatSatiri
            {
                TurId = turId[i],
                SayfaBasiFiyat = ParsePrice(i < fiyat.Length ? fiyat[i] : null),
            });
        }

        _db.FiyatListeleri.Add(liste);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{listeAdi}\" fiyat listesi eklendi.";
        return RedirectToAction(nameof(Details), new { id = tedarikciId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPriceList(int id, string listeAdi, DateOnly? tarih,
        int[] turId, string[] fiyat)
    {
        var liste = await _db.FiyatListeleri
            .Include(f => f.Satirlar)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (liste is null)
            return NotFound();

        listeAdi = (listeAdi ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(listeAdi))
        {
            TempData["Error"] = "Liste adı boş olamaz.";
            return RedirectToAction(nameof(Details), new { id = liste.TedarikciId });
        }

        liste.ListeAdi = listeAdi;
        if (tarih is { } t)
            liste.Tarih = t;

        for (var i = 0; i < turId.Length; i++)
        {
            var f = ParsePrice(i < fiyat.Length ? fiyat[i] : null);
            var row = liste.Satirlar.FirstOrDefault(s => s.TurId == turId[i]);
            if (row is null)
                liste.Satirlar.Add(new FiyatSatiri { TurId = turId[i], SayfaBasiFiyat = f });
            else
                row.SayfaBasiFiyat = f;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{listeAdi}\" fiyat listesi güncellendi.";
        return RedirectToAction(nameof(Details), new { id = liste.TedarikciId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePriceList(int id)
    {
        var liste = await _db.FiyatListeleri.FindAsync(id);
        if (liste is null)
            return NotFound();

        var tedarikciId = liste.TedarikciId;
        _db.FiyatListeleri.Remove(liste);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Fiyat listesi silindi. Bu listeyle üretilmiş hakedişler etkilenmedi.";
        return RedirectToAction(nameof(Details), new { id = tedarikciId });
    }

    private static string? Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Fiyat metnini kültürden bağımsız çözümle ("0,15" ve "0.15" kabul edilir).</summary>
    internal static decimal ParsePrice(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0m;

        s = s.Trim().Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0
            ? d
            : 0m;
    }
}

public class TedarikciListRow
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Not { get; set; }
    public int PrinterCount { get; set; }
    public int PriceListCount { get; set; }
    public DateOnly? LatestPriceListDate { get; set; }
}

public class TedarikciDetailsViewModel
{
    public Tedarikci Tedarikci { get; set; } = null!;

    /// <summary>Fiyat listesi formlarında gösterilecek tür satırları (sabit: Siyah-Beyaz, Renkli).</summary>
    public List<Tur> Turler { get; set; } = new();
}
