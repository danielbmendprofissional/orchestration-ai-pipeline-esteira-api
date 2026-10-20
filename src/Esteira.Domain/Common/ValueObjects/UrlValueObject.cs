using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record UrlValueObject
    {
        public const int TamanhoMaximoPadrao = 100;
        public const int TamanhoMaximoDast = 500;

        private UrlValueObject(string url, int tamanhoMaximo)
        {
            Uri uri = null;

            Guard.Against<DomainException>(
                !Uri.TryCreate(url?.Trim(), UriKind.Absolute, out uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps),
                "A URL informada é inválida.");

            var valor = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

            Guard.Against<DomainException>(
                valor.Length > tamanhoMaximo,
                $"A URL deve ter no máximo {tamanhoMaximo} caracteres.");

            Valor = valor;
        }

        public string Valor { get; }

        public static UrlValueObject Criar(string url, int tamanhoMaximo = TamanhoMaximoPadrao)
            => new(url, tamanhoMaximo);

        public static UrlValueObject CriarDast(string url)
            => new(url, TamanhoMaximoDast);

        public static string Normalize(string url) => Criar(url).Valor;

        public override string ToString() => Valor;
    }
}
