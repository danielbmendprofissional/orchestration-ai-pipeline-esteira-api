using Esteira.Application.Common.Base;
using Esteira.Application.Deploys.Mappers;
using Microsoft.Extensions.Logging;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Interfaces;
using MediatR;

namespace Esteira.Application.Deploys.Commands.AtualizarAnaliseDast
{
    public class AtualizarAnaliseDastHandler
         : BaseHandler, IRequestHandler<AtualizarAnaliseDastCommand, Unit>
    {
        private static readonly string[] StatusEmAndamento = { "running", "pending", "processing" };
        private static readonly string[] StatusCancelados = { "canceled", "aborted", "stopping" };
        private static readonly string[] StatusComFalha = { "failed", "error" };

        private readonly IDeployRepository _deployRepository;

        public AtualizarAnaliseDastHandler(
            IDeployRepository deployRepository,
            ILogger<AtualizarAnaliseDastHandler> logger)
            : base(logger)
        {
            _deployRepository = deployRepository;
        }

        public async Task<Unit> Handle(
            AtualizarAnaliseDastCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (StatusEmAndamento.Contains(request.StatusScan))
                    return Unit.Value;

                var deploy = await _deployRepository.ObterPorAnaliseAsync(request.IdAnalise, cancellationToken)
                    ?? throw new NotFoundException($"Análise não encontrada: '{request.IdAnalise}'");

                var status = StatusComFalha.Contains(request.StatusScan)
                    ? StatusEnum.ERRO
                    : StatusCancelados.Contains(request.StatusScan)
                        ? StatusEnum.CANCELADO
                        : StatusEnum.CONCLUIDO;

                deploy.RegistrarResultadoAnalise(request.IdAnalise, status, request.Artefatos.ParaDadosArtefato());
            }
            catch (Exception ex)
            {
                LogErrorException(ex);
            }

            return Unit.Value;
        }
    }
}
