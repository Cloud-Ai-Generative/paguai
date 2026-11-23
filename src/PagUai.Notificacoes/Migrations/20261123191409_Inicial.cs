using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PagUai.Notificacoes.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_processados",
                columns: table => new
                {
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_processados", x => x.evento_id);
                });

            migrationBuilder.CreateTable(
                name: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    enviada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_evento_id",
                table: "notificacoes",
                column: "evento_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_processados");

            migrationBuilder.DropTable(
                name: "notificacoes");
        }
    }
}
