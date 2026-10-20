using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
namespace Esteira.Domain.FluxosDeploy.Services.Etapas.Base
{
    public class BaseEtapaService
    {
        protected bool ValidarEtapa(FluxoDeployEntity entity, string siglaEtapa)
        {
            if (entity.Status != StatusEnum.EXECUTANDO)
                throw new DomainException($"Fluxo de deploy {entity.Id} não está EXECUTANDO!");

            if (!entity.Etapas.Any(e => e.SglEtapa.Equals(siglaEtapa, StringComparison.OrdinalIgnoreCase) && e.Status == StatusEnum.EXECUTANDO))
                throw new DomainException($"A etapa não está em EXECUÇÃO para o fluxo de deploy {entity.Id}");

            return true;
        }

        protected void ValidarVulnerabilidadeAmbiente(
            IEnumerable<AnaliseEntity> analises,
            ConfiguracaoSistemaEntity config)
        {
            var resumo = ResumoSeveridadeValueObject.SomarTodos(analises.Select(x => x.Resumo));

            var erros = config.Limites.ObterExcessos(resumo);

            if (erros.Any())
                throw new DomainException(string.Join(Environment.NewLine, erros));
        }

        protected void ValidarDeployAmbiente(IEnumerable<AnaliseEntity> analises, string ambiente)
        {
            if(analises.Count() == 0)
                throw new DomainException($"Não há deploys válidos para o ambiente de {ambiente}");

            var analisesExecutando = analises.Where(a => a.Status == StatusEnum.EXECUTANDO).ToList();

            if (analisesExecutando.Any())
                throw new DomainException($"As seguintes análises ainda estão em execução: {string.Join(", ", analisesExecutando.Select(a => a.Ferramenta))}");
        }

    }
}
