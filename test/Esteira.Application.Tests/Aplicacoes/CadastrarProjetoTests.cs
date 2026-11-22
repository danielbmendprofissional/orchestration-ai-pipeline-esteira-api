using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.CadastrarProjeto;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.4, critérios 6, 7 e 8: cadastrar um Projeto do grupo na Aplicação, com os 3 Ambientes.
    [Trait("Fatia", "A1")]
    public class CadastrarProjetoTests : CadastroTestBase
    {
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAplicacaoRepositorio _apps = Substitute.For<IAplicacaoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;

        public CadastrarProjetoTests()
        {
            _acesso.ResolverIdAsync(_app.PublicId, Arg.Any<CancellationToken>()).Returns(AplicacaoId);
            _apps.ObterAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(_app);
            _gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso([new ProjetoGitLabDto(501, "valuto/api", "api", "main")]));
        }

        private Task<Result<ProjetoResumoDto>> Executar(long? gitlabId = 501, string stack = "dotnet")
            => new CadastrarProjetoHandler(_acesso, Autorizacao, _apps, Projetos, _gitlab, Uow, Auditoria)
                .Handle(new CadastrarProjetoCommand(_app.PublicId, gitlabId, stack), Ct);

        [Fact, DisplayName("Critérios 6 e 8: cadastra o Projeto do grupo com a stack escolhida e cria qa, hml e producao habilitados")]
        public async Task Cadastra()
        {
            ProjetoEntity? criado = null;
            await Projetos.AdicionarAsync(Arg.Do<ProjetoEntity>(p => { criado = p; DefinirId(p, 77); }), Arg.Any<CancellationToken>());
            var ambientes = new List<AmbienteProjetoEntity>();
            await Projetos.AdicionarAmbienteAsync(Arg.Do<AmbienteProjetoEntity>(ambientes.Add), Arg.Any<CancellationToken>());

            var r = await Executar(stack: "angular");

            r.IsSuccess.Should().BeTrue();
            (criado!.AplicacaoId, criado.ProjetoGitLabId, criado.Caminho, criado.Nome, criado.Stack, criado.Ativo)
                .Should().Be((AplicacaoId, 501L, "valuto/api", "api", Stack.Angular, true));
            r.Value.Id.Should().Be(criado.PublicId);
            ambientes.Select(a => a.Ambiente).Should().BeEquivalentTo([AmbienteDeploy.Qa, AmbienteDeploy.Hml, AmbienteDeploy.Producao]);
            ambientes.Should().OnlyContain(a => a.Habilitado && a.ProjetoId == 77);
            Auditoria.Received(1).Registrar("Projeto", criado.PublicId, AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null));
        }

        [Fact, DisplayName("Critério 6: projeto que não é do grupo da Aplicação é recusado (projeto.fora-do-grupo)")]
        public async Task ForaDoGrupo()
        {
            var r = await Executar(gitlabId: 999);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.fora-do-grupo");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            Auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
        }

        [Fact, DisplayName("Critério 7: projeto já cadastrado em qualquer Aplicação é recusado (projeto.duplicado)")]
        public async Task Duplicado()
        {
            Projetos.ProjetoGitLabCadastradoAsync(501, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.duplicado");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Critério 7: corrida pega pelo índice único vira projeto.duplicado")]
        public async Task Corrida()
        {
            Uow.SaveChangesAsync().Returns<int>(_ => throw new ViolacaoDeUnicidadeException("IX_Projeto_ProjetoGitLabId"));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.duplicado");
        }

        [Fact, DisplayName("Falha do GitLab ao listar o grupo devolve o erro dele")]
        public async Task GitLabFalha()
        {
            _gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(new Erro("gitlab.indisponivel", "x", TipoErro.Failure)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização, sem consultar o GitLab")]
        public async Task SemPermissao()
        {
            Autorizacao.AutorizarAsync(AplicacaoId, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            await _gitlab.DidNotReceiveWithAnyArgs().ListarProjetosDoGrupoAsync(default!, default, default);
        }

        [Fact, DisplayName("Aplicação inexistente responde aplicacao.nao-encontrada")]
        public async Task Inexistente()
        {
            var r = await new CadastrarProjetoHandler(_acesso, Autorizacao, _apps, Projetos, _gitlab, Uow, Auditoria)
                .Handle(new CadastrarProjetoCommand(Guid.NewGuid(), 501, "dotnet"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }

        [Theory, DisplayName("Validação: projetoGitLabId e stack (dotnet, angular, outro) são obrigatórios")]
        [InlineData(null, "dotnet", "projeto.gitlab-id-obrigatorio")]
        [InlineData(5L, "cobol", "projeto.stack-invalida")]
        [InlineData(5L, "", "projeto.stack-invalida")]
        public void Validacao(long? id, string stack, string codigo)
        {
            var r = new CadastrarProjetoValidator().Validate(new CadastrarProjetoCommand(Guid.NewGuid(), id, stack));

            r.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }
    }
}
