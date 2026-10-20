using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployVersaoTarefasEntity
    {
        private FluxoDeployVersaoTarefasEntity() { }

        public int IdFluxoDeploy { get; private set; }
        public int IdProjeto { get; private set; }
        public int IdVersao { get; private set; }

        internal FluxoDeployVersaoTarefasEntity(int idProjeto, int idVersao)
        {
            Guard.Against<DomainException>(idProjeto <= 0, "Projeto do gerenciador de tarefas inválido");
            Guard.Against<DomainException>(idVersao <= 0, "Versão das tarefas inválida");

            IdProjeto = idProjeto;
            IdVersao = idVersao;
        }

        internal void Atualizar(
            int idProjeto,
            int idVersao)
        {
            Guard.Against<DomainException>(idProjeto <= 0, "Projeto do gerenciador de tarefas inválido");
            Guard.Against<DomainException>(idVersao <= 0, "Versão das tarefas inválida");

            IdProjeto = idProjeto;
            IdVersao = idVersao;
        }
    }
}