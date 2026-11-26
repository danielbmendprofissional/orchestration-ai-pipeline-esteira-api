using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class AcaoBot_P2b : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PipelineId",
                schema: "esteira",
                table: "AcaoBot",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot",
                column: "ReexecucaoDeAcaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcaoBot_AcaoBot_ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot",
                column: "ReexecucaoDeAcaoId",
                principalSchema: "esteira",
                principalTable: "AcaoBot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcaoBot_AcaoBot_ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot");

            migrationBuilder.DropIndex(
                name: "IX_AcaoBot_ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                schema: "esteira",
                table: "AcaoBot");

            migrationBuilder.DropColumn(
                name: "ReexecucaoDeAcaoId",
                schema: "esteira",
                table: "AcaoBot");
        }
    }
}
