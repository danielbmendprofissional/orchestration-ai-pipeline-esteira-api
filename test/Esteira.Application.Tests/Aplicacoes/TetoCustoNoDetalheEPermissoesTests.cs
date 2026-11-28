using System.ComponentModel;
using System.Globalization;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.DefinirTetoCusto;
using Esteira.Application.Aplicacoes.Queries.Obter;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // X3.3: `tetoCusto` no detalhe da Aplicação e `podeEditarTetoCusto` em /permissoes (critério 10).
    [Trait("Fatia", "X3")]
    public class TetoCustoNoDetalheEPermissoesTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Theory, DisplayName("10: /permissoes devolve podeEditarTetoCusto pela mesma regra da rota (só MOR)")]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task Permissoes_PodeEditarTetoCusto(bool mor, bool esperado)
        {
            var usuario = Substitute.For<IUsuarioAtual>();
            var query = Substitute.For<IAcessoAplicacaoQuery>();
            usuario.Id.Returns("sub-1");
            usuario.EhMor.Returns(mor);
            query.PapeisAsync(7, "sub-1", Arg.Any<CancellationToken>()).Returns([PapelAplicacao.Membro]);

            var r = await new AutorizacaoAplicacao(usuario, query).PermissoesAsync(7, Ct);

            r.Value.PodeEditarTetoCusto.Should().Be(esperado);
        }

        [Theory, DisplayName("Detalhe traz tetoCusto: definido, ou nulo com o padrão como efetivo")]
        [InlineData("3.50", "3.50")]
        [InlineData(null, "2.00")]
        public async Task Detalhe_TetoCusto(string? definido, string efetivo)
        {
            var acesso = Substitute.For<IAcessoAplicacaoQuery>();
            var autorizacao = Substitute.For<IAutorizacaoAplicacao>();
            var query = Substitute.For<IAplicacaoQuery>();
            var opcoes = Substitute.For<IOpcoesDosAgentes>();
            var id = Guid.NewGuid();
            decimal? teto = definido is null ? null : decimal.Parse(definido, CultureInfo.InvariantCulture);
            opcoes.TetoCustoPadraoUsd.Returns(2.00m);
            acesso.ResolverIdAsync(id, Arg.Any<CancellationToken>()).Returns(9L);
            autorizacao.AutorizarAsync(9, OperacaoAplicacao.Ver, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            query.ObterDetalheAsync(9, Arg.Any<CancellationToken>()).Returns(new AplicacaoDetalheDto
            {
                Id = id, Nome = "N", Sigla = "S", Grupo = "g",
                TetoCusto = new TetoCustoDto { TetoCustoUsd = teto }
            });

            var r = await new ObterAplicacaoHandler(acesso, autorizacao, query, opcoes).Handle(new ObterAplicacaoQuery(id), Ct);

            r.Value.TetoCusto.Should().BeEquivalentTo(
                new TetoCustoDto { TetoCustoUsd = teto, PadraoUsd = 2.00m, EfetivoUsd = decimal.Parse(efetivo, CultureInfo.InvariantCulture) });
        }
    }
}
