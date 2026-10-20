using Esteira.Domain.Common.Enums;
using Esteira.Domain.Etapas.Interfaces;
using MediatR;

namespace Esteira.Application.Etapas.Commands.AlterarStatusEtapa
{
    public class AlterarStatusEtapaHandler : IRequestHandler<AlterarStatusEtapaCommand, Unit>
    {
        private readonly IEtapaRepository _repository;
        private readonly IEtapaService _etapaService;

        public AlterarStatusEtapaHandler(IEtapaRepository repository, IEtapaService etapaService)
        {
            _repository = repository;
            _etapaService = etapaService;
        }

        public async Task<Unit> Handle(AlterarStatusEtapaCommand request, CancellationToken cancellationToken)
        {
            var status = Enum.Parse<StatusEnum>(request.Status.ToUpper());
            var etapa = await _repository.GetBySiglaAsync(request.Sigla.ToUpper(), cancellationToken);

            _etapaService.ValidarStatusEtapa(etapa, status);
            etapa.AtualizarStatusEtapa(status);

            return Unit.Value;
        }
    }
}
