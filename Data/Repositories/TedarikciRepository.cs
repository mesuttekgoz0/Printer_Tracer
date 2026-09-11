using YaziciTakip.Models;

namespace YaziciTakip.Data.Repositories;

/// <summary>Tedarikçi listesindeki bir satır: temel bilgi + fiyat/yazıcı sayaçları.</summary>
public record TedarikciListRow(int Id, string Ad, string? Not, int PrinterCount, int FiyatSayisi, int DetaySayisi);

/// <summary>Hakediş için tedarikçi seçim listesindeki bir satır.</summary>
public record TedarikciSecRow(int Id, string Ad, int PrinterCount, bool HasPriceList);

public interface ITedarikciRepository
{
    Task<List<TedarikciListRow>> GetAllAsync(CancellationToken ct = default);

    Task<Tedarikci?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> InsertAsync(string ad, string? not, CancellationToken ct = default);

    Task<bool> UpdateAsync(int id, string ad, string? not, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<List<TedarikciSecRow>> ListForHakedisSecimAsync(CancellationToken ct = default);
}

public class TedarikciRepository(AppDbContext db) : SqlRepositoryBase(db), ITedarikciRepository
{
    public async Task<List<TedarikciListRow>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<TedarikciListRow>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_GetAll");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new TedarikciListRow(
                GetInt(r, "Id"), GetString(r, "Ad"), GetStringN(r, "Not"),
                GetInt(r, "PrinterCount"), GetInt(r, "FiyatSayisi"), GetInt(r, "DetaySayisi")));
        return result;
    }

    public async Task<Tedarikci?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_GetById");
        P(cmd, "@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Tedarikci { Id = GetInt(r, "Id"), Ad = GetString(r, "Ad"), Not = GetStringN(r, "Not") };
    }

    public async Task<int> InsertAsync(string ad, string? not, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_Insert");
        P(cmd, "@Ad", ad);
        P(cmd, "@Not", not);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<bool> UpdateAsync(int id, string ad, string? not, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_Update");
        P(cmd, "@Id", id);
        P(cmd, "@Ad", ad);
        P(cmd, "@Not", not);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_Delete");
        P(cmd, "@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<List<TedarikciSecRow>> ListForHakedisSecimAsync(CancellationToken ct = default)
    {
        var result = new List<TedarikciSecRow>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tedarikci_ListForHakedisSecim");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new TedarikciSecRow(
                GetInt(r, "Id"), GetString(r, "Ad"), GetInt(r, "PrinterCount"), GetBool(r, "HasPriceList")));
        return result;
    }
}
