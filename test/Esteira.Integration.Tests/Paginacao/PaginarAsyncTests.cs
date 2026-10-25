using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Common.Paginacao;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Paginacao
{
    // ADR-0002, fatia B1: contagem, ordenação e recorte acontecem no banco.
    public class PaginarAsyncTests(InfraestruturaFixture infra)
    {
        private static readonly IReadOnlyList<ItemDeTesteDto> Todos =
            Enumerable.Range(1, 25).Select(i => new ItemDeTesteDto { Id = i, Nome = $"item-{i:00}", Grupo = i % 3 }).ToList();

        private async Task<(ContextoDePaginacao Contexto, SqlCapturado Sql)> NovoContextoAsync()
        {
            await ContextoDePaginacao.SemearAsync(infra);
            var sql = new SqlCapturado();
            return (new ContextoDePaginacao(ContextoDePaginacao.Opcoes(infra.ConnectionString, sql)), sql);
        }

        private static Task<Result<PaginaDto<ItemDeTesteDto>>> Paginar(ContextoDePaginacao contexto, PaginacaoDto paginacao)
            => contexto.Itens.PaginarAsync(
                paginacao,
                i => new ItemDeTesteDto { Id = i.Id, Nome = i.Nome, Grupo = i.Grupo },
                ContextoDePaginacao.OrdenacaoPadrao,
                TestContext.Current.CancellationToken);

        [Fact, DisplayName("Deve ordenar pelo campo do DTO em sentido decrescente com a ordenação padrão como desempate")]
        public async Task SortDesc_DeveOrdenarPeloCampoEDesempatarPelaPadrao()
        {
            var (contexto, _) = await NovoContextoAsync();
            var esperado = Todos.OrderByDescending(i => i.Grupo).ThenBy(i => i.Id).Take(10).ToList();

            var resultado = await Paginar(contexto, new PaginacaoDto { Ordenacao = "grupo", Sentido = "desc" });

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Itens.Should().Equal(esperado);
            resultado.Value.Total.Should().Be(25);
            resultado.Value.TotalPaginas.Should().Be(3);
        }

        [Fact, DisplayName("Deve aceitar o campo e o sentido de ordenação sem diferenciar maiúsculas")]
        public async Task SortComOutraCaixa_DeveOrdenar()
        {
            var (contexto, _) = await NovoContextoAsync();

            var resultado = await Paginar(contexto, new PaginacaoDto { Ordenacao = "NOME", Sentido = "DESC" });

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Itens.Select(i => i.Id).Should().Equal(Enumerable.Range(16, 10).Reverse().Select(i => (long)i));
        }

        [Fact, DisplayName("Deve falhar com paginacao.ordenacao-invalida quando o sort não é campo do DTO")]
        public async Task SortInexistente_DeveFalharComOrdenacaoInvalida()
        {
            var (contexto, sql) = await NovoContextoAsync();

            var resultado = await Paginar(contexto, new PaginacaoDto { Ordenacao = "naoExiste" });

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("paginacao.ordenacao-invalida");
            sql.Comandos.Should().BeEmpty("a consulta não roda com ordenação inválida");
        }

        [Fact, DisplayName("Deve devolver itens vazios com o total real quando a página passa da última")]
        public async Task PaginaAlemDaUltima_DeveDevolverVazioComTotalReal()
        {
            var (contexto, _) = await NovoContextoAsync();

            var resultado = await Paginar(contexto, new PaginacaoDto { Pagina = 9, TamanhoPagina = 10 });

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Itens.Should().BeEmpty();
            resultado.Value.Total.Should().Be(25);
            resultado.Value.Pagina.Should().Be(9);
        }

        [Fact, DisplayName("Deve devolver itens vazios com o total real quando a página é tão grande que o cálculo do recorte estouraria")]
        public async Task PaginaGigante_DeveDevolverVazioSemEstourar()
        {
            var (contexto, _) = await NovoContextoAsync();

            var resultado = await Paginar(contexto, new PaginacaoDto { Pagina = int.MaxValue, TamanhoPagina = 100 });

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Itens.Should().BeEmpty();
            resultado.Value.Total.Should().Be(25);
        }

        [Fact, DisplayName("Deve devolver os mesmos itens na mesma ordem ao pedir a mesma página duas vezes com valores repetidos")]
        public async Task MesmaPaginaDuasVezes_DeveSerEstavel()
        {
            var (contexto, _) = await NovoContextoAsync();
            var pedido = new PaginacaoDto { Pagina = 2, TamanhoPagina = 4, Ordenacao = "grupo", Sentido = "asc" };

            var primeira = await Paginar(contexto, pedido);
            var segunda = await Paginar(contexto, pedido);

            segunda.Value.Itens.Should().Equal(primeira.Value.Itens);
            primeira.Value.Itens.Should().Equal(Todos.OrderBy(i => i.Grupo).ThenBy(i => i.Id).Skip(4).Take(4));
        }

        [Fact, DisplayName("Deve contar e recortar a página no banco com COUNT, LIMIT e OFFSET")]
        public async Task Consulta_DeveContarERecortarNoBanco()
        {
            var (contexto, sql) = await NovoContextoAsync();

            var resultado = await Paginar(contexto, new PaginacaoDto { Pagina = 2, TamanhoPagina = 5 });

            resultado.Value.Itens.Should().HaveCount(5);
            sql.Comandos.Should().HaveCount(2);
            sql.Comandos.Should().ContainSingle(c => c.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
            var paginaSql = sql.Comandos.Single(c => !c.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
            paginaSql.Should().Contain("LIMIT").And.Contain("OFFSET");
        }
    }
}
