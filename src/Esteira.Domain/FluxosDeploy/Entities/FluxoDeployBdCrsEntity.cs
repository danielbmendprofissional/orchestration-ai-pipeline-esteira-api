using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Common.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployBdCrsEntity : EntityBase
    {
        private FluxoDeployBdCrsEntity() { }

        public int IdFluxoDeploy { get; private set; }
        public string Crs { get; private set; }
        public string Url { get; private set; }

        internal FluxoDeployBdCrsEntity(string crs, string url)
        {
            Guard.AgainstNullOrWhiteSpace(crs, nameof(crs));
            Guard.AgainstNullOrWhiteSpace(url, nameof(url));

            Crs = crs;
            Url = UrlValueObject.Criar(url).Valor;
        }
    }
}