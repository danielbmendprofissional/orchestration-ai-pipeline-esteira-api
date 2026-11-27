using System.Text.RegularExpressions;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Aplicacoes
{
    // Erros, conversões e a carga com autorização compartilhados pelos handlers de Projeto (A1.4).
    public static class ProjetoErros
    {
        public static readonly Erro NaoEncontrado = new("projeto.nao-encontrado", "Projeto não encontrado.", TipoErro.NotFound);

        public static readonly Erro ForaDoGrupo = new("projeto.fora-do-grupo", "Este projeto não pertence ao grupo do GitLab da Aplicação.", TipoErro.Failure);

        public static readonly Erro Duplicado = new("projeto.duplicado", "Este projeto já está cadastrado.", TipoErro.Conflict);

        public static readonly Erro AmbienteNaoEncontrado = new("ambiente.nao-encontrado", "Ambiente não encontrado no Projeto.", TipoErro.NotFound);

        public static readonly Erro AmbienteDesabilitado = new("ambiente.desabilitado", "O ambiente está desabilitado: habilite-o antes de cadastrar Destinos.", TipoErro.Failure);

        public static readonly Erro DestinoNaoEncontrado = new("destino.nao-encontrado", "Destino não encontrado.", TipoErro.NotFound);

        public static readonly Erro DestinoNomeDuplicado = new("destino.nome-duplicado", "Já existe um Destino com este nome neste ambiente do Projeto.", TipoErro.Conflict);

        // Corrida: o índice único do banco pegou o que a verificação prévia não viu.
        public static Erro? DaViolacao(ViolacaoDeUnicidadeException violacao)
        {
            var restricao = violacao.Restricao ?? string.Empty;
            if (restricao.Contains("ProjetoGitLabId", StringComparison.OrdinalIgnoreCase))
                return Duplicado;
            if (restricao.Contains("Destino", StringComparison.OrdinalIgnoreCase) && restricao.Contains("Nome", StringComparison.OrdinalIgnoreCase))
                return DestinoNomeDuplicado;
            return null;
        }

        // Projeto + permissão de editar o cadastro da Aplicação dele. Sem papel: 404 da Aplicação; papel insuficiente: 403.
        internal static async Task<Result<ProjetoEntity>> CarregarAutorizadoAsync(
            IProjetoRepositorio projetos, IAutorizacaoAplicacao autorizacao, Guid projetoPublicId, CancellationToken cancellationToken)
        {
            var projeto = await projetos.ObterPorPublicIdAsync(projetoPublicId, cancellationToken);
            if (projeto is null)
                return NaoEncontrado;

            var autorizado = await autorizacao.AutorizarAsync(projeto.AplicacaoId, OperacaoAplicacao.EditarCadastro, cancellationToken);
            return autorizado.IsSuccess ? Result<ProjetoEntity>.Sucesso(projeto) : Result<ProjetoEntity>.Falha(autorizado.Errors.ToArray());
        }

        public static DestinoDto Dto(DestinoEntity d) => new()
        {
            Id = d.PublicId,
            Nome = d.Nome,
            Configuracao = new Dictionary<string, string>(d.Configuracao),
            Ordem = d.Ordem
        };

        public static ProjetoResumoDto Dto(
            ProjetoEntity projeto,
            IEnumerable<AmbienteProjetoEntity> ambientes,
            IEnumerable<DestinoEntity> destinos,
            IEnumerable<Guid> dependeDe,
            Esteira.Domain.Eventos.WebhookProjetoEntity? webhook = null)
        {
            var destinosPorAmbiente = destinos.ToLookup(d => d.Ambiente);
            return new ProjetoResumoDto
            {
                Id = projeto.PublicId,
                Nome = projeto.Nome,
                Caminho = projeto.Caminho,
                Stack = EnumKebab.Texto(projeto.Stack),
                Ativo = projeto.Ativo,
                Ambientes = ambientes.OrderBy(a => a.Ambiente).Select(a => new AmbienteProjetoDto
                {
                    Id = a.PublicId,
                    Ambiente = EnumKebab.Texto(a.Ambiente),
                    Habilitado = a.Habilitado,
                    Destinos = destinosPorAmbiente[a.Ambiente].OrderBy(d => d.Ordem).ThenBy(d => d.Nome, StringComparer.Ordinal).Select(Dto).ToList()
                }).ToList(),
                DependeDe = dependeDe.ToList(),
                Webhook = webhook is null ? null : Esteira.Application.Eventos.WebhookErros.Dto(webhook)
            };
        }
    }

    // Sugestão de stack pelo `APP_TECHNOLOGY` do .gitlab-ci.yml, por regex de linha (sem pacote de YAML). Só dotnet e angular.
    public static partial class StackSugerida
    {
        [GeneratedRegex(@"^[ \t]*APP_TECHNOLOGY[ \t]*:[ \t]*[""']?([^\s""'#]+)[""']?[ \t]*(?:#.*)?\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
        private static partial Regex Linha();

        public static string? Ler(string? conteudo)
        {
            if (string.IsNullOrEmpty(conteudo))
                return null;

            var valor = Linha().Match(conteudo) is { Success: true } m ? m.Groups[1].Value.ToLowerInvariant() : null;
            return valor is "dotnet" or "angular" ? valor : null;
        }
    }
}
