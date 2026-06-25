using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RittalTravel.Migrations
{
    public partial class AddReceiptFileName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFileName",
                table: "Trips",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptFileName",
                table: "Trips");
        }
    }
}
