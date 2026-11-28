using System.Text.Json;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Data.Aplicacoes
{
    public sealed class AuditoriaEf(EsteiraContext contexto, IUsuarioAtual usuario, TimeProvider relogio) : IAuditoria
    {
        private const string NomeDoBot = "bot da Esteira";
        private const string AtorDoBot = "bot-esteira";
        private const string AtorDaPipeline = "webhook-gitlab";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // Atalho: origem `tela`, sem Aplicação, categoria deduzida da entidade (mesma tabela do backfill da migration).
        public void Registrar(string entidade, Guid entidadePublicId, AcaoAuditoria acao, object? antes, object? depois)
            => Registrar(entidade, entidadePublicId, acao, antes, depois, new DadosDoRegistro(null, CategoriaDaEntidade(entidade)));

        public void Registrar(string entidade, Guid entidadePublicId, AcaoAuditoria acao, object? antes, object? depois, DadosDoRegistro dados)
        {
            var (quem, nome) = dados.Origem switch
            {
                OrigemAuditoria.Bot => (usuario.Id ?? AtorDoBot, dados.UsuarioNome ?? NomeDoBot),
                OrigemAuditoria.Pipeline => (usuario.Id ?? AtorDaPipeline, dados.UsuarioNome ?? "pipeline"),
                _ => (usuario.Id, dados.UsuarioNome ?? usuario.Nome)
            };

            var registro = RegistroAuditoriaEntity.Criar(
                relogio.GetUtcNow().UtcDateTime,
                quem,
                entidade,
                entidadePublicId,
                acao,
                antes is null ? null : JsonSerializer.Serialize(antes, Json),
                depois is null ? null : JsonSerializer.Serialize(depois, Json),
                dados.Categoria,
                dados.Origem,
                dados.AplicacaoId,
                nome,
                dados.Verbo,
                dados.ObjetoCodigo,
                dados.ObjetoNome,
                dados.Motivo,
                dados.Autoaprovacao);

            contexto.Add(registro);
        }

        private static CategoriaAuditoria CategoriaDaEntidade(string entidade) => entidade switch
        {
            "PoliticaSeveridade" => CategoriaAuditoria.Politica,
            "PapelAplicacao" => CategoriaAuditoria.Papeis,
            "AcaoBot" => CategoriaAuditoria.Fatias,
            "Entrega" => CategoriaAuditoria.Entrega,
            _ => CategoriaAuditoria.Cadastro
        };
    }
}
