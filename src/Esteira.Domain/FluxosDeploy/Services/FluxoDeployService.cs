using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.ValueObjects;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Services
{
    public class FluxoDeployService : IFluxoDeployService
    {
        public void DeployValido(FluxoDeployEntity fluxoDeploy, string etapa)
        {
            if (fluxoDeploy == null)
                throw new DomainException("É preciso ter um fluxo de deploy criado inserir artefatos. Verifique se a versão cadastrada está condizente com a tag criada.");

            if (fluxoDeploy.Status != StatusEnum.EXECUTANDO)
                throw new DomainException($"O fluxo de deploy do sistema {fluxoDeploy.Sistema} não está válido!");

            Guard.AgainstNullOrWhiteSpace(etapa, nameof(etapa));

            var ambientesTecnicosDeploy = new[] { EtapaEnum.DEV.GetDescription(), EtapaEnum.QA.GetDescription() };
            var ambientesDeploy = new[] { EtapaEnum.DEV.GetDescription(), EtapaEnum.QA.GetDescription(), EtapaEnum.HML.GetDescription(), EtapaEnum.PRD.GetDescription() };

            if (ambientesTecnicosDeploy.Contains(etapa, StringComparer.OrdinalIgnoreCase))
            {
                if (!fluxoDeploy.AlgumaEtapaExecutando(ambientesTecnicosDeploy))
                    throw new DomainException($"Não é possível executar a ação pois nenhuma das etapas a seguir está em EXECUÇÃO: {string.Join(",", ambientesTecnicosDeploy)} ");
            }
            else if (ambientesDeploy.Contains(etapa, StringComparer.OrdinalIgnoreCase))
            {
                if (!fluxoDeploy.EtapaEstaExecutando(etapa))
                    throw new DomainException($"Não é possível executar pois a etapa {etapa} não está em EXECUÇÃO ");
            }
            else
                throw new DomainException($"Não é possível fazer deploy na etapa {etapa}");

            if (fluxoDeploy.EtapaEstaConcluida(etapa))
                throw new DomainException($"Não é possível executar pois a etapa {etapa} está CONCLUÍDA ");
        }

        public void ValidarNovoFluxo(FluxoDeployEntity fluxoDeploy)
        {
            if (fluxoDeploy == null)
                return;

            if (fluxoDeploy.StatusEtapa(EtapaEnum.HML.GetDescription()) == StatusEnum.NAO_INICIADO)
                throw new DomainException($"Não é possível criar um novo fluxo para a aplicação {fluxoDeploy.Sistema} sem que a atual esteja pelo menos em homologação");
        }

        public void ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(FluxoDeployEntity fluxoAtual, string etapaDesejada, FluxoDeployEntity fluxoAberto = null)
        {
            if (fluxoAberto == null)
                return;

            Guard.AgainstNull(fluxoAtual, nameof(fluxoAtual));

            var ambientes = new[] { EtapaEnum.HML.GetDescription(), EtapaEnum.AAN.GetDescription(), EtapaEnum.PRD.GetDescription() };

            if (fluxoAtual.AlgumaEtapaExecutando(ambientes) && !ambientes.Contains(etapaDesejada, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("A etapa não pode ser selecionada pois existe outro fluxo de deploy em execução que ainda não chegou na etapa de homologação");
        }

        public void ValidarEntradaEtapa(FluxoDeployEntity fluxoDeploy, string etapa, FluxoDeployEntity outroFluxo = null)
        {
            if (outroFluxo == null || string.IsNullOrWhiteSpace(etapa))
                return;

            Guard.AgainstNull(fluxoDeploy, nameof(fluxoDeploy));

            if (outroFluxo.EtapaEstaExecutando(etapa))
                throw new DomainException($"A etapa {etapa} já está em execução no fluxo de deploy {outroFluxo.Id}");

            var fluxoMaisNovo = fluxoDeploy.Id == 0 || fluxoDeploy.Id > outroFluxo.Id;

            if (fluxoMaisNovo && etapa.Equals(EtapaEnum.PRD.GetDescription(), StringComparison.OrdinalIgnoreCase))
                throw new DomainException($"Não é possível iniciar a etapa de PRODUÇÃO antes da conclusão do fluxo de deploy {outroFluxo.Id}");
        }

        public FluxoDeployEntity ObterFluxoAtual(IEnumerable<FluxoDeployEntity> fluxos, IEnumerable<DeployEntity> deploys, string branch, string tag, int idProjetoGit)
        {
            Guard.AgainstNull(fluxos, nameof(fluxos));
            Guard.AgainstNull(deploys, nameof(deploys));
            Guard.AgainstNullOrWhiteSpace(branch, nameof(branch));
            Guard.AgainstNullOrWhiteSpace(tag, nameof(tag));

            var listaFluxos = fluxos.ToList();

            if (listaFluxos.Count < 2)
                return listaFluxos.FirstOrDefault();

            var candidatos = listaFluxos
                .Where(a => a.Etapas.Any(
                    b => branch.Contains(b.SglEtapa, StringComparison.InvariantCultureIgnoreCase) &&
                         (b.Status == StatusEnum.NAO_INICIADO || b.Status == StatusEnum.EXECUTANDO)))
                .OrderBy(a => a.Id)
                .ToList();

            var fluxo = candidatos.FirstOrDefault(
                    a => a.Etapas.Any(
                        b => branch.Contains(b.SglEtapa, StringComparison.InvariantCultureIgnoreCase) &&
                             b.Status == StatusEnum.EXECUTANDO))
                ?? candidatos.FirstOrDefault();

            if (fluxo == null)
                return null;

            var versaoTag = VersaoAplicacaoValueObject.DaTag(tag);

            var listaDeploys = deploys.ToList();

            var deploysProjeto = listaDeploys
                .Where(a => a.IdFluxoDeploy == fluxo.Id && a.IdProjetoGit == idProjetoGit)
                .ToList();

            if (deploysProjeto.Any() && !deploysProjeto.Any(a => versaoTag.Equals(VersaoAplicacaoValueObject.Criar(a.VersaoAplicacao))))
            {
                var ultimaVersao = deploysProjeto.OrderByDescending(a => a.Id).First().VersaoAplicacao;

                throw new DomainException($"Versão aplicação do fluxo de deploy está incorreta. Neste fluxo a tag tem que iniciar com  " +
                    $"{ultimaVersao}. Tag passada: {tag.Trim().Split('.')[0]}");
            }

            var outroFluxo = listaFluxos.FirstOrDefault(x => x != fluxo);

            var versaoAplicacaoOutroFluxo = listaDeploys
                .FirstOrDefault(deploy => deploy.IdFluxoDeploy == outroFluxo?.Id && deploy.IdProjetoGit == idProjetoGit)
                ?.VersaoAplicacao;

            if (string.IsNullOrEmpty(versaoAplicacaoOutroFluxo) || deploysProjeto.Any())
                return fluxo;

            var versaoOutroFluxo = VersaoAplicacaoValueObject.Criar(versaoAplicacaoOutroFluxo);

            if (fluxo.Id > outroFluxo.Id)
            {
                if (versaoTag.EhMenorQue(versaoOutroFluxo))
                    throw new DomainException($"A versão da tag tem que ser maior que {versaoAplicacaoOutroFluxo}");
            }
            else if (!versaoTag.EhMenorQue(versaoOutroFluxo))
            {
                throw new DomainException($"A versão da tag tem que ser menor que {versaoAplicacaoOutroFluxo}");
            }

            return fluxo;
        }
    }
}
