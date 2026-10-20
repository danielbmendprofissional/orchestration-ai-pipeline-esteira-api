using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployGmudEntity
    {
        private FluxoDeployGmudEntity()
        {
        }

        public int IdFluxoDeploy { get; private set; }
        public string Url { get; private set; }
        public string Justificativa { get; private set; }
        public string Usuario { get; private set; }
        public DateTime Execucao { get; private set; }

        internal FluxoDeployGmudEntity(string urlGmud, string justificativa, string usuario, DateTime? agora)
        {
            Preencher(urlGmud, justificativa, usuario);
            Execucao = agora ?? DateTime.Now;
        }

        internal void Atualizar(
            string urlGmud,
            string justificativa,
            string usuario)
        {
            Preencher(urlGmud, justificativa, usuario);
        }

        private void Preencher(string urlGmud, string justificativa, string usuario)
        {
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));

            var possuiGmud = !string.IsNullOrWhiteSpace(urlGmud);

            Guard.Against<DomainException>(
                !possuiGmud && string.IsNullOrWhiteSpace(justificativa),
                "Justificativa é obrigatória quando uma GMUD não for informada.");

            Url = possuiGmud ? urlGmud.Trim() : null;

            Justificativa = justificativa;
            Usuario = usuario;
        }
    }
}
