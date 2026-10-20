using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Interfaces;
using MediatR;

namespace Esteira.Application.Deploys.Commands.RegistrarScanAnalise
{
    public class RegistrarScanAnaliseHandler : IRequestHandler<RegistrarScanAnaliseCommand, Unit>
    {
        private readonly IDeployRepository _deployRepository;

        public RegistrarScanAnaliseHandler(IDeployRepository deployRepository)
        {
            _deployRepository = deployRepository;
        }

        public async Task<Unit> Handle(RegistrarScanAnaliseCommand request, CancellationToken cancellationToken)
        {
            var deploy = await _deployRepository.ObterPorAnaliseAsync(request.IdAnalise, cancellationToken)
                ?? throw new NotFoundException($"Análise não encontrada: '{request.IdAnalise}'");

            if (string.IsNullOrWhiteSpace(request.ScanId))
                deploy.FalharAnalise(request.IdAnalise);
            else
                deploy.AtribuirScanAnalise(request.IdAnalise, request.ScanId);

            return Unit.Value;
        }
    }
}
