using YaziciTakip.Models;

namespace YaziciTakip.Data.Repositories;

/// <summary>Sabit tür (siyah-beyaz/renkli) lookup tablosu — sadece okuma (CRUD yok, seed veri).</summary>
public interface ITurRepository
{
    Task<List<Tur>> GetAllAsync(CancellationToken ct = default);

    Task<Tur?> GetByIdAsync(int id, CancellationToken ct = default);
}

public class TurRepository(AppDbContext db) : SqlRepositoryBase(db), ITurRepository
{
    public async Task<List<Tur>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<Tur>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tur_GetAll");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new Tur { Id = GetInt(r, "Id"), Ad = GetString(r, "Ad") });
        return result;
    }

    public async Task<Tur?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Tur_GetById");
        P(cmd, "@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Tur { Id = GetInt(r, "Id"), Ad = GetString(r, "Ad") };
    }
}
