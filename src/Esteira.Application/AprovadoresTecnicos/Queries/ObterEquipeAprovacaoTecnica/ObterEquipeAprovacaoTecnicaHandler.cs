using Esteira.Application.AprovadoresTecnicos.Interfaces;
using Esteira.Application.DTOs.Response;
using MediatR;

namespace Esteira.Application.AprovadoresTecnicos.Queries.ObterEquipeAprovacaoTecnica
{
    public class ObterEquipeAprovacaoTecnicaHandler
        : IRequestHandler<ObterEquipeAprovacaoTecnicaQuery, IReadOnlyList<EquipeDto>>
    {
        private readonly IAprovadorTecnicoQueryRepository _queryRepository;

        public ObterEquipeAprovacaoTecnicaHandler(IAprovadorTecnicoQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<IReadOnlyList<EquipeDto>> Handle(
            ObterEquipeAprovacaoTecnicaQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ListarIntegrantesAtivosAsync(request.Equipe, cancellationToken);
    }
}
