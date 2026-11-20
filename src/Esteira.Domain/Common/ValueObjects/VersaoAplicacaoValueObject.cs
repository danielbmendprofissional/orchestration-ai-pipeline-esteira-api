using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record VersaoAplicacaoValueObject
    {
        private VersaoAplicacaoValueObject(int major)
        {
            Major = major;
        }

        public int Major { get; }

        public static Result<VersaoAplicacaoValueObject> Criar(int major)
            => major < 0
                ? new Erro("versao.invalida", "A versão da aplicação não pode ser negativa.", TipoErro.Validation)
                : Result<VersaoAplicacaoValueObject>.Sucesso(new(major));

        public static Result<VersaoAplicacaoValueObject> Criar(string texto)
        {
            if (!TentarMajor(texto, out var major))
                return new Erro("versao.invalida", $"Versão da aplicação inválida: '{texto}'.", TipoErro.Validation);

            return Criar(major);
        }

        public static Result<VersaoAplicacaoValueObject> DaTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || !TentarMajor(tag.Trim().Split('.')[0], out var major) || major < 0)
                return new Erro("versao.tag-invalida", $"Tag de versão inválida: '{tag}'.", TipoErro.Validation);

            return Result<VersaoAplicacaoValueObject>.Sucesso(new(major));
        }

        public bool AceitaTag(string tag)
        {
            var versao = DaTag(tag);
            return versao.IsSuccess && versao.Value.Equals(this);
        }

        public bool EhMenorQue(VersaoAplicacaoValueObject outra)
        {
            Guard.AgainstNull(outra, nameof(outra));

            return Major < outra.Major;
        }

        public override string ToString() => $"v{Major}";

        private static bool TentarMajor(string texto, out int major)
        {
            major = 0;
            return !string.IsNullOrWhiteSpace(texto)
                && int.TryParse(texto.Trim().TrimStart('v', 'V'), out major);
        }
    }
}
