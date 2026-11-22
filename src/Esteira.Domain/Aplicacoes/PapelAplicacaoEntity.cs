using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class PapelAplicacaoEntity : EntityBase
    {
        private PapelAplicacaoEntity() { }

        public long AplicacaoId { get; private set; }
        // `sub` do Keycloak.
        public string Usuario { get; private set; } = default!;
        public PapelAplicacao Papel { get; private set; }

        public static PapelAplicacaoEntity Criar(long aplicacaoId, string usuario, PapelAplicacao papel)
        {
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));
            Guard.AgainstInvalidEnum<PapelAplicacao>(papel, nameof(papel));

            return new PapelAplicacaoEntity { AplicacaoId = aplicacaoId, Usuario = usuario, Papel = papel };
        }
    }
}
