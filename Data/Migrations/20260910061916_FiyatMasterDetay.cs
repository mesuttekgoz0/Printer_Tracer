using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class FiyatMasterDetay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fiyatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TedarikciId = table.Column<int>(type: "INTEGER", nullable: false),
                    TurId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fiyatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fiyatlar_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Fiyatlar_Turler_TurId",
                        column: x => x.TurId,
                        principalTable: "Turler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiyatDetaylari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FiyatId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BitisTarihi = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TurId = table.Column<int>(type: "INTEGER", nullable: false),
                    SayfaBasiFiyat = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiyatDetaylari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiyatDetaylari_Fiyatlar_FiyatId",
                        column: x => x.FiyatId,
                        principalTable: "Fiyatlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiyatDetaylari_Turler_TurId",
                        column: x => x.TurId,
                        principalTable: "Turler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatDetaylari_FiyatId_BaslangicTarihi",
                table: "FiyatDetaylari",
                columns: new[] { "FiyatId", "BaslangicTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatDetaylari_TurId",
                table: "FiyatDetaylari",
                column: "TurId");

            migrationBuilder.CreateIndex(
                name: "IX_Fiyatlar_TedarikciId_TurId",
                table: "Fiyatlar",
                columns: new[] { "TedarikciId", "TurId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fiyatlar_TurId",
                table: "Fiyatlar",
                column: "TurId");

            // --- Eski veri taşıma: FiyatListeleri/FiyatSatirlari -> Fiyatlar/FiyatDetaylari ---
            // Her (tedarikçi, tür) için tek master.
            migrationBuilder.Sql(@"
                INSERT INTO ""Fiyatlar"" (""TedarikciId"", ""TurId"")
                SELECT DISTINCT fl.""TedarikciId"", fs.""TurId""
                FROM ""FiyatSatirlari"" fs
                JOIN ""FiyatListeleri"" fl ON fl.""Id"" = fs.""FiyatListesiId"";");

            // Her eski satır bir detay olur; eski tek tarih -> başlangıç, +1 yıl -> bitiş (placeholder).
            migrationBuilder.Sql(@"
                INSERT INTO ""FiyatDetaylari"" (""FiyatId"", ""BaslangicTarihi"", ""BitisTarihi"", ""TurId"", ""SayfaBasiFiyat"")
                SELECT f.""Id"", fl.""Tarih"", date(fl.""Tarih"", '+1 year'), fs.""TurId"", fs.""SayfaBasiFiyat""
                FROM ""FiyatSatirlari"" fs
                JOIN ""FiyatListeleri"" fl ON fl.""Id"" = fs.""FiyatListesiId""
                JOIN ""Fiyatlar"" f ON f.""TedarikciId"" = fl.""TedarikciId"" AND f.""TurId"" = fs.""TurId"";");

            migrationBuilder.DropTable(
                name: "FiyatSatirlari");

            migrationBuilder.DropTable(
                name: "FiyatListeleri");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiyatDetaylari");

            migrationBuilder.DropTable(
                name: "Fiyatlar");

            migrationBuilder.CreateTable(
                name: "FiyatListeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TedarikciId = table.Column<int>(type: "INTEGER", nullable: false),
                    ListeAdi = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Tarih = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiyatListeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiyatListeleri_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiyatSatirlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FiyatListesiId = table.Column<int>(type: "INTEGER", nullable: false),
                    TurId = table.Column<int>(type: "INTEGER", nullable: false),
                    SayfaBasiFiyat = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiyatSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiyatSatirlari_FiyatListeleri_FiyatListesiId",
                        column: x => x.FiyatListesiId,
                        principalTable: "FiyatListeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiyatSatirlari_Turler_TurId",
                        column: x => x.TurId,
                        principalTable: "Turler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatListeleri_TedarikciId_Tarih",
                table: "FiyatListeleri",
                columns: new[] { "TedarikciId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatSatirlari_FiyatListesiId",
                table: "FiyatSatirlari",
                column: "FiyatListesiId");

            migrationBuilder.CreateIndex(
                name: "IX_FiyatSatirlari_TurId",
                table: "FiyatSatirlari",
                column: "TurId");
        }
    }
}
