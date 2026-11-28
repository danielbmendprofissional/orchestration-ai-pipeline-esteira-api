using System.Text;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using MediatR;

namespace Esteira.Application.Entregas.Queries.Obter
{
    public static class EntregaErros
    {
        // Mesmo erro para Entrega inexistente e sem acesso: a rota não revela existência.
        public static readonly Erro NaoEncontrada = new("entrega.nao-encontrada", "Entrega não encontrada.", TipoErro.NotFound);
    }

    // `GitLab:Url` (E1b): base para montar o link da pipeline no servidor. Nulo quando ausente.
    public interface IUrlDoGitLab
    {
        string? Base { get; }
    }

    public sealed record ProjetoDoFluxo(long Id, Guid PublicId, string Nome, string Caminho);

    // A Entrega (com Fluxos, Fatias e Etapas) e os nomes que a resposta precisa; AplicacaoId vem de Entrega.AplicacaoId.
    public sealed record EntregaParaLeitura(
        EntregaEntity Entrega, Guid AplicacaoPublicId, string AplicacaoNome, string AbertaPorNome, IReadOnlyList<ProjetoDoFluxo> Projetos);

    public interface IEntregaQuery
    {
        Task<EntregaParaLeitura?> ObterAsync(Guid entregaPublicId, CancellationToken cancellationToken = default);
    }

    public sealed record IdNomeDto(Guid Id, string Nome);

    public sealed record NomeDto(string Nome);

    public sealed record FatiaDaEntregaDto(string Codigo, bool ComFlag);

    public sealed record PuladaDto(string? Por, string? Motivo);

    public sealed record EtapaDto(string Etapa, string Estado, string? CodigoFalha, PuladaDto? Pulada, string? PipelineUrl, DateTime AtualizadaEm);

    public sealed record FluxoDaEntregaDto(
        Guid Id, IdNomeDto Projeto, string TipoMudanca, string? Sha, IReadOnlyList<FatiaDaEntregaDto> Fatias, IReadOnlyList<EtapaDto> Etapas);

    public sealed record EntregaDto(
        Guid Id, IdNomeDto Aplicacao, NomeDto AbertaPor, DateTime AbertaEm, string Estado, string? VezDe, IReadOnlyList<FluxoDaEntregaDto> Fluxos);

    public sealed record ObterEntregaQuery(Guid Id) : IRequest<Result<EntregaDto>>;

    public sealed class ObterEntregaHandler(IEntregaQuery query, IAutorizacaoAplicacao autorizacao, IUrlDoGitLab gitlab)
        : IRequestHandler<ObterEntregaQuery, Result<EntregaDto>>
    {
        public async Task<Result<EntregaDto>> Handle(ObterEntregaQuery request, CancellationToken cancellationToken)
        {
            var leitura = await query.ObterAsync(request.Id, cancellationToken);
            if (leitura is null)
                return EntregaErros.NaoEncontrada;

            var acesso = await autorizacao.ObterAcessoAsync(leitura.Entrega.AplicacaoId, cancellationToken);
            if (!acesso.Pode(OperacaoAplicacao.Ver))
                return EntregaErros.NaoEncontrada;

            var entrega = leitura.Entrega;
            var estado = entrega.Estado;
            var projetos = leitura.Projetos.ToDictionary(p => p.Id);

            var fluxos = entrega.Fluxos.Select(f =>
            {
                var projeto = projetos.GetValueOrDefault(f.ProjetoId);
                return new FluxoDaEntregaDto(
                    f.PublicId,
                    new IdNomeDto(projeto?.PublicId ?? Guid.Empty, projeto?.Nome ?? string.Empty),
                    EnumKebab.Texto(f.TipoMudanca),
                    f.Sha,
                    f.Fatias.Select(x => new FatiaDaEntregaDto(x.CodigoFatia, x.ComFlag)).ToList(),
                    f.Etapas.OrderBy(e => e.Etapa).Select(e => new EtapaDto(
                        EnumKebab.Texto(e.Etapa),
                        EnumKebab.Texto(e.Estado),
                        e.CodigoFalha,
                        e.Estado == EstadoEtapa.Pulada ? new PuladaDto(e.PuladaPor, e.PuladaMotivo) : null,
                        MontarUrl(projeto?.Caminho, e.PipelineGitLabId),
                        e.AtualizadaEm)).ToList());
            }).ToList();

            return Result<EntregaDto>.Sucesso(new EntregaDto(
                entrega.PublicId,
                new IdNomeDto(leitura.AplicacaoPublicId, leitura.AplicacaoNome),
                new NomeDto(leitura.AbertaPorNome),
                entrega.AbertaEm,
                EnumKebab.Texto(estado.Estado),
                estado.VezDe is null ? null : EnumKebab.Texto(estado.VezDe.Value),
                fluxos));
        }

        // Só o servidor monta: GitLab:Url absoluta http(s) + caminho do Projeto (cada segmento escapado) + id numérico da pipeline.
        private string? MontarUrl(string? caminho, long? pipelineId)
        {
            if (pipelineId is null || string.IsNullOrWhiteSpace(caminho))
                return null;
            if (!Uri.TryCreate(gitlab.Base, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
                return null;

            var sb = new StringBuilder(baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
            foreach (var segmento in caminho.Split('/', StringSplitOptions.RemoveEmptyEntries))
                sb.Append('/').Append(Uri.EscapeDataString(segmento));
            return sb.Append("/-/pipelines/").Append(pipelineId.Value).ToString();
        }
    }
}
