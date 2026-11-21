using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PagUai.Api.Modulos.Antifraude.Interno.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "antifraude");

            migrationBuilder.CreateTable(
                name: "analises",
                schema: "antifraude",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pagamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    analisada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analises", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analises_conta_origem_id_analisada_em",
                schema: "antifraude",
                table: "analises",
                columns: new[] { "conta_origem_id", "analisada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analises",
                schema: "antifraude");
        }
    }
}
