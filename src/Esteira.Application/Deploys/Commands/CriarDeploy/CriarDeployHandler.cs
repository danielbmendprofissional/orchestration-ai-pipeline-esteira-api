using Esteira.Application.Deploys.Mappers;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MassTransit;
using MediatR;

namespace Esteira.Application.Deploys.Commands.CriarDeploy
{
    public class CriarDeployHandler
        : IRequestHandler<CriarDeployCommand, Unit>
    {
        private readonly IPublishEndpoint _bus;
        private readonly IFluxoDeployRepository _fluxoDeployRepository;
        private readonly IDeployRepository _deployRepository;
        private readonly IFluxoDeployService _fluxoDeployService;
        private readonly IUnitOfWork _unitOfWork;

        public CriarDeployHandler(
            IPublishEndpoint bus,
            IFluxoDeployRepository fluxoDeployRepository,
            IDeployRepository deployRepository,
            IFluxoDeployService fluxoDeployService,
            IUnitOfWork unitOfWork)
        {
            _bus = bus;
            _fluxoDeployRepository = fluxoDeployRepository;
            _deployRepository = deployRepository;
            _fluxoDeployService = fluxoDeployService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(CriarDeployCommand request, CancellationToken cancellationToken)
        {
            var artefatos = request.ArtefatoDto.ParaArtefatos();
            var deploy = await ObterOuCriarDeploy(request, cancellationToken);

            if (!string.IsNullOrEmpty(request.Url))
            {
                await _bus.Publish(new CriarAnaliseArtefatosDastMessage
                {
                    DeployId = deploy.Id,
                    Ferramenta = request.FerramentaArtefatos,
                    Url = request.Url,
                    Tag = request.Tag,
                    IdPipeline = request.IdPipeline
                }, cancellationToken);
            }
            else
            {
                await _bus.Publish(new CriarAnaliseArtefatosSastMessage
                {
                    Artefato = artefatos,
                    Ferramenta = request.FerramentaArtefatos,
                    DeployId = deploy.Id,
                    Tag = request.Tag,
                    IdPipeline = request.IdPipeline
                }, cancellationToken);
            }

            return Unit.Value;
        }

        private async Task<DeployEntity> ObterOuCriarDeploy(CriarDeployCommand request, CancellationToken cancellationToken)
        {
            var sistema = StringValueObject.Normalize(request.Sistema);
            var branch = StringValueObject.Normalize(request.Branch);

            var fluxos = await _fluxoDeployRepository.ObterPorSistemaSomenteEtapasAsync(sistema, StatusEnum.EXECUTANDO, cancellationToken);
            var deploys = await _deployRepository.ObterPorFluxosAsync(fluxos.Select(f => f.Id), cancellationToken);

            var fluxo = _fluxoDeployService.ObterFluxoAtual(fluxos, deploys, branch, request.Tag, request.IdProjetoGit);
            _fluxoDeployService.DeployValido(fluxo, branch);

            var existente = deploys.FirstOrDefault(a =>
                a.IdFluxoDeploy == fluxo.Id &&
                a.Branch == branch &&
                a.IdProjetoGit == request.IdProjetoGit &&
                a.Status == StatusEnum.EXECUTANDO);

            if (existente is not null)
            {
                existente.ValidarVersaoAplicacao(request.Tag);
                return existente;
            }

            var deploy = DeployEntity.Criar(fluxo.Id, request.IdProjetoGit, request.NomeProjetoGit, StatusEnum.EXECUTANDO, branch, request.Linguagem, request.Tag);

            await _deployRepository.AddAsync(deploy, cancellationToken);
            await _unitOfWork.SaveChangesAsync();

            return deploy;
        }
    }
}
