using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class AplicacaoEntity : EntityBase
    {
        private AplicacaoEntity() { }

        public string Nome { get; private set; } = default!;
        // Sempre normalizada: sem espaços nas pontas, em maiúscula.
        public string Sigla { get; private set; } = default!;
        public long GrupoGitLabId { get; private set; }
        public string GrupoGitLabCaminho { get; private set; } = default!;
        public bool Ativa { get; private set; }

        public static string NormalizarSigla(string? sigla) => (sigla ?? string.Empty).Trim().ToUpperInvariant();

        public static Result<AplicacaoEntity> Criar(string nome, string sigla, long grupoGitLabId, string grupoGitLabCaminho)
        {
            var erro = Validar(nome, sigla);
            if (erro is not null)
                return erro;

            return Result<AplicacaoEntity>.Sucesso(new AplicacaoEntity
            {
                Nome = nome.Trim(),
                Sigla = NormalizarSigla(sigla),
                GrupoGitLabId = grupoGitLabId,
                GrupoGitLabCaminho = grupoGitLabCaminho,
                Ativa = true
            });
        }

        public Result Alterar(string nome, string sigla, bool ativa)
        {
            var erro = Validar(nome, sigla);
            if (erro is not null)
                return Result.Falha(erro);

            Nome = nome.Trim();
            Sigla = NormalizarSigla(sigla);
            Ativa = ativa;
            return Result.Sucesso();
        }

        private static Erro? Validar(string? nome, string? sigla)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return new Erro("aplicacao.nome-obrigatorio", "Informe o nome da Aplicação.", TipoErro.Validation);
            if (string.IsNullOrWhiteSpace(sigla))
                return new Erro("aplicacao.sigla-obrigatoria", "Informe a sigla da Aplicação.", TipoErro.Validation);
            return null;
        }
    }
}
