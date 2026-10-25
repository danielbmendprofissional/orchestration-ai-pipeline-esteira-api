using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Behavios;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace Esteira.Application.Tests.Common
{
    // Critérios 4 e 5 da fatia B2: falha do FluentValidation vira Result de falha, sem executar o handler.
    public class ValidationBehaviorTests
    {
        public sealed record Requisicao(string Url);

        public sealed class RequisicaoSemErrorCode
        {
            public string Nome { get; set; }
        }

        public sealed record Consulta;

        [Fact, DisplayName("Deve devolver Result de falha com um erro Validation por falha e não chamar o handler")]
        public async Task FalhaDeValidacao_DeveDevolverUmErroPorFalhaSemChamarHandler()
        {
            var validator = Substitute.For<IValidator<Requisicao>>();
            validator.Validate(Arg.Any<IValidationContext>()).Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Url", "A url é obrigatória.") { ErrorCode = "url.obrigatoria" },
                new ValidationFailure("Url", "A url informada é inválida.") { ErrorCode = "url.invalida" }
            }));

            var chamouHandler = false;
            var behavior = new ValidationBehavior<Requisicao, Result<string>>(new[] { validator });

            var resultado = await behavior.Handle(
                new Requisicao(string.Empty),
                _ => { chamouHandler = true; return Task.FromResult(Result<string>.Sucesso("ok")); },
                CancellationToken.None);

            chamouHandler.Should().BeFalse();
            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().HaveCount(2);
            resultado.Errors.Select(e => e.Code).Should().Equal("url.obrigatoria", "url.invalida");
            resultado.Errors.Select(e => e.Message).Should().Equal("A url é obrigatória.", "A url informada é inválida.");
            resultado.Errors.Should().OnlyContain(e => e.Type == TipoErro.Validation);
        }

        [Fact, DisplayName("Deve devolver Result de falha sem valor quando o request responde Result")]
        public async Task FalhaDeValidacao_ComResultSemValor_DeveDevolverFalha()
        {
            var validator = Substitute.For<IValidator<Requisicao>>();
            validator.Validate(Arg.Any<IValidationContext>()).Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Url", "A url é obrigatória.") { ErrorCode = "url.obrigatoria" }
            }));

            var chamouHandler = false;
            var behavior = new ValidationBehavior<Requisicao, Result>(new[] { validator });

            var resultado = await behavior.Handle(
                new Requisicao(string.Empty),
                _ => { chamouHandler = true; return Task.FromResult(Result.Sucesso()); },
                CancellationToken.None);

            chamouHandler.Should().BeFalse();
            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("url.obrigatoria");
        }

        [Fact, DisplayName("Deve usar validacao.campo-invalido quando o validator não declara ErrorCode")]
        public async Task ValidatorSemErrorCode_DeveUsarCodigoPadrao()
        {
            var validator = new InlineValidator<RequisicaoSemErrorCode>();
            validator.RuleFor(r => r.Nome).NotEmpty().WithMessage("O nome é obrigatório.");

            var behavior = new ValidationBehavior<RequisicaoSemErrorCode, Result>(new[] { validator });

            var resultado = await behavior.Handle(
                new RequisicaoSemErrorCode { Nome = string.Empty },
                _ => Task.FromResult(Result.Sucesso()),
                CancellationToken.None);

            resultado.IsSuccess.Should().BeFalse();
            var erro = resultado.Errors.Should().ContainSingle().Subject;
            erro.Code.Should().Be("validacao.campo-invalido");
            erro.Message.Should().Be("O nome é obrigatório.");
            erro.Type.Should().Be(TipoErro.Validation);
        }

        [Fact, DisplayName("Deve executar o handler sem validar quando a resposta não é Result")]
        public async Task RespostaQueNaoEResult_NaoDeveValidarNemBloquearHandler()
        {
            var validator = Substitute.For<IValidator<Consulta>>();
            var chamouHandler = false;
            var behavior = new ValidationBehavior<Consulta, string>(new[] { validator });

            var resultado = await behavior.Handle(
                new Consulta(),
                _ => { chamouHandler = true; return Task.FromResult("ok"); },
                CancellationToken.None);

            resultado.Should().Be("ok");
            chamouHandler.Should().BeTrue();
            validator.DidNotReceive().Validate(Arg.Any<IValidationContext>());
        }
    }
}
