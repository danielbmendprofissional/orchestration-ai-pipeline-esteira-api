using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Entrega_E1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entrega",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoId = table.Column<long>(type: "bigint", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    AbertaPorPessoaId = table.Column<long>(type: "bigint", nullable: false),
                    AbertaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entrega_Aplicacao_AplicacaoId",
                        column: x => x.AplicacaoId,
                        principalSchema: "esteira",
                        principalTable: "Aplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entrega_Pessoa_AbertaPorPessoaId",
                        column: x => x.AbertaPorPessoaId,
                        principalSchema: "esteira",
                        principalTable: "Pessoa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fluxo",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntregaId = table.Column<long>(type: "bigint", nullable: false),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    TipoMudanca = table.Column<string>(type: "text", nullable: false),
                    Sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Versao = table.Column<string>(type: "text", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fluxo", x => x.Id);
                    table.CheckConstraint("CK_Fluxo_ShaHex", "\"Sha\" IS NULL OR \"Sha\" ~ '^[0-9a-f]{40}$'");
                    table.ForeignKey(
                        name: "FK_Fluxo_Entrega_EntregaId",
                        column: x => x.EntregaId,
                        principalSchema: "esteira",
                        principalTable: "Entrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Fluxo_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FluxoFatia",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FluxoId = table.Column<long>(type: "bigint", nullable: false),
                    CodigoFatia = table.Column<string>(type: "text", nullable: false),
                    ComFlag = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluxoFatia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FluxoFatia_Fluxo_FluxoId",
                        column: x => x.FluxoId,
                        principalSchema: "esteira",
                        principalTable: "Fluxo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcupacaoAmbiente",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    FluxoId = table.Column<long>(type: "bigint", nullable: false),
                    OcupadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcupacaoAmbiente", x => x.Id);
                    table.CheckConstraint("CK_OcupacaoAmbiente_SoHmlEProducao", "\"Ambiente\" IN ('hml', 'producao')");
                    table.ForeignKey(
                        name: "FK_OcupacaoAmbiente_Fluxo_FluxoId",
                        column: x => x.FluxoId,
                        principalSchema: "esteira",
                        principalTable: "Fluxo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OcupacaoAmbiente_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entrega_AbertaPorPessoaId",
                schema: "esteira",
                table: "Entrega",
                column: "AbertaPorPessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrega_AplicacaoId_Situacao",
                schema: "esteira",
                table: "Entrega",
                columns: new[] { "AplicacaoId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_Entrega_PublicId",
                schema: "esteira",
                table: "Entrega",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fluxo_EntregaId_ProjetoId",
                schema: "esteira",
                table: "Fluxo",
                columns: new[] { "EntregaId", "ProjetoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fluxo_ProjetoId",
                schema: "esteira",
                table: "Fluxo",
                column: "ProjetoId");

            migrationBuilder.CreateIndex(
                name: "IX_Fluxo_PublicId",
                schema: "esteira",
                table: "Fluxo",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FluxoFatia_FluxoId_CodigoFatia",
                schema: "esteira",
                table: "FluxoFatia",
                columns: new[] { "FluxoId", "CodigoFatia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FluxoFatia_PublicId",
                schema: "esteira",
                table: "FluxoFatia",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcupacaoAmbiente_FluxoId",
                schema: "esteira",
                table: "OcupacaoAmbiente",
                column: "FluxoId");

            migrationBuilder.CreateIndex(
                name: "IX_OcupacaoAmbiente_ProjetoId_Ambiente",
                schema: "esteira",
                table: "OcupacaoAmbiente",
                columns: new[] { "ProjetoId", "Ambiente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcupacaoAmbiente_PublicId",
                schema: "esteira",
                table: "OcupacaoAmbiente",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FluxoFatia",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "OcupacaoAmbiente",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Fluxo",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Entrega",
                schema: "esteira");
        }
    }
}
