using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHakedis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hakedisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hakedisler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HakedisLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HakedisId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrinterId = table.Column<int>(type: "INTEGER", nullable: true),
                    PrinterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    PreviousCounter = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentCounter = table.Column<long>(type: "INTEGER", nullable: false),
                    Pages = table.Column<long>(type: "INTEGER", nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HakedisLines");

            migrationBuilder.DropTable(
                name: "Hakedisler");
        }
    }
}
