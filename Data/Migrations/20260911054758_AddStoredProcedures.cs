using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaziciTakip.Data.Migrations
{
    /// <summary>
    /// Tüm veri erişimi (CRUD dahil) saklı yordamlar üzerinden yapılır — bkz. Data/Repositories.
    /// EF Core sadece şema (bu migration'lar) ve bağlantı için kullanılır, LINQ ile sorgu/kayıt yok.
    /// <para>
    /// Prosedürlerin SQL kaynağı burada değil, <c>Data/StoredProcedures/&lt;ProsedürAdı&gt;.sql</c>
    /// dosyalarında — düz T-SQL, SSMS/Azure Data Studio'da doğrudan açılıp okunur/düzenlenir.
    /// Bu migration onları derlenmiş assembly'den (embedded resource) okuyup sırayla çalıştırır.
    /// Her CREATE PROCEDURE kendi <see cref="MigrationBuilder.Sql"/> çağrısında — T-SQL'de
    /// CREATE PROCEDURE bir batch'te tek başına olmalı.
    /// </para>
    /// </summary>
    public partial class AddStoredProcedures : Migration
    {
        /// <summary>Sıra önemli değil (prosedürler birbirini çağırmıyor) — okunabilirlik için entity gruplu.</summary>
        private static readonly string[] ProcedureNames =
        {
            "Tur_GetAll", "Tur_GetById",
            "Tedarikci_GetAll", "Tedarikci_GetById", "Tedarikci_Insert", "Tedarikci_Update",
            "Tedarikci_Delete", "Tedarikci_ListForHakedisSecim",
            "Printer_GetAll", "Printer_GetById", "Printer_ExistsByIp", "Printer_Insert",
            "Printer_UpdateName", "Printer_UpdateTur", "Printer_UpdateTedarikci", "Printer_UpdateModel",
            "Printer_Delete", "Printer_ListByTedarikci", "Printer_ListAllIpAddresses",
            "PrintReading_Insert", "PrintReading_GetLatest", "PrintReading_GetLast2",
            "PrintReading_GetEarliest", "PrintReading_CountByPrinter", "PrintReading_ListBeforeUtc",
            "PrintReading_AggregateAll",
            "Fiyat_FindMaster", "Fiyat_InsertMaster", "Fiyat_AnyForTedarikci", "Fiyat_ListByTedarikci",
            "Fiyat_DeleteMaster",
            "FiyatDetay_ListByFiyatId", "FiyatDetay_ListByTedarikci", "FiyatDetay_GetById",
            "FiyatDetay_Insert", "FiyatDetay_Update", "FiyatDetay_Delete", "FiyatDetay_CountByFiyatId",
            "Hakedis_GetAll", "Hakedis_GetById", "HakedisLine_ListByHakedis", "Hakedis_NextNumber",
            "Hakedis_Insert", "HakedisLine_Insert", "Hakedis_Delete", "HakedisLine_GetLastForPrinter",
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in ProcedureNames)
                migrationBuilder.Sql(LoadSql(name));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var drops = string.Join("\n", Array.ConvertAll(ProcedureNames, n => $"DROP PROCEDURE IF EXISTS dbo.{n};"));
            migrationBuilder.Sql(drops);
        }

        /// <summary>
        /// <c>Data/StoredProcedures/&lt;name&gt;.sql</c> dosyasını (embedded resource olarak
        /// derlenmiş) okur. Kaynak dosya <c>YaziciTakip.csproj</c>'da
        /// <c>&lt;EmbeddedResource Include="Data\StoredProcedures\*.sql" /&gt;</c> ile işaretli.
        /// </summary>
        private static string LoadSql(string name)
        {
            var resourceName = $"{typeof(AddStoredProcedures).Namespace!.Split('.')[0]}.Data.StoredProcedures.{name}.sql";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Saklı yordam SQL dosyası bulunamadı: {resourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
