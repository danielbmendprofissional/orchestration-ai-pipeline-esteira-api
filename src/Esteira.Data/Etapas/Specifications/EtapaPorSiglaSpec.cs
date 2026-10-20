using Esteira.Data.Common.Specifications;
using Esteira.Domain.Etapas.Entities;

namespace Esteira.Data.Etapas.Specifications
{
    public sealed class EtapaPorSiglaSpec : Specification<EtapaEntity>
    {
        public EtapaPorSiglaSpec(string sigla)
        {
            Criteria = p => p.Sigla == sigla;
        }
    }
}
