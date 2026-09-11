namespace YaziciTakip.Data.Repositories;

/// <summary>Bir okumanın sayaç değeri + zamanı (yazıcı adı olmadan, ham).</summary>
public record ReadingPoint(long PageCount, DateTime TimestampUtc);

/// <summary>Bir yazıcı için okuma sayısı + en son okuma zamanı (toplu sorgu).</summary>
public record ReadingAggregate(int PrinterId, int Count, DateTime LatestUtc);

public interface IPrintReadingRepository
{
    Task InsertAsync(int printerId, long pageCount, DateTime timestampUtc, CancellationToken ct = default);

    Task<ReadingPoint?> GetLatestAsync(int printerId, CancellationToken ct = default);

    Task<List<ReadingPoint>> GetLast2Async(int printerId, CancellationToken ct = default);

    Task<ReadingPoint?> GetEarliestAsync(int printerId, CancellationToken ct = default);

    Task<int> CountByPrinterAsync(int printerId, CancellationToken ct = default);

    Task<List<ReadingPoint>> ListBeforeUtcAsync(int printerId, DateTime endUtc, CancellationToken ct = default);

    /// <summary>Tüm yazıcılar için tek sorguda (okuma yapılmışlar için) sayı + en son okuma zamanı.</summary>
    Task<Dictionary<int, ReadingAggregate>> GetAggregateAllAsync(CancellationToken ct = default);
}

public class PrintReadingRepository(AppDbContext db) : SqlRepositoryBase(db), IPrintReadingRepository
{
    private static ReadingPoint MapPoint(System.Data.Common.DbDataReader r) => new(
        Convert.ToInt64(r["PageCount"]),
        DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("TimestampUtc")), DateTimeKind.Utc));

    public async Task InsertAsync(int printerId, long pageCount, DateTime timestampUtc, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_Insert");
        P(cmd, "@PrinterId", printerId);
        P(cmd, "@PageCount", pageCount);
        P(cmd, "@TimestampUtc", timestampUtc);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ReadingPoint?> GetLatestAsync(int printerId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_GetLatest");
        P(cmd, "@PrinterId", printerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapPoint(r) : null;
    }

    public async Task<List<ReadingPoint>> GetLast2Async(int printerId, CancellationToken ct = default)
    {
        var result = new List<ReadingPoint>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_GetLast2");
        P(cmd, "@PrinterId", printerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(MapPoint(r));
        return result;
    }

    public async Task<ReadingPoint?> GetEarliestAsync(int printerId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_GetEarliest");
        P(cmd, "@PrinterId", printerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapPoint(r) : null;
    }

    public async Task<int> CountByPrinterAsync(int printerId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_CountByPrinter");
        P(cmd, "@PrinterId", printerId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<List<ReadingPoint>> ListBeforeUtcAsync(int printerId, DateTime endUtc, CancellationToken ct = default)
    {
        var result = new List<ReadingPoint>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_ListBeforeUtc");
        P(cmd, "@PrinterId", printerId);
        P(cmd, "@EndUtc", endUtc);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(MapPoint(r));
        return result;
    }

    public async Task<Dictionary<int, ReadingAggregate>> GetAggregateAllAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<int, ReadingAggregate>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.PrintReading_AggregateAll");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var printerId = GetInt(r, "PrinterId");
            result[printerId] = new ReadingAggregate(printerId, GetInt(r, "Cnt"), GetDateTimeUtc(r, "LatestUtc"));
        }
        return result;
    }
}
