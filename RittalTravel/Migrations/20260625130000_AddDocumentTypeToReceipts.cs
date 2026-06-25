using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RittalTravel.Migrations
{
    public partial class AddDocumentTypeToReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Receipts",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DocumentType", table: "Receipts");
        }
    }
}
