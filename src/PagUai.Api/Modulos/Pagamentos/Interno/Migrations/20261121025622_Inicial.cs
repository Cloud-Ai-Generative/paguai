using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PagUai.Api.Modulos.Pagamentos.Interno.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pagamentos");

            migrationBuilder.CreateTable(
                name: "lancamentos",
                schema: "pagamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saldos",
                schema: "pagamentos",
                columns: table => new
                {
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saldos", x => x.conta_id);
                });

            migrationBuilder.CreateTable(
                name: "transferencias",
                schema: "pagamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_pix_destino = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transferencias", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_conta_id_data",
                schema: "pagamentos",
                table: "lancamentos",
                columns: new[] { "conta_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_conta_origem_id_criada_em",
                schema: "pagamentos",
                table: "transferencias",
                columns: new[] { "conta_origem_id", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lancamentos",
                schema: "pagamentos");

            migrationBuilder.DropTable(
                name: "saldos",
                schema: "pagamentos");

            migrationBuilder.DropTable(
                name: "transferencias",
                schema: "pagamentos");
        }
    }
}
