using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.Papeis;
using Esteira.Application.Aplicacoes.Queries.ListarPapeis;
using Esteira.Application.Aplicacoes.Queries.ListarPessoas;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.5, critérios 13 e 16: papéis da Aplicação e busca de Pessoa. Só o MOR gerencia; pessoa que nunca entrou é recusada.
    [Trait("Fatia", "A1")]
    public class PapeisTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IPessoaRepositorio _pessoas = Substitute.For<IPessoaRepositorio>();
        private readonly IPapelRepositorio _papeis = Substitute.For<IPapelRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly Guid _appId = Guid.NewGuid();

        public PapeisTests()
        {
            _acesso.ResolverIdAsync(_appId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.GerenciarPapeis, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.Ver, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _pessoas.ObterPorSubAsync("sub-ana", Arg.Any<CancellationToken>())
                .Returns(PessoaEntity.Criar("sub-ana", "Ana", "ana@x.com", DateTime.UtcNow));
        }

        private Task<Result<PapelResumoDto>> Vincular(string usuario = "sub-ana", string papel = "analista", Guid? app = null)
            => new VincularPapelHandler(_acesso, _autorizacao, _pessoas, _papeis, _uow, _auditoria)
                .Handle(new VincularPapelCommand(app ?? _appId, usuario, papel), Ct);

        private Task<Result> Desvincular(Guid papelId)
            => new DesvincularPapelHandler(_acesso, _autorizacao, _papeis, _auditoria)
                .Handle(new DesvincularPapelCommand(_appId, papelId), Ct);

        private static PapelAplicacaoEntity NovoPapel(long aplicacaoId = 5)
            => PapelAplicacaoEntity.Criar(aplicacaoId, "sub-ana", PapelAplicacao.Analista);

        // ---- POST /aplicacoes/{id}/papeis ----

        [Fact, DisplayName("Critério 16: vincular pessoa que entrou na Esteira grava o papel, audita Criou e devolve nome e e-mail")]
        public async Task Vincula()
        {
            var r = await Vincular();

            r.IsSuccess.Should().BeTrue();
            (r.Value.Usuario, r.Value.Nome, r.Value.Email, r.Value.Papel).Should().Be(("sub-ana", "Ana", "ana@x.com", "analista"));
            await _papeis.Received(1).AdicionarAsync(
                Arg.Is<PapelAplicacaoEntity>(p => p.AplicacaoId == 5 && p.Usuario == "sub-ana" && p.Papel == PapelAplicacao.Analista), Arg.Any<CancellationToken>());
            _auditoria.Received(1).Registrar("PapelAplicacao", r.Value.Id, AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 16: pessoa que nunca entrou é recusada com papel.pessoa-desconhecida (422), sem gravar nem auditar")]
        public async Task PessoaDesconhecida()
        {
            var r = await Vincular(usuario: "sub-fantasma");

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("papel.pessoa-desconhecida", TipoErro.Failure));
            await _papeis.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Par (Aplicação, usuário, papel) já existente responde papel.duplicado (409) sem gravar")]
        public async Task Duplicado()
        {
            _papeis.ExisteAsync(5, "sub-ana", PapelAplicacao.Analista, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Vincular();

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("papel.duplicado", TipoErro.Conflict));
            await _papeis.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Corrida: violação de unicidade do índice do papel vira papel.duplicado")]
        public async Task Corrida()
        {
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ViolacaoDeUnicidadeException("IX_PapelAplicacao_AplicacaoId_Usuario_Papel"));

            var r = await Vincular();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("papel.duplicado");
        }

        [Fact, DisplayName("Critério 13: só o MOR vincula papel — quem não pode recebe o erro da autorização e nada é gravado")]
        public async Task SoMorVincula()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.GerenciarPapeis, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Vincular();

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            await _papeis.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Aplicação inexistente responde aplicacao.nao-encontrada")]
        public async Task AplicacaoInexistente()
        {
            var r = await Vincular(app: Guid.NewGuid());

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }

        [Theory, DisplayName("Validação: papel fora do vocabulário é papel.papel-invalido; usuário vazio é papel.usuario-obrigatorio")]
        [InlineData("sub-ana", "chefe", "papel.papel-invalido")]
        [InlineData("sub-ana", "", "papel.papel-invalido")]
        [InlineData("", "analista", "papel.usuario-obrigatorio")]
        public void Validacao(string usuario, string papel, string codigo)
        {
            var resultado = new VincularPapelValidator().Validate(new VincularPapelCommand(_appId, usuario, papel));

            resultado.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }

        [Theory, DisplayName("Validação: os cinco papéis do vocabulário passam")]
        [InlineData("analista")]
        [InlineData("membro")]
        [InlineData("aprovador-tecnico")]
        [InlineData("aprovador-negocio")]
        [InlineData("publicador")]
        public void PapeisValidos(string papel)
            => new VincularPapelValidator().Validate(new VincularPapelCommand(_appId, "sub-ana", papel)).IsValid.Should().BeTrue();

        // ---- DELETE /aplicacoes/{id}/papeis/{papelId} ----

        [Fact, DisplayName("Desvincular remove o papel da Aplicação e audita Removeu com o antes")]
        public async Task Desvincula()
        {
            var papel = NovoPapel();
            _papeis.ObterAsync(5, papel.PublicId, Arg.Any<CancellationToken>()).Returns(papel);

            var r = await Desvincular(papel.PublicId);

            r.IsSuccess.Should().BeTrue();
            _papeis.Received(1).Remover(papel);
            _auditoria.Received(1).Registrar("PapelAplicacao", papel.PublicId, AcaoAuditoria.Removeu, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o == null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Papel inexistente ou de outra Aplicação responde papel.nao-encontrado (404)")]
        public async Task DesvinculaInexistente()
        {
            var r = await Desvincular(Guid.NewGuid());

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("papel.nao-encontrado", TipoErro.NotFound));
            _papeis.DidNotReceiveWithAnyArgs().Remover(default!);
        }

        [Fact, DisplayName("Critério 13: só o MOR desvincula papel")]
        public async Task SoMorDesvincula()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.GerenciarPapeis, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));
            var papel = NovoPapel();
            _papeis.ObterAsync(5, papel.PublicId, Arg.Any<CancellationToken>()).Returns(papel);

            var r = await Desvincular(papel.PublicId);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            _papeis.DidNotReceiveWithAnyArgs().Remover(default!);
        }

        // ---- GET /aplicacoes/{id}/papeis ----

        [Fact, DisplayName("Listar papéis exige poder ver a Aplicação (papel ou MOR) e devolve a lista da leitura")]
        public async Task ListaPapeis()
        {
            var query = Substitute.For<IPapelQuery>();
            query.ListarAsync(5, Arg.Any<CancellationToken>()).Returns([new PapelResumoDto { Usuario = "sub-ana", Papel = "analista" }]);

            var r = await new ListarPapeisHandler(_acesso, _autorizacao, query).Handle(new ListarPapeisQuery(_appId), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().ContainSingle().Which.Usuario.Should().Be("sub-ana");
        }

        [Fact, DisplayName("Listar papéis sem papel na Aplicação devolve o erro da autorização (404) e não consulta")]
        public async Task ListaPapeisSemAcesso()
        {
            var query = Substitute.For<IPapelQuery>();
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.Ver, Arg.Any<CancellationToken>()).Returns(Result.Falha(AutorizacaoAplicacao.NaoEncontrada));

            var r = await new ListarPapeisHandler(_acesso, _autorizacao, query).Handle(new ListarPapeisQuery(_appId), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
            await query.DidNotReceiveWithAnyArgs().ListarAsync(default, default);
        }

        // ---- GET /pessoas ----

        private sealed class UsuarioFalso(bool ehMor) : IUsuarioAtual
        {
            public string Id => "sub-mor";
            public string Usuario => "u";
            public string Nome => "N";
            public string Email => "n@x.com";
            public IReadOnlyList<string> Perfis => [];
            public bool EhMor => ehMor;
        }

        [Fact, DisplayName("Critério 13: GET /pessoas só para o MOR — os demais recebem acesso.papel-insuficiente (403) sem consulta")]
        public async Task PessoasSoMor()
        {
            var query = Substitute.For<IPessoaQuery>();

            var r = await new ListarPessoasHandler(query, new UsuarioFalso(false)).Handle(new ListarPessoasQuery(new PaginacaoDto(), "ana"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            await query.DidNotReceiveWithAnyArgs().ListarAsync(default!, default, default);
        }

        [Fact, DisplayName("GET /pessoas do MOR repassa paginação e busca à leitura")]
        public async Task PessoasMor()
        {
            var query = Substitute.For<IPessoaQuery>();
            var pagina = new PaginaDto<PessoaListaDto> { Itens = [], Pagina = 1, TamanhoPagina = 10, Total = 0 };
            var paginacao = new PaginacaoDto();
            query.ListarAsync(paginacao, "ana", Arg.Any<CancellationToken>()).Returns(Result<PaginaDto<PessoaListaDto>>.Sucesso(pagina));

            var r = await new ListarPessoasHandler(query, new UsuarioFalso(true)).Handle(new ListarPessoasQuery(paginacao, "ana"), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeSameAs(pagina);
        }
    }
}
