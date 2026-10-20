using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.FluxosDeploy.ValueObjects
{
    public sealed record VersaoFluxoValueObject
    {
        private VersaoFluxoValueObject(int numero)
        {
            Guard.Against<DomainException>(
                numero < 1,
                "A versão do fluxo deve ser maior ou igual a 1.");

            Numero = numero;
        }

        public int Numero { get; }

        public static VersaoFluxoValueObject Inicial => new(1);

        public static VersaoFluxoValueObject Criar(int numero) => new(numero);

        public static VersaoFluxoValueObject Criar(string texto)
        {
            Guard.AgainstNullOrWhiteSpace(texto, nameof(texto));

            var numero = 0;

            Guard.Against<DomainException>(
                !int.TryParse(texto.Trim().TrimStart('v', 'V'), out numero),
                $"Versão de fluxo inválida: '{texto}'.");

            return new(numero);
        }

        public static VersaoFluxoValueObject Proxima(VersaoFluxoValueObject atual)
            => atual is null ? Inicial : new(atual.Numero + 1);

        public override string ToString() => $"v{Numero}";
    }
}
