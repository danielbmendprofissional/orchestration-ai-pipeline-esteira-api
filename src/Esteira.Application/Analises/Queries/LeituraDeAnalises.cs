using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Entregas.Queries.Obter;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Analises.Queries
{
    public sealed record ContagemDto(int Critica, int Alta, int Media, int Baixa);

    // `info` é guardado e mostrado, mas nunca conta para a Política: só a contagem da Análise o traz.
    public sealed record ContagemDaAnaliseDto(int Critica, int Alta, int Media, int Baixa, int Info);

    public sealed record DecisaoDaEtapaDto(string Momento, string Decisao, string? Motivo, ContagemDto Contagem, DateTime DecididaEm);

    public sealed record AnaliseDaEtapaDto(
        Guid Id, string Escopo, string Fonte, string Tipo, string Veredito, string? Destino, decimal? CustoUsd, DateTime RecebidaEm,
        ContagemDaAnaliseDto Contagem);

    public sealed record AnalisesDaEtapaDto(DecisaoDaEtapaDto? Gate, IReadOnlyList<AnaliseDaEtapaDto> Analises);

    // Texto de terceiro: devolvido como texto puro.
    public sealed record AchadoDto(string Severidade, string Titulo, string? RegraId, string? Arquivo, int? Linha, string? Descricao);

    public sealed record FluxoLocalizado(long AplicacaoId, long FluxoId);

    public interface IAnaliseLeituraQuery
    {
        // Fluxo que pertence àquela Entrega (ids públicos); nulo se Entrega ou Fluxo não existem.
        Task<FluxoLocalizado?> LocalizarFluxoAsync(Guid entregaPublicId, Guid fluxoPublicId, CancellationToken cancellationToken = default);

        // Análises da última pipeline que gravou no Ambiente (maior PipelineGitLabId) e a decisão dessa pipeline.
        Task<AnalisesDaEtapaDto> AnalisesDaEtapaAsync(long fluxoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default);

        Task<long?> LocalizarAnaliseAsync(long fluxoId, Guid analisePublicId, CancellationToken cancellationToken = default);

        // Da mais grave para a menos; empate pela ordem de gravação.
        Task<PaginaDto<AchadoDto>> ListarAchadosAsync(
            long analiseId, SeveridadeAchado? severidade, PaginacaoDto paginacao, CancellationToken cancellationToken = default);
    }

    public static class AnaliseLeituraErros
    {
        public static readonly Erro SeveridadeInvalida = new(
            "analise.severidade-invalida", "A severidade deve ser critica, alta, media, baixa ou info.", TipoErro.Validation);
    }

    public sealed record ObterAnalisesDaEtapaQuery(Guid EntregaId, Guid FluxoId, string Etapa) : IRequest<Result<AnalisesDaEtapaDto>>;

    public sealed record ListarAchadosQuery(Guid EntregaId, Guid FluxoId, Guid AnaliseId, string? Severidade, PaginacaoDto Paginacao)
        : IRequest<Result<PaginaDto<AchadoDto>>>;

    internal static class AcessoALeitura
    {
        // Mesma recusa para inexistente e sem acesso (não revela existência).
        public static async Task<Result<FluxoLocalizado>> AutorizarAsync(
            IAnaliseLeituraQuery query, IAutorizacaoAplicacao autorizacao, Guid entregaId, Guid fluxoId, CancellationToken cancellationToken)
        {
            var fluxo = await query.LocalizarFluxoAsync(entregaId, fluxoId, cancellationToken);
            if (fluxo is null)
                return EntregaErros.NaoEncontrada;

            var acesso = await autorizacao.ObterAcessoAsync(fluxo.AplicacaoId, cancellationToken);
            return acesso.Pode(OperacaoAplicacao.Ver) ? Result<FluxoLocalizado>.Sucesso(fluxo) : EntregaErros.NaoEncontrada;
        }
    }

    public sealed class ObterAnalisesDaEtapaHandler(IAnaliseLeituraQuery query, IAutorizacaoAplicacao autorizacao)
        : IRequestHandler<ObterAnalisesDaEtapaQuery, Result<AnalisesDaEtapaDto>>
    {
        public async Task<Result<AnalisesDaEtapaDto>> Handle(ObterAnalisesDaEtapaQuery request, CancellationToken cancellationToken)
        {
            AmbienteDeploy? ambiente = request.Etapa switch
            {
                "qa" => AmbienteDeploy.Qa,
                "hml" => AmbienteDeploy.Hml,
                "prd" => AmbienteDeploy.Producao,
                _ => null
            };
            if (ambiente is null)
                return EntregaErros.NaoEncontrada;

            var acesso = await AcessoALeitura.AutorizarAsync(query, autorizacao, request.EntregaId, request.FluxoId, cancellationToken);
            if (!acesso.IsSuccess)
                return Result<AnalisesDaEtapaDto>.Falha(acesso.Errors.ToArray());

            return Result<AnalisesDaEtapaDto>.Sucesso(
                await query.AnalisesDaEtapaAsync(acesso.Value.FluxoId, ambiente.Value, cancellationToken));
        }
    }

    public sealed class ListarAchadosHandler(IAnaliseLeituraQuery query, IAutorizacaoAplicacao autorizacao)
        : IRequestHandler<ListarAchadosQuery, Result<PaginaDto<AchadoDto>>>
    {
        public async Task<Result<PaginaDto<AchadoDto>>> Handle(ListarAchadosQuery request, CancellationToken cancellationToken)
        {
            SeveridadeAchado? severidade = null;
            if (!string.IsNullOrWhiteSpace(request.Severidade))
            {
                if (!EnumKebab.TentarLer<SeveridadeAchado>(request.Severidade.Trim().ToLowerInvariant(), out var lida))
                    return AnaliseLeituraErros.SeveridadeInvalida;
                severidade = lida;
            }

            var acesso = await AcessoALeitura.AutorizarAsync(query, autorizacao, request.EntregaId, request.FluxoId, cancellationToken);
            if (!acesso.IsSuccess)
                return Result<PaginaDto<AchadoDto>>.Falha(acesso.Errors.ToArray());

            var analiseId = await query.LocalizarAnaliseAsync(acesso.Value.FluxoId, request.AnaliseId, cancellationToken);
            if (analiseId is null)
                return EntregaErros.NaoEncontrada;

            return Result<PaginaDto<AchadoDto>>.Sucesso(
                await query.ListarAchadosAsync(analiseId.Value, severidade, request.Paginacao, cancellationToken));
        }
    }
}
