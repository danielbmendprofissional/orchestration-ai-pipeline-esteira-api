using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Auditoria.Queries
{
    public sealed record ListarAuditoriaQuery(PaginacaoDto Paginacao, FiltroDeAuditoria Filtro)
        : IRequest<Result<PaginaDto<RegistroAuditoriaListaDto>>>;

    public sealed class ListarAuditoriaHandler(IAuditoriaQuery query)
        : IRequestHandler<ListarAuditoriaQuery, Result<PaginaDto<RegistroAuditoriaListaDto>>>
    {
        public Task<Result<PaginaDto<RegistroAuditoriaListaDto>>> Handle(ListarAuditoriaQuery request, CancellationToken cancellationToken)
            => query.ListarAsync(request.Paginacao, request.Filtro, cancellationToken);
    }

    public sealed record ContarAuditoriaPorCategoriaQuery(FiltroDeAuditoria Filtro) : IRequest<Result<IReadOnlyList<CategoriaContagemDto>>>;

    public sealed class ContarAuditoriaPorCategoriaHandler(IAuditoriaQuery query)
        : IRequestHandler<ContarAuditoriaPorCategoriaQuery, Result<IReadOnlyList<CategoriaContagemDto>>>
    {
        public async Task<Result<IReadOnlyList<CategoriaContagemDto>>> Handle(ContarAuditoriaPorCategoriaQuery request, CancellationToken cancellationToken)
            => Result<IReadOnlyList<CategoriaContagemDto>>.Sucesso(await query.ContarPorCategoriaAsync(request.Filtro, cancellationToken));
    }

    public sealed record ObterFiltrosDeAuditoriaQuery : IRequest<Result<FiltrosDeAuditoriaDto>>;

    public sealed class ObterFiltrosDeAuditoriaHandler(IAuditoriaQuery query)
        : IRequestHandler<ObterFiltrosDeAuditoriaQuery, Result<FiltrosDeAuditoriaDto>>
    {
        public async Task<Result<FiltrosDeAuditoriaDto>> Handle(ObterFiltrosDeAuditoriaQuery request, CancellationToken cancellationToken)
            => Result<FiltrosDeAuditoriaDto>.Sucesso(await query.FiltrosAsync(cancellationToken));
    }

    public sealed record ObterRegistroDeAuditoriaQuery(Guid Id) : IRequest<Result<RegistroAuditoriaDetalheDto>>;

    public sealed class ObterRegistroDeAuditoriaHandler(IAuditoriaQuery query)
        : IRequestHandler<ObterRegistroDeAuditoriaQuery, Result<RegistroAuditoriaDetalheDto>>
    {
        public static readonly Erro NaoEncontrado = new("auditoria.nao-encontrado", "Registro de auditoria não encontrado.", TipoErro.NotFound);

        public async Task<Result<RegistroAuditoriaDetalheDto>> Handle(ObterRegistroDeAuditoriaQuery request, CancellationToken cancellationToken)
        {
            var registro = await query.ObterAsync(request.Id, cancellationToken);
            return registro is null
                ? Result<RegistroAuditoriaDetalheDto>.Falha(NaoEncontrado)
                : Result<RegistroAuditoriaDetalheDto>.Sucesso(registro);
        }
    }
}
