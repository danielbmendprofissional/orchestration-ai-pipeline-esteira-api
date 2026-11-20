using Esteira.Domain.Common.Resultados;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using System.Collections.Concurrent;
using System.Reflection;

namespace Esteira.Application.Behavios
{
    // ADR-0003: falha de validação vira Result de falha, não exception. Só requests que respondem
    // Result/Result<T> passam por aqui; qualquer outra resposta segue direto pro handler.
    public class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        // Código estável para o erro sem WithErrorCode. O código padrão do FluentValidation
        // (ex.: "NotEmptyValidator") é detalhe interno e não pode vazar na resposta.
        private const string CodigoPadrao = "validacao.campo-invalido";
        private const string SufixoDosValidatorsEmbutidos = "Validator";

        // Result<T>.Falha é static escondido (new static); a fábrica por tipo fica cacheada.
        private static readonly ConcurrentDictionary<Type, MethodInfo> FabricasDeFalha = new();

        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!RespondeComResult(typeof(TResponse)))
                return await next();

            var context = new ValidationContext<TRequest>(request);

            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count == 0)
                return await next();

            return Falha(failures.Select(ErroDe).ToArray());
        }

        private static bool RespondeComResult(Type tipo)
            => tipo == typeof(Result)
               || (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Result<>));

        private static Erro ErroDe(ValidationFailure falha)
            => new(CodigoEstavel(falha.ErrorCode), falha.ErrorMessage, TipoErro.Validation);

        // Sem .WithErrorCode(...), o FluentValidation devolve o nome do validator embutido
        // ("NotEmptyValidator", "NotNullValidator"...). Nulo/vazio cai no mesmo código padrão.
        private static string CodigoEstavel(string? errorCode)
            => string.IsNullOrWhiteSpace(errorCode)
               || errorCode.EndsWith(SufixoDosValidatorsEmbutidos, StringComparison.Ordinal)
                ? CodigoPadrao
                : errorCode;

        private static TResponse Falha(Erro[] erros)
        {
            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Falha(erros);

            var fabrica = FabricasDeFalha.GetOrAdd(typeof(TResponse), tipo => tipo.GetMethod(
                nameof(Result<object>.Falha),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!);

            return (TResponse)fabrica.Invoke(null, new object[] { erros })!;
        }
    }

}
