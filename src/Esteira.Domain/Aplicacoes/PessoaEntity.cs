using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    // Quem já entrou na Esteira ao menos uma vez. Sub único.
    public sealed class PessoaEntity : EntityBase
    {
        private PessoaEntity() { }

        public string Sub { get; private set; } = default!;
        public string Nome { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public DateTime UltimoLoginEm { get; private set; }

        public static PessoaEntity Criar(string sub, string nome, string email, DateTime loginEm)
        {
            Guard.AgainstNullOrWhiteSpace(sub, nameof(sub));

            return new PessoaEntity { Sub = sub, Nome = nome ?? string.Empty, Email = email ?? string.Empty, UltimoLoginEm = loginEm };
        }

        public void RegistrarLogin(string nome, string email, DateTime loginEm)
        {
            Nome = nome ?? string.Empty;
            Email = email ?? string.Empty;
            UltimoLoginEm = loginEm;
        }
    }
}
