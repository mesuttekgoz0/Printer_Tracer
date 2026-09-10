using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaziciTakip.Data;
using YaziciTakip.Services;

namespace YaziciTakip.Controllers.Api;

/// <summary>"Sayaç Oku" sayfasının JSON API'si.</summary>
[ApiController]
[Route("api/readings")]
[Produces("application/json")]
public class ReadingsApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PrinterReadingService _reader;

    public ReadingsApiController(AppDbContext db, PrinterReadingService reader)
    {
        _db = db;
        _reader = reader;
    }

    /// <summary>Her yazıcının son iki okuması ve aradaki fark.</summary>
    [HttpGet]
    public async Task<ActionResult<ReadingsOverviewDto>> Get()
    {
        var printers = await _db.Printers.AsNoTracking()
            .Include(p => p.Tur)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var rows = new List<ReadingStatusDto>();
        DateTime? lastAll = null;
        long totalDelta = 0;

        foreach (var p in printers)
        {
            var last2 = await _db.PrintReadings.AsNoTracking()
                .Where(r => r.PrinterId == p.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Take(2)
                .Select(r => new { r.PageCount, r.TimestampUtc })
                .ToListAsync();

            var count = await _db.PrintReadings.AsNoTracking().CountAsync(r => r.PrinterId == p.Id);

            long? latest = last2.Count > 0 ? last2[0].PageCount : null;
            DateTime? latestUtc = last2.Count > 0 ? last2[0].TimestampUtc : null;
            long? prev = last2.Count > 1 ? last2[1].PageCount : null;
            DateTime? prevUtc = last2.Count > 1 ? last2[1].TimestampUtc : null;
            long? delta = latest is { } l && prev is { } pr && l >= pr ? l - pr : null;
            if (delta is { } d) totalDelta += d;

            if (latestUtc is { } t && (lastAll is null || t > lastAll)) lastAll = t;

            rows.Add(new ReadingStatusDto(
                p.Id, p.Name, p.IpAddress, p.Model, p.TurId, p.TurAdi,
                count, latest, latestUtc, prev, prevUtc, delta, latestUtc is not null));
        }

        return new ReadingsOverviewDto(printers.Count, lastAll, totalDelta, rows);
    }

    /// <summary>Tüm ya da seçili yazıcıların o anki sayacını SNMP ile okur ve kaydeder.</summary>
    [HttpPost("read")]
    public async Task<ActionResult<ReadResultDto>> ReadNow([FromBody] ReadNowRequest? req)
    {
        var ids = req?.PrinterIds ?? Array.Empty<int>();
        var selected = ids.Length > 0;
        var result = await _reader.ReadAllAsync(selected ? ids : null, HttpContext.RequestAborted);
        var noun = selected ? "seçili yazıcı" : "yazıcı";

        string message;
        if (result.Total == 0)
            message = "Kayıtlı yazıcı yok.";
        else if (result.FailedNames.Count == 0)
            message = result.TotalDelta > 0
                ? $"{result.SavedCount}/{result.Total} {noun} okundu. Önceki okumadan bu yana toplam {result.TotalDelta:N0} sayfa basılmış."
                : $"{result.SavedCount}/{result.Total} {noun} okundu. Sayaçlarda değişiklik yok.";
        else
            message = $"{result.SavedCount}/{result.Total} {noun} okundu. Ulaşılamayan: {string.Join(", ", result.FailedNames)}.";

        return new ReadResultDto(
            result.Total, result.SavedCount, result.FailedNames.ToArray(), result.TotalDelta,
            result.FailedNames.Count == 0 && result.Total > 0, message);
    }
}

public record ReadingsOverviewDto(
    int PrinterCount, DateTime? LastReadingUtc, long TotalDelta, IReadOnlyList<ReadingStatusDto> Printers);

public record ReadingStatusDto(
    int PrinterId, string PrinterName, string IpAddress, string? Model, int? TurId, string TurAd,
    int ReadingCount, long? LatestCounter, DateTime? LatestReadingUtc,
    long? PreviousCounter, DateTime? PreviousReadingUtc, long? Delta, bool HasReading);

public record ReadNowRequest(int[]? PrinterIds);

public record ReadResultDto(
    int Total, int SavedCount, string[] FailedNames, long TotalDelta, bool Success, string Message);
