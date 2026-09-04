using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaziciTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Printers",
                type: "TEXT",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Model",
                table: "Printers");
        }
    }
}
