using YaziciTakip.Models;

namespace YaziciTakip.Data.Repositories;

/// <summary>Hakediş listesindeki bir satır: temel bilgi + satır sayısı/toplam (JOIN+GROUP BY).</summary>
public record HakedisListRow(
    int Id, string Number, string? TedarikciAd, DateTime CreatedUtc, DateOnly PeriodStart, DateOnly PeriodEnd,
    int PrinterCount, long TotalPages, decimal TotalAmount);

/// <summary>Bir yazıcının en son hakediş satırındaki sayaç + o hakedişin dönem bitişi.</summary>
public record LastHakedisLineForPrinter(long CurrentCounter, DateOnly PeriodEnd);

public interface IHakedisRepository
{
    Task<List<HakedisListRow>> GetAllAsync(CancellationToken ct = default);

    Task<Hakedis?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<List<HakedisLine>> ListLinesAsync(int hakedisId, CancellationToken ct = default);

    /// <summary>O yıl (prefix, ör. "2026-") içindeki en yüksek sıra + 1'den üretilen yeni numara.</summary>
    Task<string> NextNumberAsync(string prefix, CancellationToken ct = default);

    Task<int> InsertAsync(Hakedis h, CancellationToken ct = default);

    Task InsertLineAsync(int hakedisId, HakedisLine line, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<LastHakedisLineForPrinter?> GetLastLineForPrinterAsync(int printerId, CancellationToken ct = default);
}

public class HakedisRepository(AppDbContext db) : SqlRepositoryBase(db), IHakedisRepository
{
    public async Task<List<HakedisListRow>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<HakedisListRow>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Hakedis_GetAll");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new HakedisListRow(
                GetInt(r, "Id"), GetString(r, "Number"), GetStringN(r, "TedarikciAd"),
                GetDateTimeUtc(r, "CreatedUtc"), GetDateOnly(r, "PeriodStart"), GetDateOnly(r, "PeriodEnd"),
                GetInt(r, "PrinterCount"), GetLong(r, "TotalPages"), GetDecimal(r, "TotalAmount")));
        return result;
    }

    public async Task<Hakedis?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Hakedis_GetById");
        P(cmd, "@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Hakedis
        {
            Id = GetInt(r, "Id"),
            Number = GetString(r, "Number"),
            TedarikciId = GetIntN(r, "TedarikciId"),
            TedarikciAd = GetStringN(r, "TedarikciAd"),
            CreatedUtc = GetDateTimeUtc(r, "CreatedUtc"),
            PeriodStart = GetDateOnly(r, "PeriodStart"),
            PeriodEnd = GetDateOnly(r, "PeriodEnd"),
            Note = GetStringN(r, "Note"),
        };
    }

    public async Task<List<HakedisLine>> ListLinesAsync(int hakedisId, CancellationToken ct = default)
    {
        var result = new List<HakedisLine>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.HakedisLine_ListByHakedis");
        P(cmd, "@HakedisId", hakedisId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new HakedisLine
            {
                Id = GetInt(r, "Id"),
                HakedisId = GetInt(r, "HakedisId"),
                PrinterId = GetIntN(r, "PrinterId"),
                PrinterName = GetString(r, "PrinterName"),
                Model = GetStringN(r, "Model"),
                TurId = GetIntN(r, "TurId"),
                TurAd = GetStringN(r, "TurAd"),
                PreviousCounter = GetLong(r, "PreviousCounter"),
                CurrentCounter = GetLong(r, "CurrentCounter"),
                Pages = GetLongN(r, "Pages"),
                UnitPrice = GetDecimal(r, "UnitPrice"),
                Amount = GetDecimal(r, "Amount"),
            });
        return result;
    }

    public async Task<string> NextNumberAsync(string prefix, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Hakedis_NextNumber");
        P(cmd, "@Prefix", prefix);
        return (string)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> InsertAsync(Hakedis h, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Hakedis_Insert");
        P(cmd, "@Number", h.Number);
        P(cmd, "@CreatedUtc", h.CreatedUtc);
        P(cmd, "@TedarikciId", (object?)h.TedarikciId);
        P(cmd, "@TedarikciAd", h.TedarikciAd);
        P(cmd, "@PeriodStart", h.PeriodStart);
        P(cmd, "@PeriodEnd", h.PeriodEnd);
        P(cmd, "@Note", h.Note);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task InsertLineAsync(int hakedisId, HakedisLine line, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.HakedisLine_Insert");
        P(cmd, "@HakedisId", hakedisId);
        P(cmd, "@PrinterId", (object?)line.PrinterId);
        P(cmd, "@PrinterName", line.PrinterName);
        P(cmd, "@Model", line.Model);
        P(cmd, "@TurId", (object?)line.TurId);
        P(cmd, "@TurAd", line.TurAd);
        P(cmd, "@PreviousCounter", line.PreviousCounter);
        P(cmd, "@CurrentCounter", line.CurrentCounter);
        P(cmd, "@Pages", (object?)line.Pages);
        P(cmd, "@UnitPrice", line.UnitPrice);
        P(cmd, "@Amount", line.Amount);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Hakedis_Delete");
        P(cmd, "@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<LastHakedisLineForPrinter?> GetLastLineForPrinterAsync(int printerId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.HakedisLine_GetLastForPrinter");
        P(cmd, "@PrinterId", printerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new LastHakedisLineForPrinter(GetLong(r, "CurrentCounter"), GetDateOnly(r, "PeriodEnd"));
    }
}
