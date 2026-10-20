using Esteira.Application.Common.Base;
using Esteira.Application.Deploys.Mappers;
using Microsoft.Extensions.Logging;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Interfaces;
using MediatR;

namespace Esteira.Application.Deploys.Commands.AdicionarArtefatosAnalise
{
    public class AdicionarArtefatosAnaliseHandler
         : BaseHandler, IRequestHandler<AdicionarArtefatosAnaliseCommand, Unit>
    {
        private readonly IDeployRepository _deployRepository;

        public AdicionarArtefatosAnaliseHandler(
            IDeployRepository deployRepository,
            ILogger<AdicionarArtefatosAnaliseHandler> logger)
            : base(logger)
        {
            _deployRepository = deployRepository;
        }

        public async Task<Unit> Handle(AdicionarArtefatosAnaliseCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var deploy = await _deployRepository.GetByIdAsync(request.DeployId, cancellationToken)
                    ?? throw new NotFoundException($"Deploy não encontrado: '{request.DeployId}'");

                deploy.AdicionarAnalise(
                    request.Ferramenta,
                    request.TipoAnalise,
                    request.IdPipeline,
                    request.Tag,
                    request.ScanId,
                    request.Status,
                    request.Artefato.ParaDadosArtefato());
            }
            catch (Exception ex)
            {
                LogErrorException(ex);
            }

            return Unit.Value;
        }
    }
}
