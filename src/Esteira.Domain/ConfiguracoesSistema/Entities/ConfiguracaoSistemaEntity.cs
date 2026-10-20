using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.ConfiguracoesSistema.ValueObjects;
namespace Esteira.Domain.ConfiguracoesSistema.Entities
{
    public class ConfiguracaoSistemaEntity : IAggregateRoot
    {
        private ConfiguracaoSistemaEntity() { }

        internal ConfiguracaoSistemaEntity(string sistema, LimitesSeveridadeValueObject limites)
        {
            Guard.AgainstNullOrWhiteSpace(sistema, nameof(sistema));

            Sistema = sistema;
            Limites = limites ?? LimitesSeveridadeValueObject.Nenhum;
        }

        public string Sistema { get; private set; }
        public LimitesSeveridadeValueObject Limites { get; private set; }

        public static ConfiguracaoSistemaEntity Criar(string sistema, LimitesSeveridadeValueObject limites = null)
            => new(sistema, limites);

        public void AlterarLimites(LimitesSeveridadeValueObject limites)
        {
            Guard.AgainstNull(limites, nameof(limites));

            Limites = limites;
        }
    }
}
