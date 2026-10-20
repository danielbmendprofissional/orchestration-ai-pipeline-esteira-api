using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployEtapaEntity
    {
        public int IdFluxoDeploy { get; private set; }
        public string SglEtapa { get; private set; }
        public int? Ordem { get; private set; }
        public StatusEnum Status { get; private set; }

        private FluxoDeployEtapaEntity() { }

        internal FluxoDeployEtapaEntity(string sglEtapa, int? ordem, StatusEnum status = StatusEnum.NAO_INICIADO)
        {
            Guard.AgainstNullOrWhiteSpace(sglEtapa, nameof(sglEtapa));

            SglEtapa = sglEtapa;
            Ordem = ordem;
            Status = status;
        }

        internal void Iniciar()
        {
            Status = StatusEnum.EXECUTANDO;
        }

        internal void Concluir()
        {
            Status = StatusEnum.CONCLUIDO;
        }

        internal void Reiniciar()
        {
            if (Status == StatusEnum.CANCELADO ||
                Status == StatusEnum.EXECUTANDO)
            {
                Status = StatusEnum.NAO_INICIADO;
            }
        }

        internal void Cancelar(bool limparOrdem = true)
        {
            Status = StatusEnum.CANCELADO;

            if (limparOrdem)
                Ordem = null;
        }

        internal void AtualizarOrdem(int? ordem)
        {
            Ordem = ordem;
        }

    }


}
