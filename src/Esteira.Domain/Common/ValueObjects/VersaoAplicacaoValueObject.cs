using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record VersaoAplicacaoValueObject
    {
        private VersaoAplicacaoValueObject(int major)
        {
            Guard.Against<DomainException>(
                major < 0,
                "A versão da aplicação não pode ser negativa.");

            Major = major;
        }

        public int Major { get; }

        public static VersaoAplicacaoValueObject Criar(int major) => new(major);

        public static VersaoAplicacaoValueObject Criar(string texto)
        {
            Guard.AgainstNullOrWhiteSpace(texto, nameof(texto));

            var major = 0;

            Guard.Against<DomainException>(
                !int.TryParse(texto.Trim().TrimStart('v', 'V'), out major),
                $"Versão da aplicação inválida: '{texto}'.");

            return new(major);
        }

        public static VersaoAplicacaoValueObject DaTag(string tag)
        {
            Guard.AgainstNullOrWhiteSpace(tag, nameof(tag));

            return Criar(tag.Trim().Split('.')[0]);
        }

        public bool AceitaTag(string tag) => DaTag(tag).Equals(this);

        public bool EhMenorQue(VersaoAplicacaoValueObject outra)
        {
            Guard.AgainstNull(outra, nameof(outra));

            return Major < outra.Major;
        }

        public override string ToString() => $"v{Major}";
    }
}
