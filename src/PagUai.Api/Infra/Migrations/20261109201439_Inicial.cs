using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PagUai.Api.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analises_antifraude",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    analisada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analises_antifraude", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cobrancas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_recebedora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paga_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cobrancas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titular = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chave_pix = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lancamentos",
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
                name: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    enviada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "ix_contas_chave_pix",
                table: "contas",
                column: "chave_pix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contas_documento",
                table: "contas",
                column: "documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_conta_id_data",
                table: "lancamentos",
                columns: new[] { "conta_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_conta_origem_id_criada_em",
                table: "transferencias",
                columns: new[] { "conta_origem_id", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analises_antifraude");

            migrationBuilder.DropTable(
                name: "cobrancas");

            migrationBuilder.DropTable(
                name: "contas");

            migrationBuilder.DropTable(
                name: "lancamentos");

            migrationBuilder.DropTable(
                name: "notificacoes");

            migrationBuilder.DropTable(
                name: "transferencias");
        }
    }
}
