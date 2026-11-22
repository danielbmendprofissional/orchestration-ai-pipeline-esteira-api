using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Queries.ListarProjetosGitLab;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.4, critério 5: lista de projetos do grupo, marcando os já cadastrados e sugerindo a stack pelo APP_TECHNOLOGY.
    [Trait("Fatia", "A1")]
    public class ProjetosDoGitLabTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _apps = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;

        public ProjetosDoGitLabTests()
        {
            _acesso.ResolverIdAsync(_app.PublicId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _apps.ObterAsync(5, Arg.Any<CancellationToken>()).Returns(_app);
            _projetos.ProjetosGitLabCadastradosAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
                .Returns(new HashSet<long>());
        }

        private void GrupoTem(params ProjetoGitLabDto[] projetos)
            => _gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso(projetos));

        private void Arquivo(long projetoId, string? conteudo, string referencia = "main")
            => _gitlab.ObterArquivoAsync("valuto", projetoId, ".gitlab-ci.yml", referencia, Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(conteudo));

        private Task<Result<IReadOnlyList<ProjetoGitLabItemDto>>> Executar()
            => new ListarProjetosGitLabHandler(_acesso, _autorizacao, _apps, _projetos, _gitlab)
                .Handle(new ListarProjetosGitLabQuery(_app.PublicId), Ct);

        [Fact, DisplayName("Critério 5: marca os projetos já cadastrados (em qualquer Aplicação) e sugere dotnet/angular pelo APP_TECHNOLOGY")]
        public async Task Marca_E_Sugere()
        {
            GrupoTem(
                new ProjetoGitLabDto(1, "valuto/api", "api", "main"),
                new ProjetoGitLabDto(2, "valuto/web", "web", "main"));
            Arquivo(1, "variables:\n  APP_TECHNOLOGY: dotnet\n");
            Arquivo(2, "variables:\r\n  APP_TECHNOLOGY: \"Angular\"  # front\r\n");
            _projetos.ProjetosGitLabCadastradosAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
                .Returns(new HashSet<long> { 2 });

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            var itens = r.Value;
            itens.Select(i => (i.ProjetoGitLabId, i.Caminho, i.Nome, i.Cadastrado, i.StackSugerida))
                .Should().Equal((1L, "valuto/api", "api", false, "dotnet"), (2L, "valuto/web", "web", true, "angular"));
        }

        [Theory, DisplayName("Critério 5: outro valor, ausência ou comentário = sem sugestão")]
        [InlineData("variables:\n  APP_TECHNOLOGY: python\n")]
        [InlineData("variables:\n  OUTRA: dotnet\n")]
        [InlineData("# APP_TECHNOLOGY: dotnet\n")]
        [InlineData("")]
        [InlineData(null)]
        public async Task SemSugestao(string? conteudo)
        {
            GrupoTem(new ProjetoGitLabDto(1, "valuto/api", "api", "main"));
            Arquivo(1, conteudo);

            var r = await Executar();

            r.Value.Should().ContainSingle().Which.StackSugerida.Should().BeNull();
        }

        [Fact, DisplayName("Critério 5: falha ao ler o arquivo de um projeto não derruba a lista")]
        public async Task FalhaNoArquivo()
        {
            GrupoTem(new ProjetoGitLabDto(1, "valuto/api", "api", "main"), new ProjetoGitLabDto(2, "valuto/web", "web", "main"));
            _gitlab.ObterArquivoAsync("valuto", 1, ".gitlab-ci.yml", "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Falha(new Erro("gitlab.indisponivel", "x", TipoErro.Failure)));
            Arquivo(2, "APP_TECHNOLOGY: angular");

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            r.Value.Select(i => i.StackSugerida).Should().Equal(null, "angular");
        }

        [Fact, DisplayName("Critério 5: lê o arquivo na branch padrão do projeto (HEAD quando o GitLab não informa)")]
        public async Task BranchPadrao()
        {
            GrupoTem(new ProjetoGitLabDto(1, "valuto/api", "api", "develop"), new ProjetoGitLabDto(2, "valuto/web", "web"));
            Arquivo(1, "APP_TECHNOLOGY: dotnet", "develop");
            Arquivo(2, "APP_TECHNOLOGY: angular", "HEAD");

            var r = await Executar();

            r.Value.Select(i => i.StackSugerida).Should().Equal("dotnet", "angular");
        }

        [Fact, DisplayName("Falha ao listar o grupo devolve o erro do GitLab")]
        public async Task FalhaNaLista()
        {
            _gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(new Erro("gitlab.bot-nao-configurado", "x", TipoErro.Failure)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
        }

        [Fact, DisplayName("Sem papel de analista/MOR: devolve o erro da autorização e não chama o GitLab")]
        public async Task SemPermissao()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            await _gitlab.DidNotReceiveWithAnyArgs().ListarProjetosDoGrupoAsync(default!, default, default);
        }

        [Fact, DisplayName("Aplicação inexistente responde aplicacao.nao-encontrada")]
        public async Task Inexistente()
        {
            var r = await new ListarProjetosGitLabHandler(_acesso, _autorizacao, _apps, _projetos, _gitlab)
                .Handle(new ListarProjetosGitLabQuery(Guid.NewGuid()), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }

        [Theory, DisplayName("APP_TECHNOLOGY: leitura por regex de linha (aspas, comentário, CRLF, maiúsculas)")]
        [InlineData("APP_TECHNOLOGY: dotnet", "dotnet")]
        [InlineData("  APP_TECHNOLOGY: 'dotnet'", "dotnet")]
        [InlineData("a: 1\r\n  APP_TECHNOLOGY: ANGULAR # x\r\nb: 2", "angular")]
        [InlineData("APP_TECHNOLOGY:    dotnet   ", "dotnet")]
        [InlineData("APP_TECHNOLOGY: dotnetcore", null)]
        [InlineData("XAPP_TECHNOLOGY: dotnet", null)]
        public void Parser(string conteudo, string? esperado)
        {
            StackSugerida.Ler(conteudo).Should().Be(esperado);
        }
    }
}
