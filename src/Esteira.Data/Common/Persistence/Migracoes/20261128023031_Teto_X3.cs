using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Teto_X3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TetoCustoAgentesUsd",
                schema: "esteira",
                table: "Aplicacao",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Aplicacao_TetoCustoAgentesFaixa",
                schema: "esteira",
                table: "Aplicacao",
                sql: "\"TetoCustoAgentesUsd\" IS NULL OR (\"TetoCustoAgentesUsd\" >= 0.10 AND \"TetoCustoAgentesUsd\" <= 50.00)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Aplicacao_TetoCustoAgentesFaixa",
                schema: "esteira",
                table: "Aplicacao");

            migrationBuilder.DropColumn(
                name: "TetoCustoAgentesUsd",
                schema: "esteira",
                table: "Aplicacao");
        }
    }
}
