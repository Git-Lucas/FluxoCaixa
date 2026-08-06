using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FluxoCaixa.Consolidado.Api.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consolidado_diario",
                columns: table => new
                {
                    comerciante_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    total_credito = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_debito = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consolidado_diario", x => new { x.comerciante_id, x.competencia });
                });

            migrationBuilder.CreateTable(
                name: "lancamento_processado",
                columns: table => new
                {
                    lancamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comerciante_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lancamento_processado", x => x.lancamento_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lancamento_processado_processado_em",
                table: "lancamento_processado",
                column: "processado_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consolidado_diario");

            migrationBuilder.DropTable(
                name: "lancamento_processado");
        }
    }
}
