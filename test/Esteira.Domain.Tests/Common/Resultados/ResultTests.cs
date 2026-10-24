using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Tests.Common.Resultados
{
    // ADR-0003: o domínio decide e devolve Result; o code é estável (<area>.<motivo>), a message é pt-BR.
    public class ResultTests
    {
        private static Erro ErroDeValidacao(string code = "url.invalida")
            => new(code, "A url informada é inválida.", TipoErro.Validation);

        [Fact, DisplayName("Deve criar Result de sucesso sem valor")]
        public void SucessoSemValor_DeveSerSucesso()
        {
            var resultado = Result.Sucesso();

            resultado.IsSuccess.Should().BeTrue();
            resultado.Errors.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve criar Result de falha guardando todos os erros na ordem")]
        public void Falha_DeveGuardarTodosOsErrosNaOrdem()
        {
            var primeiro = ErroDeValidacao();
            var segundo = new Erro("projeto.nao.encontrado", "Projeto não encontrado.", TipoErro.NotFound);

            var resultado = Result.Falha(primeiro, segundo);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().HaveCount(2);
            resultado.Errors[0].Should().Be(primeiro);
            resultado.Errors[1].Should().Be(segundo);
        }

        [Fact, DisplayName("Deve lançar ArgumentException quando Falha não recebe nenhum erro")]
        public void Falha_SemErros_DeveLancarArgumentException()
        {
            var acao = () => Result.Falha();
            var acaoGenerica = () => Result<int>.Falha();

            acao.Should().Throw<ArgumentException>();
            acaoGenerica.Should().Throw<ArgumentException>();
        }

        [Fact, DisplayName("Não deve expor Value em Result sem valor")]
        public void ResultSemValor_NaoDeveTerPropriedadeValue()
        {
            typeof(Result).GetProperty(nameof(Result<int>.Value)).Should().BeNull();
        }

        [Fact, DisplayName("Deve expor o valor em Result<T> de sucesso")]
        public void Sucesso_ComValor_DeveExporValue()
        {
            var resultado = Result<int>.Sucesso(42);

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Be(42);
            resultado.Errors.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar InvalidOperationException ao ler Value de uma falha")]
        public void Falha_LerValue_DeveLancarInvalidOperationException()
        {
            var resultado = Result<int>.Falha(ErroDeValidacao());

            resultado.IsSuccess.Should().BeFalse();
            var acao = () => _ = resultado.Value;
            acao.Should().Throw<InvalidOperationException>();
        }

        [Fact, DisplayName("Deve acessar IsSuccess e Errors pela base, sem cast inseguro")]
        public void ResultGenerico_DeveSerAcessivelPelaBase()
        {
            Result base1 = Result<int>.Falha(ErroDeValidacao());
            Result base2 = Result<int>.Sucesso(7);

            base1.IsSuccess.Should().BeFalse();
            base1.Errors.Should().ContainSingle().Which.Code.Should().Be("url.invalida");
            base2.IsSuccess.Should().BeTrue();
            base2.Errors.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve manter o type do erro no enum do domínio")]
        public void Erro_DeveExporTipo()
        {
            var erro = new Erro("ambiente.ocupado", "Ambiente já está ocupado.", TipoErro.Conflict);

            erro.Code.Should().Be("ambiente.ocupado");
            erro.Message.Should().Be("Ambiente já está ocupado.");
            erro.Type.Should().Be(TipoErro.Conflict);
        }

        [Fact, DisplayName("Deve lançar ArgumentException quando o code ou a message do Erro vem vazio")]
        public void Erro_SemCodeOuMessage_DeveLancarArgumentException()
        {
            var semCode = () => new Erro("", "mensagem", TipoErro.Validation);
            var semMessage = () => new Erro("url.invalida", "", TipoErro.Validation);

            semCode.Should().Throw<ArgumentException>();
            semMessage.Should().Throw<ArgumentException>();
        }
    }
}
