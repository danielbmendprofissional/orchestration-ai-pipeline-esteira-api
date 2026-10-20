using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.AprovadoresTecnicos.Entities
{
    public class AprovadorTecnicoEntity : EntityBase, IAggregateRoot
    {
        private AprovadorTecnicoEntity() { }

        internal AprovadorTecnicoEntity(string usuario, string equipe, StatusEnum status)
        {
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));

            Usuario = usuario;
            Equipe = equipe;
            Status = status;
        }

        public StatusEnum Status { get; private set; }
        public string Usuario { get; private set; }
        public string Equipe { get; private set; }

        public static AprovadorTecnicoEntity Criar(string usuario, string equipe, StatusEnum status)
            => new(usuario, equipe, status);

        public void AtualizarStatus(StatusEnum status)
        {
            Status = status;
        }
    }
}
