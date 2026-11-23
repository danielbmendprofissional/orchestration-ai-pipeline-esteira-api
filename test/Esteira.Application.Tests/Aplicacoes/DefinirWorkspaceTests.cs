using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.DefinirWorkspace;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // P1.2: PUT /aplicacoes/{id}/workspace (analista ou MOR) — critérios 1 e 2.
    [Trait("Fatia", "P1")]
    public class DefinirWorkspaceTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _repo = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;

        public DefinirWorkspaceTests()
        {
            _acesso.ResolverIdAsync(_app.PublicId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _repo.ObterAsync(5, Arg.Any<CancellationToken>()).Returns(_app);
            Gitlab(new ProjetoDoGrupoGitLabDto(900, "valuto/valuto-workspace", "valuto-workspace", "main", true));
        }

        private void Gitlab(ProjetoDoGrupoGitLabDto? projeto)
            => _gitlab.ObterProjetoAsync("valuto", 900, Arg.Any<CancellationToken>()).Returns(Result<ProjetoDoGrupoGitLabDto?>.Sucesso(projeto));

        private Task<Result> Executar(long? projetoId = 900, Guid? id = null)
            => new DefinirWorkspaceHandler(_acesso, _autorizacao, _repo, _projetos, _gitlab, _uow, _auditoria)
                .Handle(new DefinirWorkspaceCommand(id ?? _app.PublicId, projetoId), Ct);

        [Fact, DisplayName("Critério 1: guarda id e caminho do projeto e audita antes/depois na mesma transação")]
        public async Task Guarda_E_Audita()
        {
            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            (_app.WorkspaceGitLabId, _app.WorkspaceGitLabCaminho).Should().Be((900L, "valuto/valuto-workspace"));
            _auditoria.Received(1).Registrar("Aplicacao", _app.PublicId, AcaoAuditoria.Alterou,
                Arg.Is<object?>(o => o != null && o.ToString()!.Contains("WorkspaceGitLabId = ") && o.ToString()!.Contains("WorkspaceGitLabCaminho = ")),
                Arg.Is<object?>(o => o != null && o.ToString()!.Contains("900") && o.ToString()!.Contains("valuto/valuto-workspace")));
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 1: null limpa o workspace e audita")]
        public async Task Limpa()
        {
            _app.DefinirWorkspace(900, "valuto/valuto-workspace");

            var r = await Executar(projetoId: null);

            r.IsSuccess.Should().BeTrue();
            (_app.WorkspaceGitLabId, _app.WorkspaceGitLabCaminho).Should().Be(((long?)null, (string?)null));
            _auditoria.Received(1).Registrar("Aplicacao", _app.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null));
            await _gitlab.DidNotReceiveWithAnyArgs().ObterProjetoAsync(default!, default);
        }

        [Fact, DisplayName("Critério 2: projeto fora do grupo da Aplicação é recusado (aplicacao.workspace-fora-do-grupo)")]
        public async Task ForaDoGrupo()
        {
            Gitlab(new ProjetoDoGrupoGitLabDto(900, "outro/projeto", "projeto", "main", false));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.workspace-fora-do-grupo");
            _app.WorkspaceGitLabId.Should().BeNull();
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 2: projeto já cadastrado como Projeto é recusado (aplicacao.workspace-e-projeto)")]
        public async Task EProjeto()
        {
            _projetos.ProjetoGitLabCadastradoAsync(900, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.workspace-e-projeto");
            _app.WorkspaceGitLabId.Should().BeNull();
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
        }

        [Fact, DisplayName("Projeto inexistente no GitLab devolve erro Validation estável (aplicacao.workspace-nao-encontrado)")]
        public async Task Inexistente()
        {
            Gitlab(null);

            var r = await Executar();

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("aplicacao.workspace-nao-encontrado", TipoErro.Validation));
            _app.WorkspaceGitLabId.Should().BeNull();
        }

        [Fact, DisplayName("GitLab fora do ar propaga o erro da porta, sem alterar nada")]
        public async Task GitLabFora()
        {
            _gitlab.ObterProjetoAsync("valuto", 900, Arg.Any<CancellationToken>())
                .Returns(Result<ProjetoDoGrupoGitLabDto?>.Falha(new Erro("gitlab.indisponivel", "fora", TipoErro.Failure)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
            _app.WorkspaceGitLabId.Should().BeNull();
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização (404 sem papel, 403 papel insuficiente)")]
        public async Task SemPermissao()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            _app.WorkspaceGitLabId.Should().BeNull();
            await _gitlab.DidNotReceiveWithAnyArgs().ObterProjetoAsync(default!, default);
        }

        [Fact, DisplayName("Id inexistente responde aplicacao.nao-encontrada")]
        public async Task AplicacaoInexistente()
        {
            var r = await Executar(id: Guid.NewGuid());

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }
    }
}
