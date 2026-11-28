using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Entregas.Queries.Obter;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Entregas
{
    // E1b.4 (spec 02-tela-da-entrega.md, critérios 12 e 13): GET /entregas/{id} autorizado por Aplicação, 404 uniforme, pipelineUrl do servidor.
    [Trait("Fatia", "E1b")]
    public class ObterEntregaTests
    {
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IEntregaQuery _query = Substitute.For<IEntregaQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IUrlDoGitLab _gitlab = Substitute.For<IUrlDoGitLab>();
        private readonly EntregaEntity _entrega;
        private readonly Guid _appPublicId = Guid.NewGuid();

        public ObterEntregaTests()
        {
            _gitlab.Base.Returns("https://gitlab.exemplo.com/");
            _entrega = NovaEntrega("grupo/api");
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, [PapelAplicacao.Membro]));
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private static EntregaEntity NovaEntrega(string caminho)
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, caminho, "api", Stack.Dotnet), 10);
            return Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true), new FatiaDaEntrega("B2", false)])], T0).Value, 20);
        }

        private EntregaParaLeitura Leitura(EntregaEntity entrega, string caminho = "grupo/api") => new(
            entrega, _appPublicId, "App", "Fulano", [new ProjetoDoFluxo(10, Guid.NewGuid(), "api", caminho)]);

        private ObterEntregaHandler Handler() => new(_query, _autorizacao, _gitlab);

        [Fact, DisplayName("Entrega inexistente responde entrega.nao-encontrada sem consultar permissão")]
        public async Task Inexistente()
        {
            _query.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EntregaParaLeitura?)null);

            var r = await Handler().Handle(new ObterEntregaQuery(Guid.NewGuid()), Ct);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Be(EntregaErros.NaoEncontrada);
            EntregaErros.NaoEncontrada.Code.Should().Be("entrega.nao-encontrada");
        }

        [Fact, DisplayName("Sem papel na Aplicação (nem MOR) responde o mesmo erro da Entrega inexistente")]
        public async Task SemAcesso()
        {
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, []));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
        }

        [Fact, DisplayName("MOR sem papel enxerga a Entrega")]
        public async Task Mor()
        {
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(true, []));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Com acesso devolve Aplicação, abertura, estado, Fluxo com Fatias e as 4 Etapas em ordem")]
        public async Task ComAcesso()
        {
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            var dto = r.Value;
            dto.Id.Should().Be(_entrega.PublicId);
            dto.Aplicacao.Id.Should().Be(_appPublicId);
            dto.Aplicacao.Nome.Should().Be("App");
            dto.AbertaPor.Nome.Should().Be("Fulano");
            dto.AbertaEm.Should().Be(T0);
            dto.Estado.Should().Be("em-andamento");
            dto.VezDe.Should().BeNull();
            var fluxo = dto.Fluxos.Should().ContainSingle().Subject;
            fluxo.Id.Should().Be(_entrega.Fluxos[0].PublicId);
            fluxo.Projeto.Nome.Should().Be("api");
            fluxo.TipoMudanca.Should().Be("minor");
            fluxo.Sha.Should().BeNull();
            fluxo.Fatias.Select(f => (f.Codigo, f.ComFlag)).Should().BeEquivalentTo([("B3", true), ("B2", false)]);
            fluxo.Etapas.Select(e => (e.Etapa, e.Estado)).Should().Equal(
                ("abertura", "concluido"), ("qa", "nao-iniciado"), ("hml", "nao-iniciado"), ("prd", "nao-iniciado"));
            fluxo.Etapas.Should().OnlyContain(e => e.PipelineUrl == null && e.Pulada == null && e.CodigoFalha == null);
        }

        [Fact, DisplayName("Estado da Entrega aguardando aprovação traz vezDe aprovador-tecnico")]
        public async Task EstadoEVezDe()
        {
            _entrega.Fluxos[0].Etapa(EtapaDoFluxo.Qa).IniciarExecucao(4711, T0.AddMinutes(1));
            _entrega.Fluxos[0].Etapa(EtapaDoFluxo.Qa).AplicarStatusDaPipeline("success", T0.AddMinutes(2));
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            r.Value.Estado.Should().Be("aguardando-aprovacao");
            r.Value.VezDe.Should().Be("aprovador-tecnico");
            r.Value.Fluxos[0].Etapas.Single(e => e.Etapa == "qa").Estado.Should().Be("aguardando-aprovacao");
        }

        [Fact, DisplayName("pipelineUrl é montada no servidor: GitLab:Url + caminho do Projeto + /-/pipelines/<id>")]
        public async Task PipelineUrl()
        {
            _entrega.Fluxos[0].Etapa(EtapaDoFluxo.Qa).IniciarExecucao(4711, T0.AddMinutes(1));
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            r.Value.Fluxos[0].Etapas.Single(e => e.Etapa == "qa").PipelineUrl.Should().Be("https://gitlab.exemplo.com/grupo/api/-/pipelines/4711");
            r.Value.Fluxos[0].Etapas.Single(e => e.Etapa == "hml").PipelineUrl.Should().BeNull();
        }

        [Fact, DisplayName("pipelineUrl escapa cada segmento do caminho")]
        public async Task PipelineUrlEscapa()
        {
            var entrega = NovaEntrega("grupo/a b?x#y");
            entrega.Fluxos[0].Etapa(EtapaDoFluxo.Qa).IniciarExecucao(5, T0.AddMinutes(1));
            _query.ObterAsync(entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(entrega, "grupo/a b?x#y"));

            var r = await Handler().Handle(new ObterEntregaQuery(entrega.PublicId), Ct);

            r.Value.Fluxos[0].Etapas.Single(e => e.Etapa == "qa").PipelineUrl.Should().Be("https://gitlab.exemplo.com/grupo/a%20b%3Fx%23y/-/pipelines/5");
        }

        [Theory, DisplayName("GitLab:Url ausente, relativa ou de esquema não http(s) não gera pipelineUrl")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("gitlab.exemplo.com")]
        [InlineData("javascript:alert(1)")]
        [InlineData("ftp://gitlab.exemplo.com")]
        public async Task PipelineUrlSemBaseValida(string? baseUrl)
        {
            _gitlab.Base.Returns(baseUrl);
            _entrega.Fluxos[0].Etapa(EtapaDoFluxo.Qa).IniciarExecucao(4711, T0.AddMinutes(1));
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            r.Value.Fluxos[0].Etapas.Should().OnlyContain(e => e.PipelineUrl == null);
        }

        [Fact, DisplayName("Etapa pulada traz por e motivo; a falha traz o código")]
        public async Task PuladaEFalha()
        {
            var fluxo = _entrega.Fluxos[0];
            fluxo.Etapa(EtapaDoFluxo.Qa).Pular("ana", "urgente", T0.AddMinutes(1));
            fluxo.Etapa(EtapaDoFluxo.Prd).IniciarExecucao(9, T0.AddMinutes(1));
            fluxo.Etapa(EtapaDoFluxo.Prd).AplicarStatusDaPipeline("failed", T0.AddMinutes(2));
            _query.ObterAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(Leitura(_entrega));

            var r = await Handler().Handle(new ObterEntregaQuery(_entrega.PublicId), Ct);

            var etapas = r.Value.Fluxos[0].Etapas;
            etapas.Single(e => e.Etapa == "qa").Pulada.Should().BeEquivalentTo(new { Por = "ana", Motivo = "urgente" });
            etapas.Single(e => e.Etapa == "prd").CodigoFalha.Should().Be("pipeline.falhou");
            r.Value.Estado.Should().Be("bloqueada-ou-falha");
        }
    }
}
