using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierClic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGooglePlaceIdToProspect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GooglePlaceId",
                table: "Prospects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GooglePlaceId",
                table: "Prospects");
        }
    }
}
