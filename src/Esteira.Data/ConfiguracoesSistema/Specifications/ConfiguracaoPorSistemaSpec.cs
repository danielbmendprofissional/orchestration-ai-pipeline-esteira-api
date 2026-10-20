using Esteira.Data.Common.Specifications;
using Esteira.Domain.ConfiguracoesSistema.Entities;

namespace Esteira.Data.ConfiguracoesSistema.Specifications
{
    public sealed class ConfiguracaoPorSistemaSpec : Specification<ConfiguracaoSistemaEntity>
    {
        public ConfiguracaoPorSistemaSpec(string sistema)
        {
            Criteria = p => p.Sistema == sistema;
        }
    }
}
