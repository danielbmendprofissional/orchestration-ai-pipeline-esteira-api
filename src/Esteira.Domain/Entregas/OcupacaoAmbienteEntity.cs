using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Entregas
{
    // Ocupação de hml e produção por Projeto (ADR-0009): um Fluxo por vez. A invariante final é o índice único
    // ProjetoId+Ambiente no banco; esta entidade só decide o caso lido.
    public sealed class OcupacaoAmbienteEntity : EntityBase
    {
        private OcupacaoAmbienteEntity() { }

        public long ProjetoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public long FluxoId { get; private set; }
        public DateTime OcupadaEm { get; private set; }

        // atual: a ocupação lida do Projeto×Ambiente (nula se livre). qa não tem ocupação: devolve sucesso com valor nulo.
        // Mesmo Fluxo de novo: idempotente, devolve a própria reserva. Outro Fluxo: ambiente.ocupado.
        public static Result<OcupacaoAmbienteEntity?> Reservar(OcupacaoAmbienteEntity? atual, long projetoId, AmbienteDeploy ambiente, long fluxoId, DateTime quando)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));

            if (ambiente == AmbienteDeploy.Qa)
                return Result<OcupacaoAmbienteEntity?>.Sucesso(null);

            if (atual is not null)
                return atual.FluxoId == fluxoId
                    ? Result<OcupacaoAmbienteEntity?>.Sucesso(atual)
                    : Result<OcupacaoAmbienteEntity?>.Falha(Ocupado());

            return Result<OcupacaoAmbienteEntity?>.Sucesso(new OcupacaoAmbienteEntity
            {
                ProjetoId = projetoId,
                Ambiente = ambiente,
                FluxoId = fluxoId,
                OcupadaEm = quando
            });
        }

        // Só o Fluxo dono libera; quem chama remove a linha quando o resultado é sucesso.
        public Result Liberar(long fluxoId) => FluxoId == fluxoId ? Result.Sucesso() : Result.Falha(Ocupado());

        public static Erro Ocupado()
            => new("ambiente.ocupado", "O Ambiente do Projeto está reservado por outro Fluxo.", TipoErro.Conflict);
    }
}
