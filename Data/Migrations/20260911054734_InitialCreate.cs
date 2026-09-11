using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hakedisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TedarikciId = table.Column<int>(type: "int", nullable: true),
                    TedarikciAd = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hakedisler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tedarikciler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tedarikciler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Turler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HakedisLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HakedisId = table.Column<int>(type: "int", nullable: false),
                    PrinterId = table.Column<int>(type: "int", nullable: true),
                    PrinterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TurId = table.Column<int>(type: "int", nullable: true),
                    TurAd = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreviousCounter = table.Column<long>(type: "bigint", nullable: false),
                    CurrentCounter = table.Column<long>(type: "bigint", nullable: false),
                    Pages = table.Column<long>(type: "bigint", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HakedisLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HakedisLines_Hakedisler_HakedisId",
                        column: x => x.HakedisId,
                        principalTable: "Hakedisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fiyatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TedarikciId = table.Column<int>(type: "int", nullable: false),
                    TurId = table.Column<int>(type: "int", nullable: false)
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
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TurId = table.Column<int>(type: "int", nullable: true),
                    TedarikciId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printers_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Printers_Turler_TurId",
                        column: x => x.TurId,
                        principalTable: "Turler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FiyatDetaylari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiyatId = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    BitisTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    TurId = table.Column<int>(type: "int", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PrintReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrinterId = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintReadings_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Turler",
                columns: new[] { "Id", "Ad" },
                values: new object[,]
                {
                    { 1, "Siyah-Beyaz" },
                    { 2, "Renkli" }
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

            migrationBuilder.CreateIndex(
                name: "IX_Hakedisler_Number",
                table: "Hakedisler",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HakedisLines_HakedisId",
                table: "HakedisLines",
                column: "HakedisId");

            migrationBuilder.CreateIndex(
                name: "IX_HakedisLines_PrinterId",
                table: "HakedisLines",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_IpAddress",
                table: "Printers",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TedarikciId",
                table: "Printers",
                column: "TedarikciId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TurId",
                table: "Printers",
                column: "TurId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintReadings_PrinterId_TimestampUtc",
                table: "PrintReadings",
                columns: new[] { "PrinterId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiyatDetaylari");

            migrationBuilder.DropTable(
                name: "HakedisLines");

            migrationBuilder.DropTable(
                name: "PrintReadings");

            migrationBuilder.DropTable(
                name: "Fiyatlar");

            migrationBuilder.DropTable(
                name: "Hakedisler");

            migrationBuilder.DropTable(
                name: "Printers");

            migrationBuilder.DropTable(
                name: "Tedarikciler");

            migrationBuilder.DropTable(
                name: "Turler");
        }
    }
}
