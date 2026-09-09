using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTedarikciAndPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TedarikciId",
                table: "Printers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "HakedisLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ColorType",
                table: "HakedisLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "HakedisLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TedarikciAd",
                table: "Hakedisler",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TedarikciId",
                table: "Hakedisler",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tedarikciler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Not = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tedarikciler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiyatListeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListeAdi = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Tarih = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TedarikciId = table.Column<int>(type: "INTEGER", nullable: false)
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
                    ColorType = table.Column<int>(type: "INTEGER", nullable: false),
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TedarikciId",
                table: "Printers",
                column: "TedarikciId");

            migrationBuilder.CreateIndex(
                name: "IX_FiyatListeleri_TedarikciId_Tarih",
                table: "FiyatListeleri",
                columns: new[] { "TedarikciId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatSatirlari_FiyatListesiId",
                table: "FiyatSatirlari",
                column: "FiyatListesiId");

            migrationBuilder.AddForeignKey(
                name: "FK_Printers_Tedarikciler_TedarikciId",
                table: "Printers",
                column: "TedarikciId",
                principalTable: "Tedarikciler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Printers_Tedarikciler_TedarikciId",
                table: "Printers");

            migrationBuilder.DropTable(
                name: "FiyatSatirlari");

            migrationBuilder.DropTable(
                name: "FiyatListeleri");

            migrationBuilder.DropTable(
                name: "Tedarikciler");

            migrationBuilder.DropIndex(
                name: "IX_Printers_TedarikciId",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "TedarikciId",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "HakedisLines");

            migrationBuilder.DropColumn(
                name: "ColorType",
                table: "HakedisLines");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "HakedisLines");

            migrationBuilder.DropColumn(
                name: "TedarikciAd",
                table: "Hakedisler");

            migrationBuilder.DropColumn(
                name: "TedarikciId",
                table: "Hakedisler");
        }
    }
}
