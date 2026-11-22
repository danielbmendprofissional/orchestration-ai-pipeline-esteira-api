using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class AmbienteProjetoEntity : EntityBase
    {
        private AmbienteProjetoEntity() { }

        public long ProjetoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public bool Habilitado { get; private set; }

        public static Result<AmbienteProjetoEntity> Criar(long projetoId, AmbienteDeploy ambiente, bool habilitado = true)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));

            if (ambiente == AmbienteDeploy.Producao && !habilitado)
                return ProducaoObrigatorio();

            return Result<AmbienteProjetoEntity>.Sucesso(new AmbienteProjetoEntity
            {
                ProjetoId = projetoId,
                Ambiente = ambiente,
                Habilitado = habilitado
            });
        }

        public Result Alterar(bool habilitado)
        {
            if (Ambiente == AmbienteDeploy.Producao && !habilitado)
                return Result.Falha(ProducaoObrigatorio());

            Habilitado = habilitado;
            return Result.Sucesso();
        }

        private static Erro ProducaoObrigatorio()
            => new("ambiente.producao-obrigatorio", "O ambiente de produção não pode ser desabilitado.", TipoErro.Validation);
    }
}
