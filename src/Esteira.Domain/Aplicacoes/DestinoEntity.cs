using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class DestinoEntity : EntityBase
    {
        private static readonly string[] IndiciosDeSegredo = ["PASSWORD", "SENHA", "SECRET", "TOKEN", "KEY", "CREDENTIAL"];

        private DestinoEntity() { }

        public long ProjetoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public string Nome { get; private set; } = default!;
        // Chave -> valor, não secreto (critério 10: chave com cara de segredo é recusada). Persistido em jsonb.
        public Dictionary<string, string> Configuracao { get; private set; } = new();
        public int Ordem { get; private set; }

        public static Result<DestinoEntity> Criar(long projetoId, AmbienteDeploy ambiente, string nome, IDictionary<string, string> configuracao, int ordem)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));
            Guard.AgainstNull(configuracao, nameof(configuracao));

            var erro = Validar(nome, configuracao);
            if (erro is not null)
                return erro;

            return Result<DestinoEntity>.Sucesso(new DestinoEntity
            {
                ProjetoId = projetoId,
                Ambiente = ambiente,
                Nome = nome.Trim(),
                Configuracao = new Dictionary<string, string>(configuracao),
                Ordem = ordem
            });
        }

        public Result Alterar(string nome, IDictionary<string, string> configuracao, int ordem)
        {
            Guard.AgainstNull(configuracao, nameof(configuracao));

            var erro = Validar(nome, configuracao);
            if (erro is not null)
                return Result.Falha(erro);

            Nome = nome.Trim();
            Configuracao = new Dictionary<string, string>(configuracao);
            Ordem = ordem;
            return Result.Sucesso();
        }

        // A mensagem cita só o nome da chave, nunca o valor.
        private static Erro? Validar(string? nome, IDictionary<string, string> configuracao)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return new Erro("destino.nome-obrigatorio", "Informe o nome do Destino.", TipoErro.Validation);

            var chave = configuracao.Keys.Order(StringComparer.Ordinal)
                .FirstOrDefault(k => IndiciosDeSegredo.Any(i => k.Contains(i, StringComparison.OrdinalIgnoreCase)));
            if (chave is not null)
                return new Erro(
                    "destino.configuracao-com-segredo",
                    $"A chave \"{chave}\" parece guardar segredo. A Esteira não guarda segredo: cadastre-o como variável no GitLab e tire a chave da configuração.",
                    TipoErro.Validation);

            return null;
        }
    }
}
