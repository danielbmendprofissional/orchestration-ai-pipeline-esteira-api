using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Cadastro_Aplicacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aplicacao",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Sigla = table.Column<string>(type: "text", nullable: false),
                    GrupoGitLabId = table.Column<long>(type: "bigint", nullable: false),
                    GrupoGitLabCaminho = table.Column<string>(type: "text", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aplicacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pessoa",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sub = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    UltimoLoginEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistroAuditoria",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Quando = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Usuario = table.Column<string>(type: "text", nullable: false),
                    Entidade = table.Column<string>(type: "text", nullable: false),
                    EntidadePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<string>(type: "text", nullable: false),
                    Antes = table.Column<string>(type: "jsonb", nullable: true),
                    Depois = table.Column<string>(type: "jsonb", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PapelAplicacao",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoId = table.Column<long>(type: "bigint", nullable: false),
                    Usuario = table.Column<string>(type: "text", nullable: false),
                    Papel = table.Column<string>(type: "text", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PapelAplicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PapelAplicacao_Aplicacao_AplicacaoId",
                        column: x => x.AplicacaoId,
                        principalSchema: "esteira",
                        principalTable: "Aplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoliticaSeveridade",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    Critica = table.Column<int>(type: "integer", nullable: true),
                    Alta = table.Column<int>(type: "integer", nullable: true),
                    Media = table.Column<int>(type: "integer", nullable: true),
                    Baixa = table.Column<int>(type: "integer", nullable: true),
                    Modo = table.Column<string>(type: "text", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticaSeveridade", x => x.Id);
                    table.CheckConstraint("CK_PoliticaSeveridade_LimitesNaoNegativos", "(\"Critica\" IS NULL OR \"Critica\" >= 0) AND (\"Alta\" IS NULL OR \"Alta\" >= 0) AND (\"Media\" IS NULL OR \"Media\" >= 0) AND (\"Baixa\" IS NULL OR \"Baixa\" >= 0)");
                    table.ForeignKey(
                        name: "FK_PoliticaSeveridade_Aplicacao_AplicacaoId",
                        column: x => x.AplicacaoId,
                        principalSchema: "esteira",
                        principalTable: "Aplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projeto",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AplicacaoId = table.Column<long>(type: "bigint", nullable: false),
                    ProjetoGitLabId = table.Column<long>(type: "bigint", nullable: false),
                    Caminho = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Stack = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projeto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projeto_Aplicacao_AplicacaoId",
                        column: x => x.AplicacaoId,
                        principalSchema: "esteira",
                        principalTable: "Aplicacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AmbienteProjeto",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    Habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmbienteProjeto", x => x.Id);
                    table.CheckConstraint("CK_AmbienteProjeto_ProducaoHabilitado", "\"Ambiente\" <> 'producao' OR \"Habilitado\"");
                    table.ForeignKey(
                        name: "FK_AmbienteProjeto_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DependenciaProjeto",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    DependeDeProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependenciaProjeto", x => x.Id);
                    table.CheckConstraint("CK_DependenciaProjeto_NaoPropria", "\"ProjetoId\" <> \"DependeDeProjetoId\"");
                    table.ForeignKey(
                        name: "FK_DependenciaProjeto_Projeto_DependeDeProjetoId",
                        column: x => x.DependeDeProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DependenciaProjeto_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Destino",
                schema: "esteira",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetoId = table.Column<long>(type: "bigint", nullable: false),
                    Ambiente = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Configuracao = table.Column<string>(type: "jsonb", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Destino_Projeto_ProjetoId",
                        column: x => x.ProjetoId,
                        principalSchema: "esteira",
                        principalTable: "Projeto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmbienteProjeto_ProjetoId_Ambiente",
                schema: "esteira",
                table: "AmbienteProjeto",
                columns: new[] { "ProjetoId", "Ambiente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmbienteProjeto_PublicId",
                schema: "esteira",
                table: "AmbienteProjeto",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aplicacao_GrupoGitLabId",
                schema: "esteira",
                table: "Aplicacao",
                column: "GrupoGitLabId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aplicacao_PublicId",
                schema: "esteira",
                table: "Aplicacao",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aplicacao_Sigla",
                schema: "esteira",
                table: "Aplicacao",
                column: "Sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DependenciaProjeto_DependeDeProjetoId",
                schema: "esteira",
                table: "DependenciaProjeto",
                column: "DependeDeProjetoId");

            migrationBuilder.CreateIndex(
                name: "IX_DependenciaProjeto_ProjetoId_DependeDeProjetoId",
                schema: "esteira",
                table: "DependenciaProjeto",
                columns: new[] { "ProjetoId", "DependeDeProjetoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DependenciaProjeto_PublicId",
                schema: "esteira",
                table: "DependenciaProjeto",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destino_ProjetoId_Ambiente_Nome",
                schema: "esteira",
                table: "Destino",
                columns: new[] { "ProjetoId", "Ambiente", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destino_PublicId",
                schema: "esteira",
                table: "Destino",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PapelAplicacao_AplicacaoId_Usuario_Papel",
                schema: "esteira",
                table: "PapelAplicacao",
                columns: new[] { "AplicacaoId", "Usuario", "Papel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PapelAplicacao_PublicId",
                schema: "esteira",
                table: "PapelAplicacao",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pessoa_PublicId",
                schema: "esteira",
                table: "Pessoa",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pessoa_Sub",
                schema: "esteira",
                table: "Pessoa",
                column: "Sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoliticaSeveridade_AplicacaoId_Ambiente",
                schema: "esteira",
                table: "PoliticaSeveridade",
                columns: new[] { "AplicacaoId", "Ambiente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoliticaSeveridade_PublicId",
                schema: "esteira",
                table: "PoliticaSeveridade",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projeto_AplicacaoId",
                schema: "esteira",
                table: "Projeto",
                column: "AplicacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Projeto_ProjetoGitLabId",
                schema: "esteira",
                table: "Projeto",
                column: "ProjetoGitLabId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projeto_PublicId",
                schema: "esteira",
                table: "Projeto",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_Entidade_EntidadePublicId",
                schema: "esteira",
                table: "RegistroAuditoria",
                columns: new[] { "Entidade", "EntidadePublicId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_PublicId",
                schema: "esteira",
                table: "RegistroAuditoria",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_Quando",
                schema: "esteira",
                table: "RegistroAuditoria",
                column: "Quando");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmbienteProjeto",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "DependenciaProjeto",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Destino",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "PapelAplicacao",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Pessoa",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "PoliticaSeveridade",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "RegistroAuditoria",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Projeto",
                schema: "esteira");

            migrationBuilder.DropTable(
                name: "Aplicacao",
                schema: "esteira");
        }
    }
}
