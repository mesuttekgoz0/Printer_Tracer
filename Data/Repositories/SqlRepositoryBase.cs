using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace YaziciTakip.Data.Repositories;

/// <summary>
/// Saklı yordam tabanlı repository'ler için ortak ADO.NET yardımcıları.
/// <para>
/// Bağlantı <see cref="AppDbContext"/> üzerinden alınır — EF Core burada YALNIZCA bağlantıyı
/// (connection string / lifetime) sağlar. Sorgu ve değişiklikler LINQ ile değil, doğrudan
/// <see cref="SqlCommand"/> + <see cref="CommandType.StoredProcedure"/> ile yapılır.
/// </para>
/// </summary>
public abstract class SqlRepositoryBase(AppDbContext db)
{
    protected async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = (SqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }

    protected static SqlCommand Proc(SqlConnection conn, string name)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = name;
        return cmd;
    }

    protected static void P(SqlCommand cmd, string name, object? value) =>
        cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

    protected static void P(SqlCommand cmd, string name, DateOnly? value) =>
        cmd.Parameters.AddWithValue(name, value is { } d ? d.ToDateTime(TimeOnly.MinValue) : DBNull.Value);

    protected static void P(SqlCommand cmd, string name, DateOnly value) =>
        cmd.Parameters.AddWithValue(name, value.ToDateTime(TimeOnly.MinValue));

    // ---- okuma yardımcıları (SqlDataReader) ----

    protected static int GetInt(SqlDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));

    protected static int? GetIntN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    protected static long GetLong(SqlDataReader r, string col) => Convert.ToInt64(r[r.GetOrdinal(col)]);

    protected static long? GetLongN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToInt64(r[i]);
    }

    protected static string GetString(SqlDataReader r, string col) => r.GetString(r.GetOrdinal(col));

    protected static string? GetStringN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    protected static decimal GetDecimal(SqlDataReader r, string col) => r.GetDecimal(r.GetOrdinal(col));

    protected static bool GetBool(SqlDataReader r, string col) => r.GetBoolean(r.GetOrdinal(col));

    /// <summary>DATETIME2 sütunu her zaman UTC saklanır; Kind burada UTC olarak işaretlenir.</summary>
    protected static DateTime GetDateTimeUtc(SqlDataReader r, string col) =>
        DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(col)), DateTimeKind.Utc);

    protected static DateTime? GetDateTimeUtcN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
    }

    protected static DateOnly GetDateOnly(SqlDataReader r, string col) =>
        DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal(col)));

    protected static DateOnly? GetDateOnlyN(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i));
    }
}
