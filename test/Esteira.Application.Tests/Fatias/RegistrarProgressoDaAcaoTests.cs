using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.4, critério 11: o serviço que o executor da P2b usa para mudar o estado da Ação. Só transições válidas, auditoria,
    // dependência API -> front e descarte do instantâneo do Painel ao confirmar ou falhar.
    [Trait("Fatia", "P2")]
    public class RegistrarProgressoDaAcaoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private static readonly DateTime Agora = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IDescarteDoPainel _painel = Substitute.For<IDescarteDoPainel>();
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<AcaoBotEntity> _todas = [];

        public RegistrarProgressoDaAcaoTests()
        {
            _acesso.ObterPublicIdAsync(AppId, Arg.Any<CancellationToken>()).Returns(_appPublicId);
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns(_ => _todas.ToList());
            _acoes.ObterAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(c => _todas.FirstOrDefault(a => a.Id == c.Arg<long>()));
            _acoes.ObterPorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(c => _todas.FirstOrDefault(a => a.PublicId == c.Arg<Guid>()));
        }

        private RegistrarProgressoDaAcao Servico() => new(_acoes, _acesso, _uow, _auditoria, _painel, new RelogioFixo());

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private AcaoBotEntity Acao(long id, long projeto, EstadoAcaoBot estado, long? dependeDe = null)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", projeto, 3, TipoAcaoBot.Aprovar, 1, dependeDe, 9, null, Agora.AddHours(-1)).Value;
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(acao, id);
            switch (estado)
            {
                case EstadoAcaoBot.Executando: acao.Iniciar(Agora.AddMinutes(-30)); break;
                case EstadoAcaoBot.Confirmada: acao.Iniciar(Agora.AddMinutes(-30)); acao.Confirmar(Agora.AddMinutes(-20)); break;
                case EstadoAcaoBot.Falhou: acao.Iniciar(Agora.AddMinutes(-30)); acao.Falhar("x", Agora.AddMinutes(-20)); break;
                case EstadoAcaoBot.Cancelada: acao.Cancelar(Agora.AddMinutes(-30)); break;
            }

            _todas.Add(acao);
            return acao;
        }

        private void NadaMudou()
        {
            _uow.DidNotReceive().SaveChangesAsync();
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
            _painel.DidNotReceiveWithAnyArgs().DescartarAsync(default!, default);
        }

        [Theory, DisplayName("11: transições válidas são aplicadas, salvas e auditadas (Alterou, antes/depois)")]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Falhou)]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Cancelada)]
        public async Task Transicoes_validas(EstadoAcaoBot de, EstadoAcaoBot para)
        {
            var acao = Acao(1, 71, de);

            var r = await Servico().RegistrarAsync(acao.PublicId, para, para == EstadoAcaoBot.Falhou ? "gitlab.merge-conflito" : null, Ct);

            r.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(para);
            acao.AtualizadaEm.Should().Be(Agora);
            acao.CodigoErro.Should().Be(para == EstadoAcaoBot.Falhou ? "gitlab.merge-conflito" : null);
            await _uow.Received(1).SaveChangesAsync();
            _auditoria.Received(1).Registrar("AcaoBot", acao.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null));
        }

        [Theory, DisplayName("11: qualquer outra transição é rejeitada (acao-bot.transicao-invalida, 409) sem salvar, auditar nem descartar")]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Falhou)]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Cancelada)]
        [InlineData(EstadoAcaoBot.Confirmada, EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Confirmada, EstadoAcaoBot.Falhou)]
        [InlineData(EstadoAcaoBot.Falhou, EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Cancelada, EstadoAcaoBot.Executando)]
        public async Task Transicoes_invalidas(EstadoAcaoBot de, EstadoAcaoBot para)
        {
            var acao = Acao(1, 71, de);

            var r = await Servico().RegistrarAsync(acao.PublicId, para, "x", Ct);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("acao-bot.transicao-invalida", TipoErro.Conflict));
            acao.Estado.Should().Be(de);
            NadaMudou();
        }

        [Fact, DisplayName("11: Ação que não existe devolve acao-bot.nao-encontrada (404)")]
        public async Task Acao_inexistente()
        {
            var r = await Servico().RegistrarAsync(Guid.NewGuid(), EstadoAcaoBot.Executando, null, Ct);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("acao-bot.nao-encontrada", TipoErro.NotFound));
            NadaMudou();
        }

        [Theory, DisplayName("11: ao ir para confirmada ou falhou descarta o instantâneo da Aplicação, depois de salvar")]
        [InlineData(EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Falhou)]
        public async Task Descarta_o_instantaneo(EstadoAcaoBot para)
        {
            var acao = Acao(1, 71, EstadoAcaoBot.Executando);

            await Servico().RegistrarAsync(acao.PublicId, para, "x", Ct);

            Received.InOrder(() =>
            {
                _uow.SaveChangesAsync();
                _painel.DescartarAsync(_appPublicId.ToString(), Arg.Any<CancellationToken>());
            });
        }

        [Theory, DisplayName("11: ir para executando ou cancelada não descarta o instantâneo")]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Cancelada)]
        public async Task Nao_descarta_em_andamento(EstadoAcaoBot de, EstadoAcaoBot para)
        {
            var acao = Acao(1, 71, de);

            await Servico().RegistrarAsync(acao.PublicId, para, null, Ct);

            await _painel.DidNotReceiveWithAnyArgs().DescartarAsync(default!, default);
        }

        [Fact, DisplayName("10/11: a Ação do front só passa a executando com a da API confirmada")]
        public async Task Front_espera_a_api()
        {
            var api = Acao(1, 71, EstadoAcaoBot.Executando);
            var front = Acao(2, 72, EstadoAcaoBot.Pedida, dependeDe: 1);

            var recusada = await Servico().RegistrarAsync(front.PublicId, EstadoAcaoBot.Executando, null, Ct);

            recusada.Errors.Should().ContainSingle().Which.Code.Should().Be("acao-bot.dependencia-nao-confirmada");
            front.Estado.Should().Be(EstadoAcaoBot.Pedida);
            NadaMudou();

            await Servico().RegistrarAsync(api.PublicId, EstadoAcaoBot.Confirmada, null, Ct);
            var aceita = await Servico().RegistrarAsync(front.PublicId, EstadoAcaoBot.Executando, null, Ct);

            aceita.IsSuccess.Should().BeTrue();
            front.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        [Fact, DisplayName("10/11: se a Ação da API falha, a do front que depende dela é cancelada, na mesma gravação, e as dos outros tipos ficam")]
        public async Task Api_falha_cancela_o_front()
        {
            var api = Acao(1, 71, EstadoAcaoBot.Executando);
            var front = Acao(2, 72, EstadoAcaoBot.Pedida, dependeDe: 1);
            var outraApi = Acao(3, 73, EstadoAcaoBot.Pedida);

            var r = await Servico().RegistrarAsync(api.PublicId, EstadoAcaoBot.Falhou, "gitlab.merge-conflito", Ct);

            r.IsSuccess.Should().BeTrue();
            api.Estado.Should().Be(EstadoAcaoBot.Falhou);
            front.Estado.Should().Be(EstadoAcaoBot.Cancelada);
            outraApi.Estado.Should().Be(EstadoAcaoBot.Pedida);
            _auditoria.Received(1).Registrar("AcaoBot", api.PublicId, AcaoAuditoria.Alterou, Arg.Any<object?>(), Arg.Any<object?>());
            _auditoria.Received(1).Registrar("AcaoBot", front.PublicId, AcaoAuditoria.Alterou, Arg.Any<object?>(), Arg.Any<object?>());
            await _uow.Received(1).SaveChangesAsync();
            await _painel.Received(1).DescartarAsync(_appPublicId.ToString(), Arg.Any<CancellationToken>());
        }
    }
}
