using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployAprovadorTecnicoEntity
    {
        private FluxoDeployAprovadorTecnicoEntity()
        {
        }

        public int IdFluxoDeploy { get; private set; }
        public int IdAprovadorTecnico { get; private set; }
        public DateTime? Execucao { get; private set; }
        public bool Aprovado { get; private set; }
        public string Justificativa { get; private set; }

        internal FluxoDeployAprovadorTecnicoEntity(int idAprovadorTecnico, bool aprovado, string justificativa, DateTime? agora)
        {
            Guard.Against<DomainException>(idAprovadorTecnico <= 0, "Aprovador técnico inválido");

            IdAprovadorTecnico = idAprovadorTecnico;
            Aprovado = aprovado;
            Justificativa = justificativa;
            Execucao = agora ?? DateTime.Now;
        }

        internal void AlterarVeredito(bool aprovado, string justificativa, DateTime? agora)
        {
            Aprovado = aprovado;
            Justificativa = justificativa;
            Execucao = agora ?? DateTime.Now;
        }
    }
}
