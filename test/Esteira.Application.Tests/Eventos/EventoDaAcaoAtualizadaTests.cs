using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Fatias;
using NSubstitute;

namespace Esteira.Application.Tests.Eventos
{
    // T1.2, critérios 5 e 14: `acao.atualizada` sai depois do commit, em toda transição da Ação (RegistrarProgressoDaAcao é o
    // ponto único: executor, consulta periódica e webhook passam por ele), e só com identificadores e estado.
    [Trait("Fatia", "T1")]
    public class EventoDaAcaoAtualizadaTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private static readonly DateTime Agora = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IDescarteDoPainel _painel = Substitute.For<IDescarteDoPainel>();
        private readonly IPublicadorDeEventos _eventos = Substitute.For<IPublicadorDeEventos>();
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<AcaoBotEntity> _todas = [];
        private readonly List<string> _ordem = [];

        public EventoDaAcaoAtualizadaTests()
        {
            _acesso.ObterPublicIdAsync(AppId, Arg.Any<CancellationToken>()).Returns(_appPublicId);
            _acoes.ListarDaFatiaAsync(AppId, "T1", Arg.Any<CancellationToken>()).Returns(_ => _todas.ToList());
            _acoes.ObterAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(c => _todas.FirstOrDefault(a => a.Id == c.Arg<long>()));
            _acoes.ObterPorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(c => _todas.FirstOrDefault(a => a.PublicId == c.Arg<Guid>()));
            _uow.SaveChangesAsync().Returns(_ => { _ordem.Add("commit"); return Task.FromResult(1); });
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>())
                .Returns(c => { _ordem.Add($"evento:{c.Arg<EventoDeTempoReal>().Tipo}"); return Task.CompletedTask; });
        }

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private RegistrarProgressoDaAcao Servico() => new(_acoes, _acesso, _uow, _auditoria, _painel, new RelogioFixo(), _eventos);

        private AcaoBotEntity Acao(long id, EstadoAcaoBot estado, long? dependeDe = null, long projeto = 71)
        {
            var acao = AcaoBotEntity.Criar(AppId, "T1", projeto, 3, TipoAcaoBot.Aprovar, 1, dependeDe, 9, null, Agora.AddHours(-1)).Value;
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(acao, id);
            if (estado == EstadoAcaoBot.Executando)
                acao.Iniciar(Agora.AddMinutes(-30));
            _todas.Add(acao);
            return acao;
        }

        [Theory, DisplayName("5: toda transição publica acao.atualizada com o estado novo, só depois do commit")]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Executando, "executando")]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Confirmada, "confirmada")]
        [InlineData(EstadoAcaoBot.Executando, EstadoAcaoBot.Falhou, "falhou")]
        [InlineData(EstadoAcaoBot.Pedida, EstadoAcaoBot.Cancelada, "cancelada")]
        public async Task Publica_depois_do_commit(EstadoAcaoBot de, EstadoAcaoBot para, string texto)
        {
            var acao = Acao(1, de);

            (await Servico().RegistrarAsync(acao.PublicId, para, para == EstadoAcaoBot.Falhou ? "x.y" : null, Ct)).IsSuccess.Should().BeTrue();

            _ordem.Should().Equal("commit", "evento:acao.atualizada");
            await _eventos.Received(1).PublicarAsync(
                Arg.Is<EventoDeTempoReal>(e => e is AcaoAtualizada
                    && ((AcaoAtualizada)e).AplicacaoId == _appPublicId && ((AcaoAtualizada)e).FatiaCodigo == "T1"
                    && ((AcaoAtualizada)e).AcaoId == acao.PublicId && ((AcaoAtualizada)e).Estado == texto),
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("5: transição inválida não publica nada")]
        public async Task Transicao_invalida_nao_publica()
        {
            var acao = Acao(1, EstadoAcaoBot.Pedida);

            (await Servico().RegistrarAsync(acao.PublicId, EstadoAcaoBot.Confirmada, null, Ct)).IsSuccess.Should().BeFalse();

            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("5: a Ação dependente cancelada junto também publica o próprio evento")]
        public async Task Dependente_cancelada_publica()
        {
            var api = Acao(1, EstadoAcaoBot.Executando);
            var front = Acao(2, EstadoAcaoBot.Pedida, dependeDe: 1, projeto: 72);

            await Servico().RegistrarAsync(api.PublicId, EstadoAcaoBot.Falhou, "x.y", Ct);

            front.Estado.Should().Be(EstadoAcaoBot.Cancelada);
            _ordem.Should().Equal("commit", "evento:acao.atualizada", "evento:acao.atualizada");
            await _eventos.Received(1).PublicarAsync(Arg.Is<EventoDeTempoReal>(e => e is AcaoAtualizada && ((AcaoAtualizada)e).AcaoId == front.PublicId
                && ((AcaoAtualizada)e).Estado == "cancelada"), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("14: o evento serializado leva só identificadores, código da Fatia e estado")]
        public void Evento_sem_texto_livre()
        {
            var evento = new AcaoAtualizada(Guid.NewGuid(), "T1", Guid.NewGuid(), "falhou");

            using var doc = JsonDocument.Parse(CodecDeEventos.Serializar(evento));

            doc.RootElement.EnumerateObject().Select(p => p.Name)
                .Should().BeEquivalentTo("tipo", "aplicacaoId", "fatiaCodigo", "acaoId", "estado");
        }

        [Fact, DisplayName("14: pipeline.atualizada e fatias.mudaram também só levam identificadores, status, ref e sha")]
        public void Outros_tipos_sem_texto_livre()
        {
            var pipeline = JsonDocument.Parse(CodecDeEventos.Serializar(new PipelineAtualizada(Guid.NewGuid(), Guid.NewGuid(), 1, "success", "main", "abc")));
            var fatias = JsonDocument.Parse(CodecDeEventos.Serializar(new FatiasMudaram(Guid.NewGuid(), "T1")));

            pipeline.RootElement.EnumerateObject().Select(p => p.Name)
                .Should().BeEquivalentTo("tipo", "aplicacaoId", "projetoId", "pipelineId", "status", "ref", "sha");
            fatias.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("tipo", "aplicacaoId", "fatiaCodigo");
            pipeline.RootElement.GetProperty("tipo").GetString().Should().Be("pipeline.atualizada");
            fatias.RootElement.GetProperty("tipo").GetString().Should().Be("fatias.mudaram");
        }

        [Fact, DisplayName("14: evento malformado, sem aplicação ou de tipo desconhecido é descartado na leitura")]
        public void Malformado_e_descartado()
        {
            CodecDeEventos.TentarLer("isso nao e json", out _).Should().BeFalse();
            CodecDeEventos.TentarLer("{\"tipo\":\"desconhecido\",\"aplicacaoId\":\"" + Guid.NewGuid() + "\"}", out _).Should().BeFalse();
            CodecDeEventos.TentarLer("{\"tipo\":\"acao.atualizada\",\"fatiaCodigo\":\"T1\",\"acaoId\":\"" + Guid.NewGuid() + "\",\"estado\":\"falhou\"}", out _).Should().BeFalse();
            CodecDeEventos.TentarLer("{\"tipo\":\"acao.atualizada\",\"aplicacaoId\":\"" + Guid.Empty + "\",\"fatiaCodigo\":\"T1\",\"acaoId\":\"" + Guid.NewGuid() + "\",\"estado\":\"falhou\"}", out _).Should().BeFalse();
        }

        [Fact, DisplayName("14: evento válido faz a viagem de ida e volta")]
        public void Ida_e_volta()
        {
            var original = new AcaoAtualizada(Guid.NewGuid(), "T1", Guid.NewGuid(), "confirmada");

            CodecDeEventos.TentarLer(CodecDeEventos.Serializar(original), out var lido).Should().BeTrue();

            lido.Should().Be(original);
        }
    }
}
