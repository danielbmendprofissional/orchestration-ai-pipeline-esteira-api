using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace Esteira.Application.FluxosDeploy.Queries.ObterGmudFluxoDeploy
{
    public class ObterGmudFluxoDeployHandler : IRequestHandler<ObterGmudFluxoDeployQuery, GmudDto>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;
        private readonly GerenciadorTarefasSettings _settings;

        public ObterGmudFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository, IOptions<GerenciadorTarefasSettings> settings)
        {
            _queryRepository = queryRepository;
            _settings = settings.Value;
        }

        public async Task<GmudDto> Handle(
            ObterGmudFluxoDeployQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ObterGmudAsync(request.IdFluxoDeploy, cancellationToken)
               ?? new GmudDto { Url = _settings.UrlNovaGmud };
    }
}
