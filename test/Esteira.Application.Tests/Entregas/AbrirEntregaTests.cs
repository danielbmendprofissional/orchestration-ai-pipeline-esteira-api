using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Entregas.Commands.Abrir;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Entregas
{
    // E1.2 (spec 01-entrega-e-fluxo.md, critérios 1 e 2): AbrirEntregaCommand com papéis.
    [Trait("Fatia", "E1")]
    public class AbrirEntregaTests
    {
        private const long AplicacaoId = 7;
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _aplicacoes = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IPessoaRepositorio _pessoas = Substitute.For<IPessoaRepositorio>();
        private readonly IEntregaRepositorio _entregas = Substitute.For<IEntregaRepositorio>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly Guid _aplicacaoPublicId = Guid.NewGuid();
        private readonly ProjetoEntity _api = NovoProjeto(10);
        private readonly ProjetoEntity _front = NovoProjeto(11);

        public AbrirEntregaTests()
        {
            var aplicacao = DefinirId(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, AplicacaoId);
            var pessoa = DefinirId(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            _usuario.Id.Returns("sub-1");
            _acesso.ResolverIdAsync(_aplicacaoPublicId, Arg.Any<CancellationToken>()).Returns(AplicacaoId);
            _aplicacoes.ObterAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(aplicacao);
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns(pessoa);
            foreach (var p in new[] { _api, _front })
            {
                _projetos.ObterPorPublicIdAsync(p.PublicId, Arg.Any<CancellationToken>()).Returns(p);
                Qa(p, true);
            }
            ComAcesso(false, PapelAplicacao.Membro);
        }

        private static ProjetoEntity NovoProjeto(long id)
            => DefinirId(ProjetoEntity.Criar(AplicacaoId, 100 + id, $"g/p{id}", $"p{id}", Stack.Dotnet), id);

        private static T DefinirId<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private void ComAcesso(bool mor, params PapelAplicacao[] papeis)
            => _autorizacao.ObterAcessoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(mor, papeis));

        private void Qa(ProjetoEntity p, bool habilitado)
            => _projetos.ObterAmbienteAsync(p.Id, AmbienteDeploy.Qa, Arg.Any<CancellationToken>())
                .Returns(AmbienteProjetoEntity.Criar(p.Id, AmbienteDeploy.Qa, habilitado).Value);

        private AbrirEntregaHandler Handler()
            => new(_acesso, _autorizacao, _aplicacoes, _projetos, _pessoas, _entregas, _usuario, new FakeRelogio(T0));

        private AbrirEntregaCommand Comando() => new(_aplicacaoPublicId,
        [
            new FluxoParaAbrir(_api.PublicId, TipoMudanca.Minor, [new FatiaParaAbrir("B2", true), new FatiaParaAbrir("B3", false)]),
            new FluxoParaAbrir(_front.PublicId, TipoMudanca.Major, [new FatiaParaAbrir("B2", true)])
        ]);

        [Theory, DisplayName("1: Membro ou MOR abre a Entrega aberta com um Fluxo por Projeto, cada um com suas Fatias e o TipoMudanca")]
        [InlineData(false, PapelAplicacao.Membro)]
        [InlineData(true, PapelAplicacao.Publicador)]
        public async Task AbreComPapel(bool mor, PapelAplicacao papel)
        {
            ComAcesso(mor, papel);
            EntregaEntity? gravada = null;
            await _entregas.AdicionarAsync(Arg.Do<EntregaEntity>(e => gravada = e), Arg.Any<CancellationToken>());

            var r = await Handler().Handle(Comando(), Ct);

            r.IsSuccess.Should().BeTrue();
            gravada.Should().NotBeNull();
            r.Value.Should().Be(gravada!.PublicId);
            gravada.Situacao.Should().Be(SituacaoEntrega.Aberta);
            gravada.AplicacaoId.Should().Be(AplicacaoId);
            gravada.AbertaPorPessoaId.Should().Be(3);
            gravada.AbertaEm.Should().Be(T0);
            gravada.Fluxos.Should().HaveCount(2);
            var fluxoApi = gravada.Fluxos.Single(f => f.ProjetoId == _api.Id);
            fluxoApi.TipoMudanca.Should().Be(TipoMudanca.Minor);
            fluxoApi.Fatias.Select(f => (f.CodigoFatia, f.ComFlag)).Should().BeEquivalentTo([("B2", true), ("B3", false)]);
            var fluxoFront = gravada.Fluxos.Single(f => f.ProjetoId == _front.Id);
            fluxoFront.TipoMudanca.Should().Be(TipoMudanca.Major);
            fluxoFront.Fatias.Should().ContainSingle().Which.CodigoFatia.Should().Be("B2");
        }

        [Theory, DisplayName("2: sem Membro (nem MOR) recusa com entrega.sem-permissao e não grava nada")]
        [InlineData(PapelAplicacao.Analista)]
        [InlineData(PapelAplicacao.AprovadorTecnico)]
        [InlineData(PapelAplicacao.AprovadorNegocio)]
        [InlineData(PapelAplicacao.Publicador)]
        public async Task RecusaSemPapel(PapelAplicacao papel)
        {
            ComAcesso(false, papel);
            await RecusaSemGravar();
        }

        [Fact, DisplayName("2: pessoa sem nenhum papel na Aplicação também recebe entrega.sem-permissao")]
        public async Task RecusaSemNenhumPapel()
        {
            ComAcesso(false);
            await RecusaSemGravar();
        }

        private async Task RecusaSemGravar()
        {
            var r = await Handler().Handle(Comando(), Ct);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "entrega.sem-permissao" && e.Type == TipoErro.Forbidden);
            await _entregas.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Aplicação desconhecida devolve aplicacao.nao-encontrada e não grava")]
        public async Task AplicacaoDesconhecida()
        {
            var r = await Handler().Handle(Comando() with { AplicacaoId = Guid.NewGuid() }, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.nao-encontrada");
            await _entregas.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Projeto desconhecido devolve entrega.projeto-invalido e não grava")]
        public async Task ProjetoDesconhecido()
        {
            var cmd = new AbrirEntregaCommand(_aplicacaoPublicId, [new FluxoParaAbrir(Guid.NewGuid(), TipoMudanca.Minor, [new FatiaParaAbrir("B2", true)])]);

            var r = await Handler().Handle(cmd, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("entrega.projeto-invalido");
            await _entregas.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        [Fact, DisplayName("Erro do domínio propaga como Result sem reescrita (qa desabilitado) e não grava")]
        public async Task ErroDoDominioPropaga()
        {
            Qa(_front, false);

            var r = await Handler().Handle(Comando(), Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.desabilitado");
            await _entregas.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        }

        private sealed class FakeRelogio(DateTime agora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(agora, TimeSpan.Zero);
        }
    }
}
