using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.2, critérios 1, 5, 8 e 9: Abrir review. Handler orquestra; as regras de recusa vêm do estado fresco da Aplicação.
    [Trait("Fatia", "P2")]
    public class AbrirReviewTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IPainelAplicacaoQuery _apps = Substitute.For<IPainelAplicacaoQuery>();
        private readonly IPainelDeFatias _painel = Substitute.For<IPainelDeFatias>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly IPessoaRepositorio _pessoas = Substitute.For<IPessoaRepositorio>();
        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly List<AcaoBotEntity> _adicionadas = [];
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly AplicacaoNoPainel _app;
        private long _proximoId = 100;

        public AbrirReviewTests()
        {
            _app = new AplicacaoNoPainel(_appPublicId, "Valuto", "VAL", "g/valuto", 10, "g/valuto/ws",
                [new ProjetoNoPainel(RepositorioFatia.Api, 20, "g/valuto/api", 71), new ProjetoNoPainel(RepositorioFatia.Frontend, 30, "g/valuto/front", 72)]);
            _usuario.Id.Returns("sub-1");
            _acesso.ResolverIdAsync(_appPublicId, Arg.Any<CancellationToken>()).Returns(AppId);
            _autorizacao.AutorizarAsync(AppId, OperacaoAplicacao.AbrirReview, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _apps.ListarVisiveisAsync("sub-1", Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([_app]);
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns(Definir(PessoaEntity.Criar("sub-1", "Ana", "a@x.com", DateTime.UtcNow), 9));
            Fresco(Ok(Fatia("P2", EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 5), Mr(RepositorioFatia.Api, 3), Mr(RepositorioFatia.Api, 1, "merged"),
                Mr(RepositorioFatia.Frontend, 8))));
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns(Array.Empty<AcaoBotEntity>());
            _acoes.AdicionarAsync(Arg.Do<AcaoBotEntity>(a => _adicionadas.Add(a)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            _uow.SaveChangesAsync().Returns(_ =>
            {
                foreach (var a in _adicionadas.Where(a => a.Id == 0))
                    Definir(a, _proximoId++);
                return Task.FromResult(1);
            });
        }

        private static T Definir<T>(T e, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);
            return e;
        }

        private static MrDaGaveta Mr(RepositorioFatia repo, long iid, string estado = "opened", string? pipeline = "success")
            => new(repo, iid, "P2 — Review", estado, "feature/p2-x", pipeline);

        private static FatiaLida Fatia(string codigo, EstadoDaFatia estado, params MrDaGaveta[] mrs) => new(
            codigo, "Review", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x.md", 7,
            new ResultadoDeEstado(estado, null, false, false), false, new DetalheDaFatia(null, null, [], [], mrs));

        private static ResultadoDoPainel Ok(params FatiaLida[] fatias) => new("x", SituacaoDoPainel.Ok, fatias, DateTimeOffset.UtcNow, false, null);

        private void Fresco(ResultadoDoPainel r) => _painel.ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>()).Returns(r);

        private Task<Result<AcoesPedidasDto>> Executar(string codigo = "P2")
            => new AbrirReviewHandler(new PreparadorDeDecisao(_acesso, _autorizacao, _apps, _painel, _usuario),
                    _pessoas, _acoes, _uow, _auditoria, _publicador, TimeProvider.System)
                .Handle(new AbrirReviewCommand(_appPublicId, codigo), Ct);

        private void NadaGravado()
        {
            _acoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
            _publicador.DidNotReceiveWithAnyArgs().Publish(default(AcaoBotPedida)!, default);
            _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("1: grava uma Ação abrir-review pedida por repositório, na MR aberta mais antiga, audita e publica AcaoBotPedida")]
        public async Task Grava_audita_e_publica()
        {
            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Should().HaveCount(2);
            var api = _adicionadas.Single(a => a.ProjetoId == 71);
            var front = _adicionadas.Single(a => a.ProjetoId == 72);
            (api.MrIid, api.Tipo, api.Estado, api.PedidaPorPessoaId, api.AplicacaoId, api.FatiaCodigo)
                .Should().Be((3L, TipoAcaoBot.AbrirReview, EstadoAcaoBot.Pedida, 9L, AppId, "P2"));
            api.ChaveIdempotencia.Should().Be("5:P2:abrir-review:71:3:1");
            front.MrIid.Should().Be(8);
            r.Value.Acoes.Select(a => a.Repositorio).Should().BeEquivalentTo(["api", "frontend"]);
            _auditoria.Received(2).Registrar("AcaoBot", Arg.Any<Guid>(), AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null));
            await _publicador.Received(2).Publish(Arg.Any<AcaoBotPedida>(), Arg.Any<CancellationToken>());
            await _publicador.Received(1).Publish(
                Arg.Is<AcaoBotPedida>(m => m.AcaoId == api.PublicId && m.ChaveIdempotencia == api.ChaveIdempotencia), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("1: tudo na mesma transação: abre, grava e confirma; a Ação do front depende da Ação da API (API primeiro)")]
        public async Task Mesma_transacao_e_dependencia()
        {
            await Executar();

            Received.InOrder(() =>
            {
                _uow.BeginTransactionAsync();
                _uow.SaveChangesAsync(); // Id da Ação da API
                _uow.SaveChangesAsync();
                _uow.CommitAsync();
            });
            var api = _adicionadas.Single(a => a.ProjetoId == 71);
            _adicionadas.Single(a => a.ProjetoId == 72).DependeDeAcaoId.Should().Be(api.Id);
            api.DependeDeAcaoId.Should().BeNull();
        }

        [Fact, DisplayName("1: Fatia só de API recebe uma Ação só")]
        public async Task Fatia_so_de_api()
        {
            Fresco(Ok(Fatia("P2", EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 3))));

            var r = await Executar();

            r.Value.Acoes.Should().ContainSingle();
            _adicionadas.Should().ContainSingle().Which.DependeDeAcaoId.Should().BeNull();
        }

        [Theory, DisplayName("9: sem papel (404) ou sem a operação (403) recusa antes de ler o GitLab e de gravar")]
        [InlineData(TipoErro.NotFound, "aplicacao.nao-encontrada")]
        [InlineData(TipoErro.Forbidden, "acesso.papel-insuficiente")]
        public async Task Autorizacao_recusa(TipoErro tipo, string codigo)
        {
            _autorizacao.AutorizarAsync(AppId, OperacaoAplicacao.AbrirReview, Arg.Any<CancellationToken>()).Returns(Result.Falha(new Erro(codigo, "x", tipo)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            await _painel.DidNotReceiveWithAnyArgs().ObterFrescoAsync(default!, default);
            NadaGravado();
        }

        [Fact, DisplayName("5: código que não existe no mapa é recusado (fatia.nao-encontrada, 404)")]
        public async Task Fatia_inexistente()
        {
            var r = await Executar("ZZ9");

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "fatia.nao-encontrada" && e.Type == TipoErro.NotFound);
            NadaGravado();
        }

        [Theory, DisplayName("5: Fatia que não está Em revisão é recusada (fatia.sem-mr-aberta, 409)")]
        [InlineData(EstadoDaFatia.EmExecucao)]
        [InlineData(EstadoDaFatia.ProntaParaEntrega)]
        [InlineData(EstadoDaFatia.ComSpec)]
        public async Task Fora_de_revisao(EstadoDaFatia estado)
        {
            Fresco(Ok(Fatia("P2", estado, Mr(RepositorioFatia.Api, 3))));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "fatia.sem-mr-aberta" && e.Type == TipoErro.Conflict);
            NadaGravado();
        }

        [Fact, DisplayName("5: Em revisão mas sem MR aberta (só mergeada) é recusada (fatia.sem-mr-aberta)")]
        public async Task Sem_mr_aberta()
        {
            Fresco(Ok(Fatia("P2", EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 3, "merged"))));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("fatia.sem-mr-aberta");
            NadaGravado();
        }

        [Theory, DisplayName("8: GitLab fora ou sem token recusa e não grava; o código do GitLab é repassado")]
        [InlineData("gitlab.indisponivel")]
        [InlineData("gitlab.bot-nao-configurado")]
        public async Task Gitlab_fora(string codigo)
        {
            Fresco(new ResultadoDoPainel("x", SituacaoDoPainel.Falha, [], null, false, new Erro(codigo, "x", TipoErro.Failure)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            NadaGravado();
        }

        [Fact, DisplayName("8: decide sobre a leitura fresca, nunca sobre o instantâneo guardado")]
        public async Task Usa_leitura_fresca()
        {
            await Executar();

            await _painel.Received(1).ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>());
            await _painel.DidNotReceive().ObterAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("8: Aplicação sem workspace é recusada (painel.sem-workspace) sem gravar")]
        public async Task Sem_workspace()
        {
            Fresco(new ResultadoDoPainel("x", SituacaoDoPainel.SemWorkspace, [], null, false, null));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("painel.sem-workspace");
            NadaGravado();
        }

        [Theory, DisplayName("Regra da spec: review ainda vivo (pedida, executando) ou confirmado na MR recusa (fatia.review-ja-pedido, 409)")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Confirmada)]
        public async Task Review_ja_pedido(EstadoAcaoBot estado)
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns([AcaoExistente(71, 3, estado, 1)]);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "fatia.review-ja-pedido" && e.Type == TipoErro.Conflict);
            NadaGravado();
        }

        [Fact, DisplayName("Regra da spec: review que falhou ou foi cancelado pode ser pedido de novo, com a tentativa seguinte na chave")]
        public async Task Nova_tentativa_depois_de_falha()
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>())
                .Returns([AcaoExistente(71, 3, EstadoAcaoBot.Falhou, 1), AcaoExistente(71, 3, EstadoAcaoBot.Cancelada, 2)]);

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Single(a => a.ProjetoId == 71).ChaveIdempotencia.Should().Be("5:P2:abrir-review:71:3:3");
        }

        [Fact, DisplayName("Regra da spec: Ação de aprovar/reprovar na MR não conta para o review (só abrir-review)")]
        public async Task Outros_tipos_nao_contam()
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns([AcaoExistente(71, 3, EstadoAcaoBot.Falhou, 1, TipoAcaoBot.Aprovar)]);

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Single(a => a.ProjetoId == 71).ChaveIdempotencia.Should().EndWith(":1");
        }

        [Fact, DisplayName("Duplo clique: chave que já existe devolve o estado da primeira Ação sem gravar nem publicar de novo")]
        public async Task Chave_existente_devolve_a_primeira()
        {
            var primeira = AcaoExistente(71, 3, EstadoAcaoBot.Pedida, 1);
            _acoes.ObterPorChaveAsync("5:P2:abrir-review:71:3:1", Arg.Any<CancellationToken>()).Returns(primeira);
            _acoes.ObterPorChaveAsync("5:P2:abrir-review:72:8:1", Arg.Any<CancellationToken>()).Returns(AcaoExistente(72, 8, EstadoAcaoBot.Pedida, 1));

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Should().Contain(a => a.Id == primeira.PublicId);
            NadaGravado();
        }

        [Fact, DisplayName("Duplo clique concorrente: violação 23505 desfaz a transação e devolve a Ação que ganhou")]
        public async Task Violacao_de_unicidade_devolve_a_primeira()
        {
            var primeira = AcaoExistente(71, 3, EstadoAcaoBot.Pedida, 1);
            var primeiraFront = AcaoExistente(72, 8, EstadoAcaoBot.Pedida, 1);
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ViolacaoDeUnicidadeException("IX_AcaoBot_ChaveIdempotencia"));
            _acoes.ObterPorChaveAsync("5:P2:abrir-review:71:3:1", Arg.Any<CancellationToken>()).Returns((AcaoBotEntity?)null, primeira);
            _acoes.ObterPorChaveAsync("5:P2:abrir-review:72:8:1", Arg.Any<CancellationToken>()).Returns(primeiraFront);

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Select(a => a.Id).Should().BeEquivalentTo([primeira.PublicId, primeiraFront.PublicId]);
            await _uow.Received(1).RollbackAsync();
            await _uow.DidNotReceive().CommitAsync();
        }

        [Fact, DisplayName("Outra violação de unicidade (não é a chave) não é engolida")]
        public async Task Violacao_estranha_sobe()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ViolacaoDeUnicidadeException("IX_Outro"));

            var acao = async () => await Executar();

            await acao.Should().ThrowAsync<ViolacaoDeUnicidadeException>();
            await _uow.Received(1).RollbackAsync();
        }

        [Fact, DisplayName("Pessoa que ainda não entrou na Esteira não tem Ação gravada (acesso.pessoa-nao-registrada)")]
        public async Task Pessoa_nao_registrada()
        {
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns((PessoaEntity?)null);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acesso.pessoa-nao-registrada");
            NadaGravado();
        }

        private static AcaoBotEntity AcaoExistente(long projeto, long mr, EstadoAcaoBot estado, int tentativa, TipoAcaoBot tipo = TipoAcaoBot.AbrirReview)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", projeto, mr, tipo, tentativa, null, 9, tipo == TipoAcaoBot.Reprovar ? "x" : null, DateTime.UtcNow).Value;
            Definir(acao, 500 + tentativa);
            typeof(AcaoBotEntity).GetProperty(nameof(AcaoBotEntity.Estado))!.SetValue(acao, estado);
            return acao;
        }
    }
}
