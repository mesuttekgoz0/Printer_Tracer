using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTurTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Sabit tür tablosu + seed (1 = Siyah-Beyaz, 2 = Renkli). Eski enum değerleri bu id'lerle aynı.
            migrationBuilder.CreateTable(
                name: "Turler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turler", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Turler",
                columns: new[] { "Id", "Ad" },
                values: new object[,]
                {
                    { 1, "Siyah-Beyaz" },
                    { 2, "Renkli" }
                });

            // 2) Printers.ColorType (enum int) -> Printers.TurId (FK). Değerleri koru (0 = belirtilmemiş = null).
            migrationBuilder.AddColumn<int>(
                name: "TurId",
                table: "Printers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Printers\" SET \"TurId\" = \"ColorType\" WHERE \"ColorType\" IN (1, 2);");

            migrationBuilder.DropColumn(
                name: "ColorType",
                table: "Printers");

            // 3) HakedisLine.ColorType (donmuş) -> TurId + TurAd (donmuş). Var olan satırları da doldur.
            migrationBuilder.AddColumn<int>(
                name: "TurId",
                table: "HakedisLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TurAd",
                table: "HakedisLines",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"HakedisLines\" SET \"TurId\" = \"ColorType\", " +
                "\"TurAd\" = CASE \"ColorType\" WHEN 1 THEN 'Siyah-Beyaz' WHEN 2 THEN 'Renkli' ELSE NULL END " +
                "WHERE \"ColorType\" IN (1, 2);");

            migrationBuilder.DropColumn(
                name: "ColorType",
                table: "HakedisLines");

            // 4) FiyatSatiri.ColorType -> TurId (rename değerleri korur).
            migrationBuilder.RenameColumn(
                name: "ColorType",
                table: "FiyatSatirlari",
                newName: "TurId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TurId",
                table: "Printers",
                column: "TurId");

            migrationBuilder.CreateIndex(
                name: "IX_FiyatSatirlari_TurId",
                table: "FiyatSatirlari",
                column: "TurId");

            migrationBuilder.AddForeignKey(
                name: "FK_FiyatSatirlari_Turler_TurId",
                table: "FiyatSatirlari",
                column: "TurId",
                principalTable: "Turler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Printers_Turler_TurId",
                table: "Printers",
                column: "TurId",
                principalTable: "Turler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FiyatSatirlari_Turler_TurId",
                table: "FiyatSatirlari");

            migrationBuilder.DropForeignKey(
                name: "FK_Printers_Turler_TurId",
                table: "Printers");

            migrationBuilder.DropTable(
                name: "Turler");

            migrationBuilder.DropIndex(
                name: "IX_Printers_TurId",
                table: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_FiyatSatirlari_TurId",
                table: "FiyatSatirlari");

            migrationBuilder.DropColumn(
                name: "TurId",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "TurAd",
                table: "HakedisLines");

            migrationBuilder.DropColumn(
                name: "TurId",
                table: "HakedisLines");

            migrationBuilder.RenameColumn(
                name: "TurId",
                table: "FiyatSatirlari",
                newName: "ColorType");

            migrationBuilder.AddColumn<int>(
                name: "ColorType",
                table: "Printers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ColorType",
                table: "HakedisLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
