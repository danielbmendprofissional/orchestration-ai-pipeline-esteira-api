using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class AcaoBot_P2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcaoBot",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoId = table.Column<long>(type: "bigint", nullable: false),
                    FatiaCodigo = table.Column<string>(type: "text", nullable: false),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    MrIid = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    ChaveIdempotencia = table.Column<string>(type: "text", nullable: false),
                    DependeDeAcaoId = table.Column<long>(type: "bigint", nullable: true),
                    PedidaPorPessoaId = table.Column<long>(type: "bigint", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PedidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CodigoErro = table.Column<string>(type: "text", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcaoBot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcaoBot_AcaoBot_DependeDeAcaoId",
                        column: x => x.DependeDeAcaoId,
                        principalSchema: "esteira",
                        principalTable: "AcaoBot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcaoBot_Aplicacao_AplicacaoId",
                        column: x => x.AplicacaoId,
                        principalSchema: "esteira",
                        principalTable: "Aplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcaoBot_Pessoa_PedidaPorPessoaId",
                        column: x => x.PedidaPorPessoaId,
                        principalSchema: "esteira",
                        principalTable: "Pessoa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcaoBot_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_AplicacaoId_FatiaCodigo",
                schema: "esteira",
                table: "AcaoBot",
                columns: new[] { "AplicacaoId", "FatiaCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_ChaveIdempotencia",
                schema: "esteira",
                table: "AcaoBot",
                column: "ChaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_DependeDeAcaoId",
                schema: "esteira",
                table: "AcaoBot",
                column: "DependeDeAcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_PedidaPorPessoaId",
                schema: "esteira",
                table: "AcaoBot",
                column: "PedidaPorPessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_ProjetoId",
                schema: "esteira",
                table: "AcaoBot",
                column: "ProjetoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcaoBot_PublicId",
                schema: "esteira",
                table: "AcaoBot",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcaoBot",
                schema: "esteira");
        }
    }
}
