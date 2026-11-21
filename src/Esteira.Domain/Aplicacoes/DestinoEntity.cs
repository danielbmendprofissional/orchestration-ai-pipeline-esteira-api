using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class DestinoEntity : EntityBase
    {
        private DestinoEntity() { }

        public long ProjetoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public string Nome { get; private set; } = default!;
        // Chave -> valor, não secreto. Persistido em jsonb. (A recusa de chave com cara de segredo é do handler, critério 10.)
        public Dictionary<string, string> Configuracao { get; private set; } = new();
        public int Ordem { get; private set; }

        public static Result<DestinoEntity> Criar(long projetoId, AmbienteDeploy ambiente, string nome, IDictionary<string, string> configuracao, int ordem)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));
            Guard.AgainstNull(configuracao, nameof(configuracao));

            if (string.IsNullOrWhiteSpace(nome))
                return new Erro("destino.nome-obrigatorio", "Informe o nome do Destino.", TipoErro.Validation);

            return Result<DestinoEntity>.Sucesso(new DestinoEntity
            {
                ProjetoId = projetoId,
                Ambiente = ambiente,
                Nome = nome.Trim(),
                Configuracao = new Dictionary<string, string>(configuracao),
                Ordem = ordem
            });
        }
    }
}
