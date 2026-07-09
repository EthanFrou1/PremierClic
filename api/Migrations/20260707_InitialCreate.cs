using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierClic.Api.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Categorie = table.Column<string>(type: "text", nullable: true),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Ville = table.Column<string>(type: "text", nullable: true),
                    CodePostal = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    SourceDonnees = table.Column<string>(type: "text", nullable: true),
                    ADejaUnSiteWeb = table.Column<bool>(type: "boolean", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateDerniereMaj = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prospects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: true),
                    Sujet = table.Column<string>(type: "text", nullable: true),
                    CorpsHtml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mockups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UrlPreview = table.Column<string>(type: "text", nullable: true),
                    Path = table.Column<string>(type: "text", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mockups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mockups_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailEnvoyes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sujet = table.Column<string>(type: "text", nullable: true),
                    CorpsHtml = table.Column<string>(type: "text", nullable: true),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    TokenDesinscription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailEnvoyes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailEnvoyes_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mockups_ProspectId",
                table: "Mockups",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailEnvoyes_ProspectId",
                table: "EmailEnvoyes",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailEnvoyes_TokenDesinscription",
                table: "EmailEnvoyes",
                column: "TokenDesinscription",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmailEnvoyes");
            migrationBuilder.DropTable(name: "Mockups");
            migrationBuilder.DropTable(name: "Users");
            migrationBuilder.DropTable(name: "EmailTemplates");
            migrationBuilder.DropTable(name: "Prospects");
        }
    }
}
