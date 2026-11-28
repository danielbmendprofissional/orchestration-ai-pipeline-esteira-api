using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esteira.Data.Common.Persistence.Migracoes
{
    /// <inheritdoc />
    public partial class Auditoria_X1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AplicacaoId",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Autoaprovacao",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: false,
                defaultValue: "cadastro");

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjetoCodigo",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjetoNome",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: false,
                defaultValue: "tela");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioNome",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verbo",
                schema: "esteira",
                table: "RegistroAuditoria",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_AplicacaoId_Quando",
                schema: "esteira",
                table: "RegistroAuditoria",
                columns: new[] { "AplicacaoId", "Quando" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAuditoria_ObjetoCodigo",
                schema: "esteira",
                table: "RegistroAuditoria",
                column: "ObjetoCodigo");

            migrationBuilder.AddForeignKey(
                name: "FK_RegistroAuditoria_Aplicacao_AplicacaoId",
                schema: "esteira",
                table: "RegistroAuditoria",
                column: "AplicacaoId",
                principalSchema: "esteira",
                principalTable: "Aplicacao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill (X1): preenche os registros existentes por entidade, antes da trava de imutabilidade.
            migrationBuilder.Sql(@"
UPDATE esteira.""RegistroAuditoria"" r SET
  ""AplicacaoId"" = CASE r.""Entidade""
    WHEN 'Aplicacao' THEN (SELECT x.""Id"" FROM esteira.""Aplicacao"" x WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'Projeto' THEN (SELECT x.""AplicacaoId"" FROM esteira.""Projeto"" x WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'PoliticaSeveridade' THEN (SELECT x.""AplicacaoId"" FROM esteira.""PoliticaSeveridade"" x WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'PapelAplicacao' THEN (SELECT x.""AplicacaoId"" FROM esteira.""PapelAplicacao"" x WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'AcaoBot' THEN (SELECT x.""AplicacaoId"" FROM esteira.""AcaoBot"" x WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'AmbienteProjeto' THEN (SELECT p.""AplicacaoId"" FROM esteira.""AmbienteProjeto"" x JOIN esteira.""Projeto"" p ON p.""Id"" = x.""ProjetoId"" WHERE x.""PublicId"" = r.""EntidadePublicId"")
    WHEN 'Destino' THEN (SELECT p.""AplicacaoId"" FROM esteira.""Destino"" x JOIN esteira.""Projeto"" p ON p.""Id"" = x.""ProjetoId"" WHERE x.""PublicId"" = r.""EntidadePublicId"")
    ELSE NULL END,
  ""Categoria"" = CASE r.""Entidade""
    WHEN 'PoliticaSeveridade' THEN 'politica'
    WHEN 'PapelAplicacao' THEN 'papeis'
    WHEN 'AcaoBot' THEN 'fatias'
    ELSE 'cadastro' END,
  ""Origem"" = CASE WHEN r.""Usuario"" = 'webhook-gitlab' THEN 'pipeline' ELSE 'tela' END,
  ""UsuarioNome"" = COALESCE((SELECT pe.""Nome"" FROM esteira.""Pessoa"" pe WHERE pe.""Sub"" = r.""Usuario""), r.""Usuario""),
  ""ObjetoCodigo"" = CASE WHEN r.""Entidade"" = 'AcaoBot'
    THEN (SELECT x.""FatiaCodigo"" FROM esteira.""AcaoBot"" x WHERE x.""PublicId"" = r.""EntidadePublicId"") END;");

            // Categoria não tem padrão: quem grava sempre informa (o padrão só serviu ao backfill).
            migrationBuilder.Sql(@"ALTER TABLE esteira.""RegistroAuditoria"" ALTER COLUMN ""Categoria"" DROP DEFAULT;");

            // Trava de imutabilidade (defesa em profundidade): criada DEPOIS do backfill.
            migrationBuilder.Sql(@"
CREATE FUNCTION esteira.registro_auditoria_imutavel() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'RegistroAuditoria é imutável: % recusado', TG_OP USING ERRCODE = 'restrict_violation';
END;
$$;
CREATE TRIGGER trg_registro_auditoria_imutavel
  BEFORE UPDATE OR DELETE ON esteira.""RegistroAuditoria""
  FOR EACH ROW EXECUTE FUNCTION esteira.registro_auditoria_imutavel();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A trava sai antes de desfazer as colunas.
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_registro_auditoria_imutavel ON esteira.""RegistroAuditoria"";
DROP FUNCTION IF EXISTS esteira.registro_auditoria_imutavel();");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistroAuditoria_Aplicacao_AplicacaoId",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_RegistroAuditoria_AplicacaoId_Quando",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_RegistroAuditoria_ObjetoCodigo",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "AplicacaoId",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "Autoaprovacao",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "Categoria",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "Motivo",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "ObjetoCodigo",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "ObjetoNome",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "Origem",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "UsuarioNome",
                schema: "esteira",
                table: "RegistroAuditoria");

            migrationBuilder.DropColumn(
                name: "Verbo",
                schema: "esteira",
                table: "RegistroAuditoria");
        }
    }
}
