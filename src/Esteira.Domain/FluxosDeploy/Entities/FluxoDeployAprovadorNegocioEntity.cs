using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Common.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployAprovadorNegocioEntity : EntityBase
    {
        private FluxoDeployAprovadorNegocioEntity() { }

        public int IdFluxoDeploy { get; private set; }
        public int IdTarefa { get; private set; }
        public string Url { get; private set; }

        internal FluxoDeployAprovadorNegocioEntity(int idTarefa, string url)
        {
            Guard.Against<DomainException>(idTarefa <= 0, "Tarefa inválida");

            IdTarefa = idTarefa;
            Url = UrlValueObject.Criar(url).Valor;
        }
    }
}