using YaziciTakip.Models;

namespace YaziciTakip.Data.Repositories;

public interface IFiyatRepository
{
    Task<int?> FindMasterAsync(int tedarikciId, int turId, CancellationToken ct = default);

    Task<int> InsertMasterAsync(int tedarikciId, int turId, CancellationToken ct = default);

    Task<bool> AnyForTedarikciAsync(int tedarikciId, CancellationToken ct = default);

    /// <summary>Bir tedarikçinin tüm fiyat master'ları (tür adıyla), detaylar olmadan.</summary>
    Task<List<Fiyat>> ListByTedarikciAsync(int tedarikciId, CancellationToken ct = default);

    /// <summary>Bir tedarikçinin TÜM fiyat master'larına ait detaylar (FiyatId ile birlikte).</summary>
    Task<List<FiyatDetay>> ListDetaylarByTedarikciAsync(int tedarikciId, CancellationToken ct = default);

    Task<List<FiyatDetay>> ListDetaylarByFiyatIdAsync(int fiyatId, CancellationToken ct = default);

    Task<FiyatDetay?> GetDetayByIdAsync(int id, CancellationToken ct = default);

    Task InsertDetayAsync(int fiyatId, int turId, DateOnly bas, DateOnly bit, decimal fiyat, CancellationToken ct = default);

    Task<bool> UpdateDetayAsync(int id, DateOnly bas, DateOnly bit, decimal fiyat, CancellationToken ct = default);

    Task<bool> DeleteDetayAsync(int id, CancellationToken ct = default);

    Task<int> CountDetaylarByFiyatIdAsync(int fiyatId, CancellationToken ct = default);

    Task<bool> DeleteMasterAsync(int fiyatId, CancellationToken ct = default);
}

public class FiyatRepository(AppDbContext db) : SqlRepositoryBase(db), IFiyatRepository
{
    private static FiyatDetay MapDetay(System.Data.Common.DbDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        FiyatId = r.GetInt32(r.GetOrdinal("FiyatId")),
        TurId = r.GetInt32(r.GetOrdinal("TurId")),
        BaslangicTarihi = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("BaslangicTarihi"))),
        BitisTarihi = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("BitisTarihi"))),
        SayfaBasiFiyat = r.GetDecimal(r.GetOrdinal("SayfaBasiFiyat")),
    };

    public async Task<int?> FindMasterAsync(int tedarikciId, int turId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Fiyat_FindMaster");
        P(cmd, "@TedarikciId", tedarikciId);
        P(cmd, "@TurId", turId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? GetInt(r, "Id") : null;
    }

    public async Task<int> InsertMasterAsync(int tedarikciId, int turId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Fiyat_InsertMaster");
        P(cmd, "@TedarikciId", tedarikciId);
        P(cmd, "@TurId", turId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<bool> AnyForTedarikciAsync(int tedarikciId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Fiyat_AnyForTedarikci");
        P(cmd, "@TedarikciId", tedarikciId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<Fiyat>> ListByTedarikciAsync(int tedarikciId, CancellationToken ct = default)
    {
        var result = new List<Fiyat>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Fiyat_ListByTedarikci");
        P(cmd, "@TedarikciId", tedarikciId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new Fiyat
            {
                Id = GetInt(r, "Id"),
                TedarikciId = GetInt(r, "TedarikciId"),
                TurId = GetInt(r, "TurId"),
                Tur = new Tur { Id = GetInt(r, "TurId"), Ad = GetString(r, "TurAd") },
            });
        return result;
    }

    public async Task<List<FiyatDetay>> ListDetaylarByTedarikciAsync(int tedarikciId, CancellationToken ct = default)
    {
        var result = new List<FiyatDetay>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_ListByTedarikci");
        P(cmd, "@TedarikciId", tedarikciId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(MapDetay(r));
        return result;
    }

    public async Task<List<FiyatDetay>> ListDetaylarByFiyatIdAsync(int fiyatId, CancellationToken ct = default)
    {
        var result = new List<FiyatDetay>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_ListByFiyatId");
        P(cmd, "@FiyatId", fiyatId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(MapDetay(r));
        return result;
    }

    public async Task<FiyatDetay?> GetDetayByIdAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_GetById");
        P(cmd, "@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapDetay(r) : null;
    }

    public async Task InsertDetayAsync(int fiyatId, int turId, DateOnly bas, DateOnly bit, decimal fiyat, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_Insert");
        P(cmd, "@FiyatId", fiyatId);
        P(cmd, "@TurId", turId);
        P(cmd, "@BaslangicTarihi", bas);
        P(cmd, "@BitisTarihi", bit);
        P(cmd, "@SayfaBasiFiyat", fiyat);
        await cmd.ExecuteScalarAsync(ct);
    }

    public async Task<bool> UpdateDetayAsync(int id, DateOnly bas, DateOnly bit, decimal fiyat, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_Update");
        P(cmd, "@Id", id);
        P(cmd, "@BaslangicTarihi", bas);
        P(cmd, "@BitisTarihi", bit);
        P(cmd, "@SayfaBasiFiyat", fiyat);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> DeleteDetayAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_Delete");
        P(cmd, "@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<int> CountDetaylarByFiyatIdAsync(int fiyatId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.FiyatDetay_CountByFiyatId");
        P(cmd, "@FiyatId", fiyatId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<bool> DeleteMasterAsync(int fiyatId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Fiyat_DeleteMaster");
        P(cmd, "@Id", fiyatId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }
}
