using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Interfaces;
using MediatR;

namespace Esteira.Application.Deploys.Commands.IniciarAnaliseDinamica
{
    public class IniciarAnaliseDinamicaHandler : IRequestHandler<IniciarAnaliseDinamicaCommand, int?>
    {
        private readonly IDeployRepository _deployRepository;
        private readonly IUnitOfWork _unitOfWork;

        public IniciarAnaliseDinamicaHandler(IDeployRepository deployRepository, IUnitOfWork unitOfWork)
        {
            _deployRepository = deployRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int?> Handle(IniciarAnaliseDinamicaCommand request, CancellationToken cancellationToken)
        {
            if (await _deployRepository.ExisteAnaliseDinamicaEmExecucaoNaUrlAsync(request.Url, cancellationToken))
                return null;

            var deploy = await _deployRepository.GetByIdAsync(request.DeployId, cancellationToken)
                ?? throw new NotFoundException($"Deploy não encontrado: '{request.DeployId}'");

            var analise = deploy.IniciarAnaliseDinamica(request.Ferramenta, request.IdPipeline, request.Tag, request.Url);

            await _unitOfWork.SaveChangesAsync();

            return analise.Id;
        }
    }
}
