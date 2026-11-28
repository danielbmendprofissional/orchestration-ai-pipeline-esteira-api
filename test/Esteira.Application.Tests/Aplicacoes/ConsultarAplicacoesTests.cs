using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Queries.Listar;
using Esteira.Application.Aplicacoes.Queries.Obter;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.3: GET /aplicacoes (visibilidade por papel/MOR) e GET /aplicacoes/{id} (404 sem papel).
    [Trait("Fatia", "A1")]
    public class ConsultarAplicacoesTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAplicacaoQuery _query = Substitute.For<IAplicacaoQuery>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();

        [Theory, DisplayName("Lista: repassa busca, ativa e a identidade (sub e MOR) para o filtro de visibilidade")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Lista_Repassa(bool mor)
        {
            _usuario.Id.Returns("sub-1");
            _usuario.EhMor.Returns(mor);
            var pagina = new PaginacaoDto { Pagina = 2 };
            var esperado = Result<PaginaDto<AplicacaoListaDto>>.Sucesso(new PaginaDto<AplicacaoListaDto> { Itens = [], Pagina = 2, TamanhoPagina = 10, Total = 0 });
            _query.ListarAsync(pagina, "vlt", true, "sub-1", mor, Arg.Any<CancellationToken>()).Returns(esperado);

            var r = await new ListarAplicacoesHandler(_query, _usuario).Handle(new ListarAplicacoesQuery(pagina, "vlt", true), Ct);

            r.Should().BeSameAs(esperado);
        }

        [Fact, DisplayName("Detalhe: sem papel e sem MOR responde 404 e não lê a Aplicação")]
        public async Task Detalhe_SemPapel()
        {
            var id = Guid.NewGuid();
            _acesso.ResolverIdAsync(id, Arg.Any<CancellationToken>()).Returns(9L);
            _autorizacao.AutorizarAsync(9, OperacaoAplicacao.Ver, Arg.Any<CancellationToken>()).Returns(Result.Falha(AutorizacaoAplicacao.NaoEncontrada));

            var r = await new ObterAplicacaoHandler(_acesso, _autorizacao, _query, Substitute.For<IOpcoesDosAgentes>()).Handle(new ObterAplicacaoQuery(id), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
            await _query.DidNotReceiveWithAnyArgs().ObterDetalheAsync(default, default);
        }

        [Fact, DisplayName("Detalhe: com acesso devolve o detalhe")]
        public async Task Detalhe_ComAcesso()
        {
            var id = Guid.NewGuid();
            var detalhe = new AplicacaoDetalheDto { Id = id, Nome = "N", Sigla = "S", Grupo = "g" };
            _acesso.ResolverIdAsync(id, Arg.Any<CancellationToken>()).Returns(9L);
            _autorizacao.AutorizarAsync(9, OperacaoAplicacao.Ver, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _query.ObterDetalheAsync(9, Arg.Any<CancellationToken>()).Returns(detalhe);

            var r = await new ObterAplicacaoHandler(_acesso, _autorizacao, _query, Substitute.For<IOpcoesDosAgentes>()).Handle(new ObterAplicacaoQuery(id), Ct);

            r.Value.Should().BeSameAs(detalhe);
        }
    }
}
