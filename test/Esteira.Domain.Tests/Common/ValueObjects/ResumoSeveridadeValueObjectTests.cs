using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.ValueObjects;
using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Common.ValueObjects
{
    public class ResumoSeveridadeValueObjectTests
    {
        private static ResumoSeveridadeValueObject Resumo(int c, int a, int m, int b) => ResumoSeveridadeValueObject.Criar(c, a, m, b).Value;

        [Fact, DisplayName("Deve criar o resumo com as quantidades informadas e calcular o total")]
        public void Criar_Valido_DeveCalcularTotal()
        {
            var resumo = Resumo(1, 2, 3, 4);

            resumo.VulnerabilidadeCritica.Should().Be(1);
            resumo.VulnerabilidadeAlta.Should().Be(2);
            resumo.VulnerabilidadeMedia.Should().Be(3);
            resumo.VulnerabilidadeBaixa.Should().Be(4);
            resumo.Total.Should().Be(10);
        }

        [Theory, DisplayName("Deve devolver falha severidade.quantidade-negativa quando alguma quantidade for negativa")]
        [InlineData(-1, 0, 0, 0)]
        [InlineData(0, -1, 0, 0)]
        [InlineData(0, 0, -1, 0)]
        [InlineData(0, 0, 0, -1)]
        public void Criar_Negativo_DeveDevolverFalha(int critica, int alta, int media, int baixa)
        {
            var resultado = ResumoSeveridadeValueObject.Criar(critica, alta, media, baixa);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "severidade.quantidade-negativa" && e.Type == TipoErro.Validation);
        }

        [Fact, DisplayName("Deve ser igual quando todas as quantidades forem iguais")]
        public void Equals_MesmosValores_DeveSerIgual()
        {
            var a = Resumo(1, 2, 3, 4);
            var b = Resumo(1, 2, 3, 4);

            a.Should().Be(b);
            (a == b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact, DisplayName("Não deve ser igual quando as quantidades estiverem trocadas")]
        public void Equals_ValoresTrocados_NaoDeveSerIgual()
        {
            Resumo(1, 2, 0, 0).Should().NotBe(Resumo(2, 1, 0, 0));
        }

        [Fact, DisplayName("Deve somar os resumos sem alterar os originais")]
        public void Somar_DeveRetornarNovaInstancia()
        {
            var a = Resumo(1, 1, 1, 1);
            var b = Resumo(2, 0, 3, 0);

            var soma = a.Somar(b);

            soma.Should().Be(Resumo(3, 1, 4, 1));
            a.Should().Be(Resumo(1, 1, 1, 1));
        }

        [Fact, DisplayName("Deve lançar ArgumentNullException ao somar um resumo nulo")]
        public void Somar_Nulo_DeveLancarArgumentNull()
        {
            var act = () => ResumoSeveridadeValueObject.Vazio.Somar(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact, DisplayName("Deve retornar resumo vazio ao somar uma lista vazia")]
        public void SomarTodos_ListaVazia_DeveRetornarVazio()
        {
            ResumoSeveridadeValueObject.SomarTodos(Array.Empty<ResumoSeveridadeValueObject>())
                .Should().Be(ResumoSeveridadeValueObject.Vazio);
        }

        [Fact, DisplayName("Deve somar todos os resumos da lista")]
        public void SomarTodos_DeveSomarTodos()
        {
            var resumos = new[]
            {
                Resumo(1, 0, 0, 0),
                Resumo(0, 2, 0, 0),
                Resumo(0, 0, 3, 4)
            };

            ResumoSeveridadeValueObject.SomarTodos(resumos).Should().Be(Resumo(1, 2, 3, 4));
        }

        [Fact, DisplayName("Deve mapear as severidades ignorando maiúsculas, minúsculas e nulos")]
        public void DeSeveridades_DeveMapearIgnorandoCaseENulos()
        {
            var resumo = ResumoSeveridadeValueObject.DeSeveridades(new[]
            {
                "CRITICAL", "high", "Error", "WARNING", "medium", "INFO", "low", "UNKNOWN", null, "OUTRA"
            });

            resumo.Should().Be(Resumo(1, 2, 2, 3));
        }

        [Fact, DisplayName("Deve lançar ArgumentNullException quando a lista de severidades for nula")]
        public void DeSeveridades_Nula_DeveLancarArgumentNull()
        {
            var act = () => ResumoSeveridadeValueObject.DeSeveridades(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact, DisplayName("Deve ser imutável (sem setters públicos)")]
        public void DeveSerImutavel()
        {
            typeof(ResumoSeveridadeValueObject).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Should().BeEmpty();
        }
    }
}
