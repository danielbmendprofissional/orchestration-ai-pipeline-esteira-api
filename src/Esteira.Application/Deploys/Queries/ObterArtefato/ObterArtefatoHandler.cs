using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.DTOs.Response;
using MediatR;

namespace Esteira.Application.Deploys.Queries.ObterArtefato
{
    public class ObterArtefatoHandler
        : IRequestHandler<ObterArtefatoQuery, ArtefatoCompletoDto>
    {
        private readonly IDeployQueryRepository _queryRepository;

        public ObterArtefatoHandler(IDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<ArtefatoCompletoDto> Handle(
            ObterArtefatoQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ObterArtefatoAsync(request.Id, cancellationToken);
    }
}
