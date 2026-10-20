using Esteira.Application.DTOs.Response;
using Esteira.Application.Etapas.Interfaces;
using MediatR;

namespace Esteira.Application.Etapas.Queries.ObterListaEtapa
{
    public class ObterListaEtapaHandler
        : IRequestHandler<ObterListaEtapaQuery, IEnumerable<EtapaDto>>
    {
        private readonly IEtapaQueryRepository _queryRepository;

        public ObterListaEtapaHandler(IEtapaQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<IEnumerable<EtapaDto>> Handle(
            ObterListaEtapaQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ListarAtivasAsync(cancellationToken);
    }
}
