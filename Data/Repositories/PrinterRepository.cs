using Microsoft.Data.SqlClient;
using YaziciTakip.Models;

namespace YaziciTakip.Data.Repositories;

/// <summary>IpAddress unique kısıtı ihlal edildiğinde fırlatılır (SQL error 2601/2627).</summary>
public class DuplicateIpAddressException(string ipAddress) : Exception($"\"{ipAddress}\" zaten kayıtlı bir yazıcı.")
{
    public string IpAddress { get; } = ipAddress;
}

public interface IPrinterRepository
{
    Task<List<Printer>> GetAllAsync(CancellationToken ct = default);

    Task<Printer?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsByIpAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>Yeni yazıcı ekler, yeni Id'yi döner. IP zaten kayıtlıysa <see cref="DuplicateIpAddressException"/>.</summary>
    Task<int> InsertAsync(string name, string ipAddress, int? turId, int? tedarikciId, CancellationToken ct = default);

    Task<bool> UpdateNameAsync(int id, string name, CancellationToken ct = default);

    Task<bool> UpdateTurAsync(int id, int? turId, CancellationToken ct = default);

    Task<bool> UpdateTedarikciAsync(int id, int? tedarikciId, CancellationToken ct = default);

    Task<bool> UpdateModelAsync(int id, string? model, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<List<Printer>> ListByTedarikciAsync(int tedarikciId, CancellationToken ct = default);

    Task<List<string>> ListAllIpAddressesAsync(CancellationToken ct = default);
}

public class PrinterRepository(AppDbContext db) : SqlRepositoryBase(db), IPrinterRepository
{
    private static Printer Map(System.Data.Common.DbDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        IpAddress = r.GetString(r.GetOrdinal("IpAddress")),
        Model = r.IsDBNull(r.GetOrdinal("Model")) ? null : r.GetString(r.GetOrdinal("Model")),
        TurId = r.IsDBNull(r.GetOrdinal("TurId")) ? null : r.GetInt32(r.GetOrdinal("TurId")),
        Tur = r.IsDBNull(r.GetOrdinal("TurId"))
            ? null
            : new Tur { Id = r.GetInt32(r.GetOrdinal("TurId")), Ad = r.GetString(r.GetOrdinal("TurAd")) },
        TedarikciId = r.IsDBNull(r.GetOrdinal("TedarikciId")) ? null : r.GetInt32(r.GetOrdinal("TedarikciId")),
        Tedarikci = r.IsDBNull(r.GetOrdinal("TedarikciId"))
            ? null
            : new Tedarikci { Id = r.GetInt32(r.GetOrdinal("TedarikciId")), Ad = r.GetString(r.GetOrdinal("TedarikciAd")) },
    };

    public async Task<List<Printer>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<Printer>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_GetAll");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(Map(r));
        return result;
    }

    public async Task<Printer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_GetById");
        P(cmd, "@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<bool> ExistsByIpAsync(string ipAddress, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_ExistsByIp");
        P(cmd, "@IpAddress", ipAddress);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> InsertAsync(string name, string ipAddress, int? turId, int? tedarikciId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_Insert");
        P(cmd, "@Name", name);
        P(cmd, "@IpAddress", ipAddress);
        P(cmd, "@TurId", (object?)turId);
        P(cmd, "@TedarikciId", (object?)tedarikciId);
        try
        {
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateIpAddressException(ipAddress);
        }
    }

    public async Task<bool> UpdateNameAsync(int id, string name, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_UpdateName");
        P(cmd, "@Id", id);
        P(cmd, "@Name", name);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> UpdateTurAsync(int id, int? turId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_UpdateTur");
        P(cmd, "@Id", id);
        P(cmd, "@TurId", (object?)turId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> UpdateTedarikciAsync(int id, int? tedarikciId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_UpdateTedarikci");
        P(cmd, "@Id", id);
        P(cmd, "@TedarikciId", (object?)tedarikciId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> UpdateModelAsync(int id, string? model, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_UpdateModel");
        P(cmd, "@Id", id);
        P(cmd, "@Model", model);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_Delete");
        P(cmd, "@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<List<Printer>> ListByTedarikciAsync(int tedarikciId, CancellationToken ct = default)
    {
        var result = new List<Printer>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_ListByTedarikci");
        P(cmd, "@TedarikciId", tedarikciId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(Map(r));
        return result;
    }

    public async Task<List<string>> ListAllIpAddressesAsync(CancellationToken ct = default)
    {
        var result = new List<string>();
        var conn = await OpenAsync(ct);
        await using var cmd = Proc(conn, "dbo.Printer_ListAllIpAddresses");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(r.GetString(0));
        return result;
    }
}
