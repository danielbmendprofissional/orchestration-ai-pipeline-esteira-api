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
    // P2b.5, critérios 13, 14 e 15: Reexecutar uma Ação falhada. Papel por operação do pedido original, estado fresco, só a última
    // do tipo na MR, nova Ação com tentativa + 1 e ReexecucaoDeAcaoId, duplo clique na mesma chave.
    [Trait("Fatia", "P2b")]
    public class ReexecutarAcaoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private const long Api = 71;
        private const long Front = 72;

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
        private long _proximoId = 100;

        public ReexecutarAcaoTests()
        {
            var app = new AplicacaoNoPainel(_appPublicId, "Valuto", "VAL", "g/valuto", 10, "g/valuto/ws",
                [new ProjetoNoPainel(RepositorioFatia.Api, 20, "g/valuto/api", Api), new ProjetoNoPainel(RepositorioFatia.Frontend, 30, "g/valuto/front", Front)]);
            _usuario.Id.Returns("sub-1");
            _acesso.ResolverIdAsync(_appPublicId, Arg.Any<CancellationToken>()).Returns(AppId);
            ConcederTudo();
            _apps.ListarVisiveisAsync("sub-1", Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([app]);
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns(Definir(PessoaEntity.Criar("sub-1", "Ana", "a@x.com", DateTime.UtcNow), 9));
            Fresco(EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 3), Mr(RepositorioFatia.Frontend, 8));
            _acoes.AdicionarAsync(Arg.Do<AcaoBotEntity>(a => _adicionadas.Add(a)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            _uow.SaveChangesAsync().Returns(_ =>
            {
                foreach (var a in _adicionadas.Where(a => a.Id == 0))
                    Definir(a, _proximoId++);
                return Task.FromResult(1);
            });
        }

        private void ConcederTudo()
        {
            _autorizacao.ObterAcessoAsync(AppId, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(true, []));
            _autorizacao.AutorizarAsync(AppId, Arg.Any<OperacaoAplicacao>(), Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
        }

        private static T Definir<T>(T e, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);
            return e;
        }

        private static MrDaGaveta Mr(RepositorioFatia repo, long iid, string estado = "opened")
            => new(repo, iid, "P2 — Review", estado, "feature/p2-x", "success");

        private void Fresco(EstadoDaFatia estado, params MrDaGaveta[] mrs)
        {
            var fatia = new FatiaLida("P2", "Review", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x.md", 7,
                new ResultadoDeEstado(estado, null, false, false), false, new DetalheDaFatia(null, null, [], [], mrs));
            _painel.ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoDoPainel("x", SituacaoDoPainel.Ok, [fatia], DateTimeOffset.UtcNow, false, null));
        }

        // `id` fixa o Id interno; `estado` força o estado (o domínio só deixa transitar por métodos).
        private static AcaoBotEntity Acao(
            long id, long projeto, long mr, TipoAcaoBot tipo, EstadoAcaoBot estado, int tentativa = 1, long? dependeDe = null, long? reexecucaoDe = null)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", projeto, mr, tipo, tentativa, dependeDe, 9, tipo == TipoAcaoBot.Reprovar ? "Falta teste" : null,
                DateTime.UtcNow, reexecucaoDe).Value;
            Definir(acao, id);
            typeof(AcaoBotEntity).GetProperty(nameof(AcaoBotEntity.Estado))!.SetValue(acao, estado);
            return acao;
        }

        private void Historico(params AcaoBotEntity[] acoes)
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns(acoes);
            foreach (var a in acoes)
                _acoes.ObterPorPublicIdAsync(a.PublicId, Arg.Any<CancellationToken>()).Returns(a);
        }

        private Task<Result<AcoesPedidasDto>> Executar(AcaoBotEntity acao, string codigo = "P2")
            => new ReexecutarAcaoHandler(new PreparadorDeDecisao(_acesso, _autorizacao, _apps, _painel, _usuario),
                    _acesso, _autorizacao, _pessoas, _acoes, _uow, _auditoria, _publicador, TimeProvider.System)
                .Handle(new ReexecutarAcaoCommand(_appPublicId, codigo, acao.PublicId), Ct);

        private void NadaGravado()
        {
            _acoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
            _publicador.DidNotReceiveWithAnyArgs().Publish(default(AcaoBotPedida)!, default);
            _uow.DidNotReceive().CommitAsync();
        }

        [Fact, DisplayName("13: Aprovar da API falhou e a do front está cancelada: cria as duas com tentativa + 1, ReexecucaoDeAcaoId e a do front dependendo da nova")]
        public async Task Recria_api_e_front_cancelada()
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            var front = Acao(11, Front, 8, TipoAcaoBot.Aprovar, EstadoAcaoBot.Cancelada, dependeDe: 10);
            Historico(api, front);

            var r = await Executar(api);

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Select(a => (a.ProjetoId, a.MrIid, a.ChaveIdempotencia)).Should().Equal(
                (Api, 3L, "5:P2:aprovar:71:3:2"), (Front, 8L, "5:P2:aprovar:72:8:2"));
            _adicionadas.Should().OnlyContain(a => a.Tipo == TipoAcaoBot.Aprovar && a.Estado == EstadoAcaoBot.Pedida && a.PedidaPorPessoaId == 9);
            _adicionadas[0].ReexecucaoDeAcaoId.Should().Be(10);
            _adicionadas[0].DependeDeAcaoId.Should().BeNull();
            _adicionadas[1].ReexecucaoDeAcaoId.Should().Be(11);
            _adicionadas[1].DependeDeAcaoId.Should().Be(_adicionadas[0].Id).And.NotBe(0);
            r.Value.Acoes.Select(a => a.Repositorio).Should().Equal("api", "frontend");
        }

        [Fact, DisplayName("13: audita e publica AcaoBotPedida por Ação criada, tudo na mesma transação")]
        public async Task Audita_publica_na_transacao()
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            var front = Acao(11, Front, 8, TipoAcaoBot.Aprovar, EstadoAcaoBot.Cancelada, dependeDe: 10);
            Historico(api, front);

            await Executar(api);

            _auditoria.Received(2).Registrar("AcaoBot", Arg.Any<Guid>(), AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
            await _publicador.Received(1).Publish(
                Arg.Is<AcaoBotPedida>(m => m.AcaoId == _adicionadas[0].PublicId && m.Tipo == "aprovar" && m.ChaveIdempotencia == "5:P2:aprovar:71:3:2"),
                Arg.Any<CancellationToken>());
            await _publicador.Received(2).Publish(Arg.Any<AcaoBotPedida>(), Arg.Any<CancellationToken>());
            Received.InOrder(() =>
            {
                _uow.BeginTransactionAsync();
                _acoes.TravarFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>());
                _uow.SaveChangesAsync(); // Id da nova Ação da API, para a do front depender dela
                _uow.SaveChangesAsync();
                _uow.CommitAsync();
            });
        }

        [Fact, DisplayName("13: Reprovar falhado mantém o mesmo comentário na nova Ação")]
        public async Task Reprovar_copia_comentario()
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Reprovar, EstadoAcaoBot.Falhou);
            Historico(api);

            var r = await Executar(api);

            r.IsSuccess.Should().BeTrue();
            var nova = _adicionadas.Should().ContainSingle().Subject;
            (nova.Tipo, nova.Comentario, nova.ChaveIdempotencia).Should().Be((TipoAcaoBot.Reprovar, "Falta teste", "5:P2:reprovar:71:3:2"));
        }

        [Fact, DisplayName("13: falhou só a Ação do front (a da API confirmada): cria só a do front, sem dependência nova")]
        public async Task So_front()
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Confirmada);
            var front = Acao(11, Front, 8, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, dependeDe: 10);
            Historico(api, front);

            var r = await Executar(front);

            r.IsSuccess.Should().BeTrue();
            var nova = _adicionadas.Should().ContainSingle().Subject;
            (nova.ProjetoId, nova.ReexecucaoDeAcaoId, nova.ChaveIdempotencia).Should().Be((Front, 11L, "5:P2:aprovar:72:8:2"));
            nova.DependeDeAcaoId.Should().Be(10);
        }

        [Fact, DisplayName("13: a tentativa conta todas as falhas e cancelamentos anteriores da MR")]
        public async Task Tentativa_seguinte()
        {
            var primeira = Acao(10, Api, 3, TipoAcaoBot.AbrirReview, EstadoAcaoBot.Falhou, 1);
            var segunda = Acao(12, Api, 3, TipoAcaoBot.AbrirReview, EstadoAcaoBot.Falhou, 2, reexecucaoDe: 10);
            Fresco(EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 3));
            Historico(primeira, segunda);

            await Executar(segunda);

            _adicionadas.Should().ContainSingle().Which.ChaveIdempotencia.Should().Be("5:P2:abrir-review:71:3:3");
        }

        [Theory, DisplayName("14: Ação que não está falhou não é reexecutável (acao.nao-reexecutavel, 409) e nada é gravado")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Cancelada)]
        public async Task Nao_falhou(EstadoAcaoBot estado)
        {
            var acao = Acao(10, Api, 3, TipoAcaoBot.Aprovar, estado);
            Historico(acao);

            var r = await Executar(acao);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("acao.nao-reexecutavel", TipoErro.Conflict));
            NadaGravado();
        }

        [Fact, DisplayName("14: falhada que não é a última do tipo na MR não é reexecutável")]
        public async Task Nao_e_a_ultima()
        {
            var velha = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 1);
            var nova = Acao(12, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Confirmada, 2);
            Historico(velha, nova);

            var r = await Executar(velha);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acao.nao-reexecutavel");
            NadaGravado();
        }

        [Theory, DisplayName("14: outra Ação da mesma decisão viva recusa com fatia.decisao-em-andamento (409)")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        public async Task Decisao_em_andamento(EstadoAcaoBot estado)
        {
            var falhada = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            var viva = Acao(12, Api, 3, TipoAcaoBot.Reprovar, estado);
            Historico(falhada, viva);

            var r = await Executar(falhada);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("fatia.decisao-em-andamento", TipoErro.Conflict));
            NadaGravado();
        }

        [Theory, DisplayName("14: Fatia fora de Em revisão, ou MR da Ação já não aberta, é fatia.sem-mr-aberta (409), lendo o estado fresco")]
        [InlineData(EstadoDaFatia.EmExecucao, "opened")]
        [InlineData(EstadoDaFatia.EmRevisao, "merged")]
        [InlineData(EstadoDaFatia.EmRevisao, "closed")]
        public async Task Sem_mr_aberta(EstadoDaFatia estado, string estadoDaMr)
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            Historico(api);
            Fresco(estado, Mr(RepositorioFatia.Api, 3, estadoDaMr), Mr(RepositorioFatia.Frontend, 8));

            var r = await Executar(api);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("fatia.sem-mr-aberta", TipoErro.Conflict));
            await _painel.Received(1).ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>());
            NadaGravado();
        }

        [Fact, DisplayName("14: sem papel na Aplicação é 404 aplicacao.nao-encontrada, antes de ler a Ação ou o GitLab")]
        public async Task Sem_papel_404()
        {
            var api = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            Historico(api);
            _autorizacao.ObterAcessoAsync(AppId, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, []));

            var r = await Executar(api);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("aplicacao.nao-encontrada", TipoErro.NotFound));
            await _painel.DidNotReceiveWithAnyArgs().ObterFrescoAsync(default!, default);
            NadaGravado();
        }

        [Theory, DisplayName("14: papel sem a operação do pedido original é 403 acesso.papel-insuficiente (abrir-review: Membro basta; aprovar/reprovar: Aprovador técnico)")]
        [InlineData(TipoAcaoBot.Aprovar, PapelAplicacao.Membro, false)]
        [InlineData(TipoAcaoBot.Reprovar, PapelAplicacao.Membro, false)]
        [InlineData(TipoAcaoBot.AbrirReview, PapelAplicacao.Analista, false)]
        [InlineData(TipoAcaoBot.AbrirReview, PapelAplicacao.Membro, true)]
        [InlineData(TipoAcaoBot.Aprovar, PapelAplicacao.AprovadorTecnico, true)]
        public async Task Papel_por_operacao_do_pedido_original(TipoAcaoBot tipo, PapelAplicacao papel, bool permitido)
        {
            var acao = Acao(10, Api, 3, tipo, EstadoAcaoBot.Falhou);
            Historico(acao);
            _autorizacao.ObterAcessoAsync(AppId, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, [papel]));
            // O preparador autoriza pela mesma regra do AcessoAplicacao.
            var acesso = new AcessoAplicacao(false, [papel]);
            _autorizacao.AutorizarAsync(AppId, Arg.Any<OperacaoAplicacao>(), Arg.Any<CancellationToken>()).Returns(c =>
                acesso.Pode(c.Arg<OperacaoAplicacao>()) ? Result.Sucesso() : Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar(acao);

            if (permitido)
                r.IsSuccess.Should().BeTrue();
            else
            {
                r.Errors.Should().ContainSingle().Which.Code.Should().Be("acesso.papel-insuficiente");
                NadaGravado();
            }
        }

        [Fact, DisplayName("14: Ação inexistente ou de outra Fatia é 404 acao.nao-encontrada")]
        public async Task Acao_inexistente()
        {
            Historico();
            var fantasma = Acao(99, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou);
            _acoes.ObterPorPublicIdAsync(fantasma.PublicId, Arg.Any<CancellationToken>()).Returns((AcaoBotEntity?)null);

            var r = await Executar(fantasma);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("acao.nao-encontrada", TipoErro.NotFound));
            NadaGravado();
        }

        [Fact, DisplayName("15: o mesmo Reexecutar duas vezes não cria a segunda Ação e devolve a primeira (mesma chave)")]
        public async Task Duplo_clique()
        {
            var falhada = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 1);
            var primeira = Acao(12, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Pedida, 2, reexecucaoDe: 10);
            Historico(falhada, primeira);
            _acoes.ObterPorChaveAsync("5:P2:aprovar:71:3:2", Arg.Any<CancellationToken>()).Returns(primeira);

            var r = await Executar(falhada);

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Should().ContainSingle().Which.Id.Should().Be(primeira.PublicId);
            NadaGravado();
        }

        [Fact, DisplayName("15: duplo clique concorrente (violação da chave) desfaz a transação e devolve a Ação que ganhou")]
        public async Task Duplo_clique_concorrente()
        {
            var falhada = Acao(10, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 1);
            Historico(falhada);
            var vencedora = Acao(12, Api, 3, TipoAcaoBot.Aprovar, EstadoAcaoBot.Pedida, 2, reexecucaoDe: 10);
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ViolacaoDeUnicidadeException("IX_AcaoBot_ChaveIdempotencia"));
            var primeiraLeitura = true;
            _acoes.ObterPorChaveAsync("5:P2:aprovar:71:3:2", Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (primeiraLeitura)
                {
                    primeiraLeitura = false;
                    return Task.FromResult<AcaoBotEntity?>(null);
                }

                return Task.FromResult<AcaoBotEntity?>(vencedora);
            });

            var r = await Executar(falhada);

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Single().Id.Should().Be(vencedora.PublicId);
            await _uow.Received(1).RollbackAsync();
            await _uow.DidNotReceive().CommitAsync();
        }
    }
}
