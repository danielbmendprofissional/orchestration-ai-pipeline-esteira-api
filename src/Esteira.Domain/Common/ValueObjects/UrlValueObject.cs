using Esteira.Domain.Common.Resultados;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record UrlValueObject
    {
        public const int TamanhoMaximoPadrao = 100;
        public const int TamanhoMaximoDast = 500;

        private UrlValueObject(string valor)
        {
            Valor = valor;
        }

        public string Valor { get; }

        public static Result<UrlValueObject> Criar(string url, int tamanhoMaximo = TamanhoMaximoPadrao)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return new Erro("url.invalida", "A URL informada é inválida.", TipoErro.Validation);

            var valor = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

            if (valor.Length > tamanhoMaximo)
                return new Erro("url.longa-demais", $"A URL deve ter no máximo {tamanhoMaximo} caracteres.", TipoErro.Validation);

            return Result<UrlValueObject>.Sucesso(new UrlValueObject(valor));
        }

        public static Result<UrlValueObject> CriarDast(string url)
            => Criar(url, TamanhoMaximoDast);

        public override string ToString() => Valor;
    }
}
