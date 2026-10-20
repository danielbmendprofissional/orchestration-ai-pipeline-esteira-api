using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APROVADOR_TECNICO",
                columns: table => new
                {
                    ID_APROVADOR_TECNICO = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IND_STATUS = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    DSC_USUARIO_APROVADOR = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DSC_EQUIPE_TECNICA = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APROVADOR_TECNICO", x => x.ID_APROVADOR_TECNICO);
                });

            migrationBuilder.CreateTable(
                name: "CONFIGURACAO_SISTEMA",
                columns: table => new
                {
                    SGL_SISTEMA = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NUM_VULNERABILIDADE_CRITICA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_ALTA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_MEDIA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_BAIXA = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONFIGURACAO_SISTEMA", x => x.SGL_SISTEMA);
                });

            migrationBuilder.CreateTable(
                name: "ETAPA",
                columns: table => new
                {
                    SGL_ETAPA = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DSC_ETAPA = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IND_STATUS = table.Column<string>(type: "text", nullable: false),
                    NUM_ORDEM = table.Column<int>(type: "integer", nullable: true),
                    IND_OBRIGATORIO = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ETAPA", x => x.SGL_ETAPA);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IND_STATUS = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    DAT_INICIO = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DAT_FIM = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SGL_SISTEMA = table.Column<string>(type: "text", nullable: false),
                    DSC_USUARIO_CRIACAO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DSC_VERSAO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY", x => x.ID_FLUXO_DEPLOY);
                });

            migrationBuilder.CreateTable(
                name: "DEPLOY",
                columns: table => new
                {
                    ID_DEPLOY = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    COD_PROJETO_GIT = table.Column<int>(type: "integer", nullable: false),
                    DSC_PROJETO_GIT = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IND_STATUS = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    DSC_BRANCH = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DSC_LINGUAGEM = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DSC_VERSAO_APLICACAO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEPLOY", x => x.ID_DEPLOY);
                    table.ForeignKey(
                        name: "FK_DEPLOY_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_APROV_NEG",
                columns: table => new
                {
                    ID_APROVADOR_NEGOCIO = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    COD_TAREFA = table.Column<int>(type: "integer", nullable: false),
                    DSC_URL_TAREFA_APROVACAO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_APROV_NEG", x => x.ID_APROVADOR_NEGOCIO);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_APROV_NEG_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_APROV_TEC",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    ID_APROVADOR_TECNICO = table.Column<int>(type: "integer", nullable: false),
                    DAT_EXECUCAO = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IND_APROVADO = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    DSC_JUSTIFICATIVA = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_APROV_TEC", x => new { x.ID_FLUXO_DEPLOY, x.ID_APROVADOR_TECNICO });
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_APROV_TEC_APROVADOR_TECNICO_ID_APROVADOR_TECNI~",
                        column: x => x.ID_APROVADOR_TECNICO,
                        principalTable: "APROVADOR_TECNICO",
                        principalColumn: "ID_APROVADOR_TECNICO",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_APROV_TEC_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_BD_CRS",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY_BD_CRS = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    DSC_CRS = table.Column<string>(type: "text", nullable: false),
                    DSC_URL_CRS = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_BD_CRS", x => x.ID_FLUXO_DEPLOY_BD_CRS);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_BD_CRS_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_ETAPA",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    SGL_ETAPA = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NUM_ORDEM = table.Column<int>(type: "integer", nullable: true),
                    IND_STATUS = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_ETAPA", x => new { x.ID_FLUXO_DEPLOY, x.SGL_ETAPA });
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_ETAPA_ETAPA_SGL_ETAPA",
                        column: x => x.SGL_ETAPA,
                        principalTable: "ETAPA",
                        principalColumn: "SGL_ETAPA",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_ETAPA_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_GMUD",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    DSC_URL_GMUD = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DSC_JUSTIFICATIVA = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DSC_USUARIO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DAT_EXECUCAO = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_GMUD", x => x.ID_FLUXO_DEPLOY);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_GMUD_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FLUXO_DEPLOY_VERSAO_TAREFAS",
                columns: table => new
                {
                    ID_FLUXO_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    COD_PROJETO = table.Column<int>(type: "integer", nullable: false),
                    COD_VERSAO = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLUXO_DEPLOY_VERSAO_TAREFAS", x => x.ID_FLUXO_DEPLOY);
                    table.ForeignKey(
                        name: "FK_FLUXO_DEPLOY_VERSAO_TAREFAS_FLUXO_DEPLOY_ID_FLUXO_DEPLOY",
                        column: x => x.ID_FLUXO_DEPLOY,
                        principalTable: "FLUXO_DEPLOY",
                        principalColumn: "ID_FLUXO_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ANALISE",
                columns: table => new
                {
                    ID_ANALISE = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ID_DEPLOY = table.Column<int>(type: "integer", nullable: false),
                    DSC_FERRAMENTA = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DSC_TIPO_ANALISE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    COD_PIPELINE = table.Column<int>(type: "integer", nullable: false),
                    DSC_TAG = table.Column<string>(type: "text", nullable: false),
                    COD_SCAN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DSC_URL_ALVO = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IND_STATUS = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    NUM_VULNERABILIDADE_CRITICA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_ALTA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_MEDIA = table.Column<int>(type: "integer", nullable: false),
                    NUM_VULNERABILIDADE_BAIXA = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ANALISE", x => x.ID_ANALISE);
                    table.ForeignKey(
                        name: "FK_ANALISE_DEPLOY_ID_DEPLOY",
                        column: x => x.ID_DEPLOY,
                        principalTable: "DEPLOY",
                        principalColumn: "ID_DEPLOY",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ARTEFATO",
                columns: table => new
                {
                    ID_ARTEFATO = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ID_ANALISE = table.Column<int>(type: "integer", nullable: false),
                    CLB_REFERENCIAS = table.Column<string>(type: "text", nullable: true),
                    IND_SEVERIDADE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DSC_LOCAL_ARQUIVO = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    IND_CWE_VULNERABILIDADE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IND_OWASP_VULNERABILIDADE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IND_CVE_VULNERABILIDADE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DSC_TITULO_VULNERABILIDADE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CLB_VULNERABILIDADE = table.Column<string>(type: "text", nullable: true),
                    CLB_PACOTE_CORRECAO = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    DSC_LINHA_VULNERABILIDADE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARTEFATO", x => x.ID_ARTEFATO);
                    table.ForeignKey(
                        name: "FK_ARTEFATO_ANALISE_ID_ANALISE",
                        column: x => x.ID_ANALISE,
                        principalTable: "ANALISE",
                        principalColumn: "ID_ANALISE",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ANALISE_ID_DEPLOY",
                table: "ANALISE",
                column: "ID_DEPLOY");

            migrationBuilder.CreateIndex(
                name: "UK_ANALISE_URL_EXECUTANDO",
                table: "ANALISE",
                column: "DSC_URL_ALVO",
                unique: true,
                filter: "\"DSC_TIPO_ANALISE\" = 'DAST' AND \"IND_STATUS\" = 'EXECUTANDO'");

            migrationBuilder.CreateIndex(
                name: "IX_ARTEFATO_ID_ANALISE",
                table: "ARTEFATO",
                column: "ID_ANALISE");

            migrationBuilder.CreateIndex(
                name: "IX_DEPLOY_ID_FLUXO_DEPLOY",
                table: "DEPLOY",
                column: "ID_FLUXO_DEPLOY");

            migrationBuilder.CreateIndex(
                name: "IX_FLUXO_DEPLOY_APROV_NEG_ID_FLUXO_DEPLOY",
                table: "FLUXO_DEPLOY_APROV_NEG",
                column: "ID_FLUXO_DEPLOY");

            migrationBuilder.CreateIndex(
                name: "IX_FLUXO_DEPLOY_APROV_TEC_ID_APROVADOR_TECNICO",
                table: "FLUXO_DEPLOY_APROV_TEC",
                column: "ID_APROVADOR_TECNICO");

            migrationBuilder.CreateIndex(
                name: "IX_FLUXO_DEPLOY_BD_CRS_ID_FLUXO_DEPLOY",
                table: "FLUXO_DEPLOY_BD_CRS",
                column: "ID_FLUXO_DEPLOY");

            migrationBuilder.CreateIndex(
                name: "IX_FLUXO_DEPLOY_ETAPA_SGL_ETAPA",
                table: "FLUXO_DEPLOY_ETAPA",
                column: "SGL_ETAPA");

            migrationBuilder.Sql(ViewResumo.Criar());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ViewResumo.Remover);

            migrationBuilder.DropTable(
                name: "ARTEFATO");

            migrationBuilder.DropTable(
                name: "CONFIGURACAO_SISTEMA");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_APROV_NEG");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_APROV_TEC");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_BD_CRS");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_ETAPA");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_GMUD");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY_VERSAO_TAREFAS");

            migrationBuilder.DropTable(
                name: "ANALISE");

            migrationBuilder.DropTable(
                name: "APROVADOR_TECNICO");

            migrationBuilder.DropTable(
                name: "ETAPA");

            migrationBuilder.DropTable(
                name: "DEPLOY");

            migrationBuilder.DropTable(
                name: "FLUXO_DEPLOY");
        }
    }
}
