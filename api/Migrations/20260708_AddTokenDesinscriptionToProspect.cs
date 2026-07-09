using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierClic.Api.Migrations
{
    public partial class AddTokenDesinscriptionToProspect : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenDesinscription",
                table: "Prospects",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenDesinscription",
                table: "Prospects");
        }
    }
}
