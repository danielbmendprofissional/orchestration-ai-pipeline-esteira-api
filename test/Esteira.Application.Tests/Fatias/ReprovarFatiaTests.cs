using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
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
    // P2.4, critérios 4 e 6: Reprovar. Uma Ação por MR aberta (qualquer CI), comentário obrigatório, nada escrito no GitLab.
    [Trait("Fatia", "P2")]
    public class ReprovarFatiaTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private const string Comentario = "Falta cobrir o <b>caso</b> de saldo negativo.";

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

        public ReprovarFatiaTests()
        {
            _app = new AplicacaoNoPainel(_appPublicId, "Valuto", "VAL", "g/valuto", 10, "g/valuto/ws",
                [new ProjetoNoPainel(RepositorioFatia.Api, 20, "g/valuto/api", 71), new ProjetoNoPainel(RepositorioFatia.Frontend, 30, "g/valuto/front", 72)]);
            _usuario.Id.Returns("sub-1");
            _acesso.ResolverIdAsync(_appPublicId, Arg.Any<CancellationToken>()).Returns(AppId);
            _autorizacao.AutorizarAsync(AppId, OperacaoAplicacao.DecidirFatia, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _apps.ListarVisiveisAsync("sub-1", Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([_app]);
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns(Definir(PessoaEntity.Criar("sub-1", "Ana", "a@x.com", DateTime.UtcNow), 9));
            // CI vermelho/sem pipeline não impede reprovar.
            Fresco(Ok(Fatia("P2", EstadoDaFatia.EmRevisao, Mr(RepositorioFatia.Api, 5, pipeline: "failed"), Mr(RepositorioFatia.Api, 3, pipeline: "running"),
                Mr(RepositorioFatia.Api, 1, "merged"), Mr(RepositorioFatia.Frontend, 8, pipeline: null))));
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

        private Task<Result<AcoesPedidasDto>> Executar(string? comentario = Comentario, string codigo = "P2")
            => new ReprovarFatiaHandler(new PreparadorDeDecisao(_acesso, _autorizacao, _apps, _painel, _usuario),
                    _pessoas, _acoes, _uow, _auditoria, _publicador, TimeProvider.System)
                .Handle(new ReprovarFatiaCommand(_appPublicId, codigo, comentario!), Ct);

        private void NadaGravado()
        {
            _acoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
            _publicador.DidNotReceiveWithAnyArgs().Publish(default(AcaoBotPedida)!, default);
            _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("4: grava uma Ação reprovar pedida por MR aberta, com comentário e autor, com qualquer CI; audita e publica AcaoBotPedida")]
        public async Task Grava_audita_e_publica()
        {
            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Select(a => (a.ProjetoId, a.MrIid)).Should().Equal((71L, 3L), (71L, 5L), (72L, 8L));
            _adicionadas.Should().OnlyContain(a => a.Tipo == TipoAcaoBot.Reprovar && a.Estado == EstadoAcaoBot.Pedida && a.PedidaPorPessoaId == 9 && a.Comentario == Comentario);
            _adicionadas[0].ChaveIdempotencia.Should().Be("5:P2:reprovar:71:3:1");
            r.Value.Acoes.Select(a => a.Repositorio).Should().Equal("api", "api", "frontend");
            r.Value.Acoes.Should().OnlyContain(a => a.Tipo == "reprovar" && a.Estado == "pedida");
            _auditoria.Received(3).Registrar("AcaoBot", Arg.Any<Guid>(), AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
            await _publicador.Received(3).Publish(Arg.Any<AcaoBotPedida>(), Arg.Any<CancellationToken>());
            await _publicador.Received(1).Publish(
                Arg.Is<AcaoBotPedida>(m => m.AcaoId == _adicionadas[0].PublicId && m.Tipo == "reprovar" && m.ChaveIdempotencia == _adicionadas[0].ChaveIdempotencia),
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("4/15: o comentário fica só na Ação; nem a auditoria nem a mensagem do outbox o carregam")]
        public async Task Comentario_fora_da_auditoria_e_do_outbox()
        {
            var depois = new List<string>();
            _auditoria.When(a => a.Registrar(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AcaoAuditoria>(), Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<DadosDoRegistro>()))
                .Do(c => depois.Add(JsonSerializer.Serialize(c.ArgAt<object?>(4))));
            var mensagens = new List<string>();
            _publicador.When(p => p.Publish(Arg.Any<AcaoBotPedida>(), Arg.Any<CancellationToken>()))
                .Do(c => mensagens.Add(JsonSerializer.Serialize(c.Arg<AcaoBotPedida>())));

            await Executar();

            depois.Should().HaveCount(3).And.OnlyContain(j => !j.Contains("saldo") && !j.Contains("Comentario"));
            mensagens.Should().HaveCount(3).And.OnlyContain(j => !j.Contains("saldo"));
        }

        [Fact, DisplayName("4: comentário de 2000 caracteres é aceito")]
        public async Task Comentario_no_limite()
        {
            var r = await Executar(new string('a', 2000));

            r.IsSuccess.Should().BeTrue();
        }

        [Theory, DisplayName("4: sem comentário ou acima de 2000 caracteres recusa (fatia.comentario-invalido, 400) e não grava nada")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("INVALIDO-2001")]
        public async Task Comentario_invalido(string? comentario)
        {
            if (comentario == "INVALIDO-2001")
                comentario = new string('a', 2001);

            var r = await Executar(comentario);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("fatia.comentario-invalido", TipoErro.Validation));
            NadaGravado();
        }

        [Fact, DisplayName("4: tudo na mesma transação (abre, grava e confirma)")]
        public async Task Mesma_transacao()
        {
            await Executar();

            Received.InOrder(() =>
            {
                _uow.BeginTransactionAsync();
                _uow.SaveChangesAsync(); // Id da primeira Ação da API
                _uow.SaveChangesAsync();
                _uow.CommitAsync();
            });
        }

        [Fact, DisplayName("4: Reprovar fecha as duas MRs: a Ação do front depende da primeira da API")]
        public async Task Front_depende_da_api()
        {
            await Executar();

            var apis = _adicionadas.Where(a => a.ProjetoId == 71).ToList();
            apis.Should().OnlyContain(a => a.DependeDeAcaoId == null);
            _adicionadas.Single(a => a.ProjetoId == 72).DependeDeAcaoId.Should().Be(apis[0].Id);
        }

        [Theory, DisplayName("9: sem papel (404) ou sem a operação (403) recusa antes de ler o GitLab e de gravar")]
        [InlineData(TipoErro.NotFound, "aplicacao.nao-encontrada")]
        [InlineData(TipoErro.Forbidden, "acesso.papel-insuficiente")]
        public async Task Autorizacao_recusa(TipoErro tipo, string codigo)
        {
            _autorizacao.AutorizarAsync(AppId, OperacaoAplicacao.DecidirFatia, Arg.Any<CancellationToken>()).Returns(Result.Falha(new Erro(codigo, "x", tipo)));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            await _painel.DidNotReceiveWithAnyArgs().ObterFrescoAsync(default!, default);
            NadaGravado();
        }

        [Fact, DisplayName("5: Fatia que não está Em revisão é recusada (fatia.sem-mr-aberta)")]
        public async Task Fora_de_revisao()
        {
            Fresco(Ok(Fatia("P2", EstadoDaFatia.EmExecucao, Mr(RepositorioFatia.Api, 3))));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("fatia.sem-mr-aberta");
            NadaGravado();
        }

        [Theory, DisplayName("6: decisão (aprovar) pedida ou executando recusa o Reprovar (fatia.decisao-em-andamento, 409) sem gravar")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        public async Task Decisao_em_andamento(EstadoAcaoBot estado)
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns([AcaoExistente(71, 3, estado, 1, TipoAcaoBot.Aprovar)]);

            var r = await Executar();

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("fatia.decisao-em-andamento", TipoErro.Conflict));
            NadaGravado();
        }

        [Theory, DisplayName("6: decisão que falhou ou foi cancelada não bloqueia; o Reprovar segue com a tentativa seguinte")]
        [InlineData(EstadoAcaoBot.Falhou)]
        [InlineData(EstadoAcaoBot.Cancelada)]
        public async Task Decisao_encerrada_nao_bloqueia(EstadoAcaoBot estado)
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>())
                .Returns([AcaoExistente(71, 3, estado, 1, TipoAcaoBot.Reprovar), AcaoExistente(71, 3, estado, 2, TipoAcaoBot.Aprovar)]);

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            _adicionadas.Single(a => a.ProjetoId == 71 && a.MrIid == 3).ChaveIdempotencia.Should().Be("5:P2:reprovar:71:3:2");
            _adicionadas.Single(a => a.ProjetoId == 71 && a.MrIid == 5).ChaveIdempotencia.Should().Be("5:P2:reprovar:71:5:1");
        }

        [Fact, DisplayName("6: duplo clique no Reprovar cai na mesma chave e devolve a primeira Ação sem gravar nem publicar de novo")]
        public async Task Duplo_clique_mesma_chave()
        {
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>())
                .Returns([AcaoExistente(71, 3, EstadoAcaoBot.Pedida, 1, TipoAcaoBot.Reprovar)]);
            foreach (var (chave, p, m) in new[] { ("5:P2:reprovar:71:3:1", 71L, 3L), ("5:P2:reprovar:71:5:1", 71L, 5L), ("5:P2:reprovar:72:8:1", 72L, 8L) })
                _acoes.ObterPorChaveAsync(chave, Arg.Any<CancellationToken>()).Returns(AcaoExistente(p, m, EstadoAcaoBot.Pedida, 1, TipoAcaoBot.Reprovar));

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Should().HaveCount(3);
            NadaGravado();
        }

        [Fact, DisplayName("6: violação 23505 da chave desfaz a transação e devolve as Ações que ganharam")]
        public async Task Violacao_de_unicidade_devolve_a_primeira()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ViolacaoDeUnicidadeException("IX_AcaoBot_ChaveIdempotencia"));
            var vencedoras = new Dictionary<string, AcaoBotEntity>
            {
                ["5:P2:reprovar:71:3:1"] = AcaoExistente(71, 3, EstadoAcaoBot.Pedida, 1, TipoAcaoBot.Reprovar),
                ["5:P2:reprovar:71:5:1"] = AcaoExistente(71, 5, EstadoAcaoBot.Pedida, 1, TipoAcaoBot.Reprovar),
                ["5:P2:reprovar:72:8:1"] = AcaoExistente(72, 8, EstadoAcaoBot.Pedida, 1, TipoAcaoBot.Reprovar)
            };
            var primeiraLeitura = true;
            _acoes.ObterPorChaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c =>
            {
                if (primeiraLeitura && c.Arg<string>() == "5:P2:reprovar:71:3:1")
                {
                    primeiraLeitura = false;
                    return Task.FromResult<AcaoBotEntity?>(null);
                }

                return Task.FromResult<AcaoBotEntity?>(vencedoras[c.Arg<string>()]);
            });

            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            r.Value.Acoes.Select(a => a.Id).Should().BeEquivalentTo(vencedoras.Values.Select(a => a.PublicId));
            await _uow.Received(1).RollbackAsync();
            await _uow.DidNotReceive().CommitAsync();
        }

        [Fact, DisplayName("Pessoa que ainda não entrou na Esteira não tem Ação gravada (acesso.pessoa-nao-registrada)")]
        public async Task Pessoa_nao_registrada()
        {
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns((PessoaEntity?)null);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acesso.pessoa-nao-registrada");
            NadaGravado();
        }

        private static AcaoBotEntity AcaoExistente(long projeto, long mr, EstadoAcaoBot estado, int tentativa, TipoAcaoBot tipo)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", projeto, mr, tipo, tentativa, null, 9, tipo == TipoAcaoBot.Reprovar ? "x" : null, DateTime.UtcNow).Value;
            Definir(acao, 500 + tentativa);
            typeof(AcaoBotEntity).GetProperty(nameof(AcaoBotEntity.Estado))!.SetValue(acao, estado);
            return acao;
        }
    }
}
