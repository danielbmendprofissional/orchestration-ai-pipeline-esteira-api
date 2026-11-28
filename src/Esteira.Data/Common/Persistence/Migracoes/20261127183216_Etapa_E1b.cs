using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Etapa_E1b : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtapaFluxo",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FluxoId = table.Column<long>(type: "bigint", nullable: false),
                    Etapa = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    PipelineGitLabId = table.Column<long>(type: "bigint", nullable: true),
                    CodigoFalha = table.Column<string>(type: "text", nullable: true),
                    PuladaPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PuladaMotivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UltimoEventoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapaFluxo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtapaFluxo_Fluxo_FluxoId",
                        column: x => x.FluxoId,
                        principalSchema: "esteira",
                        principalTable: "Fluxo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtapaFluxo_FluxoId_Etapa",
                schema: "esteira",
                table: "EtapaFluxo",
                columns: new[] { "FluxoId", "Etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtapaFluxo_PublicId",
                schema: "esteira",
                table: "EtapaFluxo",
                column: "PublicId",
                unique: true);

            // Cada Fluxo já existente ganha as 4 Etapas (abertura concluido; as outras nao-iniciado): nada é deduzido do passado.
            // xmin é coluna de sistema (token de concorrência), por isso não entra na tabela.
            migrationBuilder.Sql("""
                INSERT INTO esteira."EtapaFluxo" ("PublicId", "FluxoId", "Etapa", "Estado", "UltimoEventoEm", "AtualizadaEm")
                SELECT gen_random_uuid(), f."Id", e."Etapa",
                       CASE e."Etapa" WHEN 'abertura' THEN 'concluido' ELSE 'nao-iniciado' END,
                       en."AbertaEm", en."AbertaEm"
                FROM esteira."Fluxo" f
                JOIN esteira."Entrega" en ON en."Id" = f."EntregaId"
                CROSS JOIN (VALUES ('abertura'), ('qa'), ('hml'), ('prd')) AS e("Etapa");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtapaFluxo",
                schema: "esteira");
        }
    }
}
