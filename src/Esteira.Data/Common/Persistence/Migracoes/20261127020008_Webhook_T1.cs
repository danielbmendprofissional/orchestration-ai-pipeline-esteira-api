using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Webhook_T1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookProjeto",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    GitLabHookId = table.Column<long>(type: "bigint", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    CodigoErro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookProjeto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookProjeto_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookProjeto_ProjetoId",
                schema: "esteira",
                table: "WebhookProjeto",
                column: "ProjetoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookProjeto_PublicId",
                schema: "esteira",
                table: "WebhookProjeto",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookProjeto",
                schema: "esteira");
        }
    }
}
