using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PagUai.Api.Modulos.Contas.Interno.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contas");

            migrationBuilder.CreateTable(
                name: "contas",
                schema: "contas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titular = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chave_pix = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contas_chave_pix",
                schema: "contas",
                table: "contas",
                column: "chave_pix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contas_documento",
                schema: "contas",
                table: "contas",
                column: "documento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas",
                schema: "contas");
        }
    }
}
