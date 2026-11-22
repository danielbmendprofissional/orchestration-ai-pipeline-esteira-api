using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Workspace_Da_Aplicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkspaceGitLabCaminho",
                schema: "esteira",
                table: "Aplicacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkspaceGitLabId",
                schema: "esteira",
                table: "Aplicacao",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Aplicacao_WorkspaceCoerente",
                schema: "esteira",
                table: "Aplicacao",
                sql: "(\"WorkspaceGitLabId\" IS NULL AND \"WorkspaceGitLabCaminho\" IS NULL) OR (\"WorkspaceGitLabId\" IS NOT NULL AND \"WorkspaceGitLabCaminho\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Aplicacao_WorkspaceCoerente",
                schema: "esteira",
                table: "Aplicacao");

            migrationBuilder.DropColumn(
                name: "WorkspaceGitLabCaminho",
                schema: "esteira",
                table: "Aplicacao");

            migrationBuilder.DropColumn(
                name: "WorkspaceGitLabId",
                schema: "esteira",
                table: "Aplicacao");
        }
    }
}
