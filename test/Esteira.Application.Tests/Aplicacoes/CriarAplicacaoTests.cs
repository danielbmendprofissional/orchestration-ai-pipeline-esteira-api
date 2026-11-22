using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.Criar;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.3, critérios 1 a 4: cadastrar a Aplicação a partir de um grupo do GitLab.
    [Trait("Fatia", "A1")]
    public class CriarAplicacaoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IAplicacaoRepositorio _repo = Substitute.For<IAplicacaoRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();

        public CriarAplicacaoTests()
        {
            _autorizacao.PodeCriar().Returns(true);
            _gitlab.ObterGrupoAsync("valuto", Arg.Any<CancellationToken>())
                .Returns(Result<GrupoGitLabDto>.Sucesso(new GrupoGitLabDto(42, "valuto", "Valuto")));
        }

        private Task<Result<AplicacaoListaDto>> Executar(string nome = "Valuto", string sigla = " vlt ", string grupo = "valuto")
            => new CriarAplicacaoHandler(_autorizacao, _gitlab, _repo, _uow, _auditoria)
                .Handle(new CriarAplicacaoCommand(nome, sigla, grupo), Ct);

        [Fact, DisplayName("Critério 1: guarda o ID do grupo, normaliza a sigla e cria as 4 políticas padrão")]
        public async Task Cria_ComGrupoExistente()
        {
            AplicacaoEntity? gravada = null;
            await _repo.AdicionarAsync(Arg.Do<AplicacaoEntity>(a => gravada = a), Arg.Any<CancellationToken>());
            var politicas = new List<PoliticaSeveridadeEntity>();
            await _repo.AdicionarPoliticaAsync(Arg.Do<PoliticaSeveridadeEntity>(politicas.Add), Arg.Any<CancellationToken>());

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            gravada!.GrupoGitLabId.Should().Be(42);
            gravada.GrupoGitLabCaminho.Should().Be("valuto");
            gravada.Sigla.Should().Be("VLT");
            r.Value.Id.Should().Be(gravada.PublicId);
            r.Value.Sigla.Should().Be("VLT");
            politicas.Select(p => p.Ambiente).Should().BeEquivalentTo(
                [AmbientePolitica.Review, AmbientePolitica.Qa, AmbientePolitica.Hml, AmbientePolitica.Producao]);
            politicas.Single(p => p.Ambiente == AmbientePolitica.Review).Modo.Should().Be(ModoPolitica.Avisa);
            politicas.Where(p => p.Ambiente != AmbientePolitica.Review)
                .Should().OnlyContain(p => p.Modo == ModoPolitica.Bloqueia && p.Critica == 0 && p.Alta == 0 && p.Media == null && p.Baixa == null);
        }

        [Fact, DisplayName("Critério 14: registra a criação na auditoria, sem estado anterior")]
        public async Task Cria_Audita()
        {
            AplicacaoEntity? gravada = null;
            await _repo.AdicionarAsync(Arg.Do<AplicacaoEntity>(a => gravada = a), Arg.Any<CancellationToken>());

            await Executar();

            _auditoria.Received(1).Registrar("Aplicacao", gravada!.PublicId, AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null));
        }

        [Fact, DisplayName("Critério 2: recusa sigla repetida (aplicacao.sigla-duplicada) sem consultar o GitLab")]
        public async Task SiglaDuplicada()
        {
            _repo.SiglaExisteAsync("VLT", null, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "aplicacao.sigla-duplicada" && e.Type == TipoErro.Conflict);
            await _gitlab.DidNotReceiveWithAnyArgs().ObterGrupoAsync(default!, default);
            await _repo.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Critério 3: recusa grupo já vinculado a outra Aplicação (aplicacao.grupo-ja-vinculado)")]
        public async Task GrupoJaVinculado()
        {
            _repo.GrupoVinculadoAsync(42, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "aplicacao.grupo-ja-vinculado" && e.Type == TipoErro.Conflict);
            await _repo.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Critério 4: repassa gitlab.grupo-nao-encontrado")]
        public async Task GrupoNaoEncontrado()
        {
            _gitlab.ObterGrupoAsync("x", Arg.Any<CancellationToken>())
                .Returns(Result<GrupoGitLabDto>.Falha(new Erro("gitlab.grupo-nao-encontrado", "m", TipoErro.Failure)));

            var r = await Executar(grupo: "x");

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.grupo-nao-encontrado");
            await _repo.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Critério 4: repassa gitlab.bot-nao-configurado quando falta o token do grupo")]
        public async Task BotNaoConfigurado()
        {
            _gitlab.ObterGrupoAsync("x", Arg.Any<CancellationToken>())
                .Returns(Result<GrupoGitLabDto>.Falha(new Erro("gitlab.bot-nao-configurado", "m", TipoErro.Failure)));

            var r = await Executar(grupo: "x");

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
        }

        [Fact, DisplayName("Só o MOR cria: os demais recebem 403 acesso.papel-insuficiente")]
        public async Task SoMor()
        {
            _autorizacao.PodeCriar().Returns(false);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acesso.papel-insuficiente");
            await _gitlab.DidNotReceiveWithAnyArgs().ObterGrupoAsync(default!, default);
        }

        [Theory, DisplayName("Corrida: violação de unicidade do banco vira o mesmo erro da verificação prévia")]
        [InlineData("IX_Aplicacao_Sigla", "aplicacao.sigla-duplicada")]
        [InlineData("IX_Aplicacao_GrupoGitLabId", "aplicacao.grupo-ja-vinculado")]
        public async Task Corrida(string restricao, string codigo)
        {
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ViolacaoDeUnicidadeException(restricao));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == codigo && e.Type == TipoErro.Conflict);
        }

        [Theory, DisplayName("Validação: nome, sigla e grupo são obrigatórios, com code estável")]
        [InlineData("", "VLT", "valuto", "aplicacao.nome-obrigatorio")]
        [InlineData("Valuto", " ", "valuto", "aplicacao.sigla-obrigatoria")]
        [InlineData("Valuto", "VLT", "", "aplicacao.grupo-obrigatorio")]
        public void Validacao(string nome, string sigla, string grupo, string codigo)
        {
            var r = new CriarAplicacaoValidator().Validate(new CriarAplicacaoCommand(nome, sigla, grupo));

            r.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }
    }
}
