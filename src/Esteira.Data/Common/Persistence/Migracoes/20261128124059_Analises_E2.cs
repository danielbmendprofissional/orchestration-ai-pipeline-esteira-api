using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Analises_E2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analise",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FluxoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    Escopo = table.Column<string>(type: "text", nullable: false),
                    DestinoNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Fonte = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Veredito = table.Column<string>(type: "text", nullable: false),
                    CustoUsd = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    PipelineGitLabId = table.Column<long>(type: "bigint", nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analise", x => x.Id);
                    table.CheckConstraint("CK_Analise_CustoNaoNegativo", "\"CustoUsd\" IS NULL OR \"CustoUsd\" >= 0");
                    table.ForeignKey(
                        name: "FK_Analise_Fluxo_FluxoId",
                        column: x => x.FluxoId,
                        principalSchema: "esteira",
                        principalTable: "Fluxo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecisaoDoGate",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FluxoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    Momento = table.Column<string>(type: "text", nullable: false),
                    Decisao = table.Column<string>(type: "text", nullable: false),
                    Critica = table.Column<int>(type: "integer", nullable: false),
                    Alta = table.Column<int>(type: "integer", nullable: false),
                    Media = table.Column<int>(type: "integer", nullable: false),
                    Baixa = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PipelineGitLabId = table.Column<long>(type: "bigint", nullable: false),
                    DecididaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisaoDoGate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisaoDoGate_Fluxo_FluxoId",
                        column: x => x.FluxoId,
                        principalSchema: "esteira",
                        principalTable: "Fluxo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Achado",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnaliseId = table.Column<long>(type: "bigint", nullable: false),
                    Severidade = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RegraId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Linha = table.Column<int>(type: "integer", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChaveDoTipo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achado_Analise_AnaliseId",
                        column: x => x.AnaliseId,
                        principalSchema: "esteira",
                        principalTable: "Analise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achado_AnaliseId",
                schema: "esteira",
                table: "Achado",
                column: "AnaliseId");

            migrationBuilder.CreateIndex(
                name: "IX_Achado_ChaveDoTipo",
                schema: "esteira",
                table: "Achado",
                column: "ChaveDoTipo");

            migrationBuilder.CreateIndex(
                name: "IX_Achado_PublicId",
                schema: "esteira",
                table: "Achado",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Analise_FluxoId_Ambiente_Escopo_Fonte_DestinoNome_PipelineG~",
                schema: "esteira",
                table: "Analise",
                columns: new[] { "FluxoId", "Ambiente", "Escopo", "Fonte", "DestinoNome", "PipelineGitLabId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Analise_PublicId",
                schema: "esteira",
                table: "Analise",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisaoDoGate_FluxoId_Ambiente_Momento_PipelineGitLabId",
                schema: "esteira",
                table: "DecisaoDoGate",
                columns: new[] { "FluxoId", "Ambiente", "Momento", "PipelineGitLabId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisaoDoGate_PublicId",
                schema: "esteira",
                table: "DecisaoDoGate",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achado",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "DecisaoDoGate",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Analise",
                schema: "esteira");
        }
    }
}
