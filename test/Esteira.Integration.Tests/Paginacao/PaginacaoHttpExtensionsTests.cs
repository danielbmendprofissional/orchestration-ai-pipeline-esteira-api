using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Api.Paginacao;
using Microsoft.AspNetCore.Http;

namespace Esteira.Integration.Tests.Paginacao
{
    // ADR-0002, fatia B1.2: leitura de X-Page/X-Page-Size (headers) e sort/order (query string).
    public class PaginacaoHttpExtensionsTests
    {
        private static HttpRequest Pedido(string? pagina = null, string? tamanho = null, string? sort = null, string? order = null)
        {
            var contexto = new DefaultHttpContext();
            if (pagina is not null) contexto.Request.Headers[HeadersPaginacao.Pagina] = pagina;
            if (tamanho is not null) contexto.Request.Headers[HeadersPaginacao.TamanhoPagina] = tamanho;
            var query = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
            if (sort is not null) query["sort"] = sort;
            if (order is not null) query["order"] = order;
            contexto.Request.Query = new QueryCollection(query);
            return contexto.Request;
        }

        [Fact, DisplayName("Deve usar página 1, tamanho 10 e sentido asc quando nada é informado")]
        public void SemParametros_DeveUsarPadroes()
        {
            var resultado = Pedido().ObterPaginacao();

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Pagina.Should().Be(1);
            resultado.Value.TamanhoPagina.Should().Be(10);
            resultado.Value.Ordenacao.Should().BeNull();
            resultado.Value.Sentido.Should().Be("asc");
        }

        [Fact, DisplayName("Deve ler página, tamanho, sort e order informados, aceitando o sentido em qualquer caixa")]
        public void ComParametrosValidos_DeveLer()
        {
            var resultado = Pedido("3", "100", "nome", "DESC").ObterPaginacao();

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Pagina.Should().Be(3);
            resultado.Value.TamanhoPagina.Should().Be(100);
            resultado.Value.Ordenacao.Should().Be("nome");
            resultado.Value.Sentido.Should().Be("desc");
        }

        [Theory, DisplayName("Deve recusar X-Page inválido com paginacao.pagina-invalida")]
        [InlineData("abc")]
        [InlineData("1.5")]
        [InlineData("0")]
        [InlineData("-1")]
        public void PaginaInvalida_DeveFalhar(string pagina)
        {
            var resultado = Pedido(pagina: pagina).ObterPaginacao();

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Select(e => e.Code).Should().Equal("paginacao.pagina-invalida");
        }

        [Theory, DisplayName("Deve recusar X-Page-Size inválido com paginacao.tamanho-invalido")]
        [InlineData("abc")]
        [InlineData("1.5")]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("101")]
        public void TamanhoInvalido_DeveFalhar(string tamanho)
        {
            var resultado = Pedido(tamanho: tamanho).ObterPaginacao();

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Select(e => e.Code).Should().Equal("paginacao.tamanho-invalido");
        }

        [Fact, DisplayName("Deve recusar order diferente de asc e desc com paginacao.sentido-invalido")]
        public void SentidoInvalido_DeveFalhar()
        {
            var resultado = Pedido(order: "cima").ObterPaginacao();

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Select(e => e.Code).Should().Equal("paginacao.sentido-invalido");
        }

        [Fact, DisplayName("Deve devolver todos os erros de uma vez quando vários parâmetros são inválidos")]
        public void VariosInvalidos_DeveDevolverTodosOsErros()
        {
            var resultado = Pedido("0", "500", order: "cima").ObterPaginacao();

            resultado.Errors.Select(e => e.Code).Should().Equal(
                "paginacao.pagina-invalida", "paginacao.tamanho-invalido", "paginacao.sentido-invalido");
        }
    }
}
