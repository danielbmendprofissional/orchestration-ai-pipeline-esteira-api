using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E1b (spec 02-tela-da-entrega.md, "Backend": EtapaFluxo, tabela de transições; critérios 1 e 9).
    [Trait("Fatia", "E1b")]
    public class EtapaFluxoTests
    {
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = T0.AddMinutes(5);
        private static readonly DateTime T2 = T0.AddMinutes(10);
        private static readonly AplicacaoEntity App = AplicacaoEntity.Criar("App", "APP", 1, "g").Value;
        private static readonly PessoaEntity Pessoa = PessoaEntity.Criar("sub", "Fulano", "f@x.com", T0);

        private static EntregaEntity Entrega(int fluxos = 1)
        {
            var itens = Enumerable.Range(1, fluxos).Select(i =>
                new FluxoDaEntrega(ProjetoEntity.Criar(0, 100 + i, $"g/p{i}", $"p{i}", Stack.Dotnet), true, TipoMudanca.Minor, [new FatiaDaEntrega("B2", true)])).ToList();
            return EntregaEntity.Abrir(App, Pessoa, itens, T0).Value;
        }

        private static FluxoEntity Fluxo() => Entrega().Fluxos[0];

        [Fact, DisplayName("Critério 1: Entrega.Abrir cria as 4 Etapas por Fluxo: abertura concluido, o resto nao-iniciado")]
        public void AbrirCriaAsEtapas()
        {
            var entrega = Entrega(2);

            foreach (var fluxo in entrega.Fluxos)
            {
                fluxo.Etapas.Select(e => (e.Etapa, e.Estado)).Should().BeEquivalentTo(
                [
                    (EtapaDoFluxo.Abertura, EstadoEtapa.Concluido),
                    (EtapaDoFluxo.Qa, EstadoEtapa.NaoIniciado),
                    (EtapaDoFluxo.Hml, EstadoEtapa.NaoIniciado),
                    (EtapaDoFluxo.Prd, EstadoEtapa.NaoIniciado)
                ]);
                fluxo.Etapas.Should().OnlyContain(e => e.PipelineGitLabId == null && e.UltimoEventoEm == T0 && e.AtualizadaEm == T0);
            }
        }

        [Theory, DisplayName("esteira-abrir aceito: nao-iniciado, falhou, executando ou aguardando-aprovacao -> executando, guardando o pipeline_id")]
        [InlineData(EstadoEtapa.NaoIniciado)]
        [InlineData(EstadoEtapa.Falhou)]
        [InlineData(EstadoEtapa.Executando)]
        [InlineData(EstadoEtapa.AguardandoAprovacao)]
        public void IniciarExecucaoDeOrigemValida(EstadoEtapa origem)
        {
            var etapa = EtapaEm(origem);

            var r = etapa.IniciarExecucao(777, T2);

            r.IsSuccess.Should().BeTrue();
            (etapa.Estado, etapa.PipelineGitLabId, etapa.CodigoFalha, etapa.UltimoEventoEm, etapa.AtualizadaEm).Should().Be((EstadoEtapa.Executando, 777L, null, T2, T2));
        }

        [Fact, DisplayName("esteira-abrir sem pipeline_id: executando sem id")]
        public void IniciarSemId()
        {
            var etapa = Fluxo().Etapa(EtapaDoFluxo.Qa);
            etapa.IniciarExecucao(null, T1).IsSuccess.Should().BeTrue();
            (etapa.Estado, etapa.PipelineGitLabId).Should().Be((EstadoEtapa.Executando, null));
        }

        [Theory, DisplayName("Critério 9: concluido e pulada -> executando não está na tabela: recusa e mantém o estado")]
        [InlineData(EstadoEtapa.Concluido)]
        [InlineData(EstadoEtapa.Pulada)]
        public void IniciarForaDaTabela(EstadoEtapa origem)
        {
            var etapa = EtapaEm(origem);
            var antes = (etapa.Estado, etapa.PipelineGitLabId, etapa.UltimoEventoEm);

            var r = etapa.IniciarExecucao(9, T2);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("etapa.transicao-invalida");
            (etapa.Estado, etapa.PipelineGitLabId, etapa.UltimoEventoEm).Should().Be(antes);
        }

        [Fact, DisplayName("Atalho: qa e hml nao-iniciado -> pulada com por e motivo")]
        public void Atalho()
        {
            var fluxo = Fluxo();

            fluxo.Etapa(EtapaDoFluxo.Qa).Pular("ana", "hotfix", T1).IsSuccess.Should().BeTrue();
            fluxo.Etapa(EtapaDoFluxo.Hml).Pular("ana", "hotfix", T1).IsSuccess.Should().BeTrue();

            foreach (var e in new[] { EtapaDoFluxo.Qa, EtapaDoFluxo.Hml })
            {
                var etapa = fluxo.Etapa(e);
                (etapa.Estado, etapa.PuladaPor, etapa.PuladaMotivo, etapa.AtualizadaEm).Should().Be((EstadoEtapa.Pulada, "ana", "hotfix", T1));
            }
        }

        [Theory, DisplayName("Atalho só a partir de nao-iniciado: Etapa em outro estado fica como está")]
        [InlineData(EstadoEtapa.Executando)]
        [InlineData(EstadoEtapa.Concluido)]
        [InlineData(EstadoEtapa.Falhou)]
        [InlineData(EstadoEtapa.AguardandoAprovacao)]
        [InlineData(EstadoEtapa.Pulada)]
        public void AtalhoForaDaTabela(EstadoEtapa origem)
        {
            var etapa = EtapaEm(origem);

            var r = etapa.Pular("ana", "x", T2);

            r.IsSuccess.Should().BeFalse();
            (etapa.Estado, etapa.PuladaPor).Should().Be((origem, origem == EstadoEtapa.Pulada ? "p" : null));
        }

        [Theory, DisplayName("Webhook: success -> aguardando-aprovacao (qa/hml) ou concluido (prd); failed/canceled -> falhou com código")]
        [InlineData(EtapaDoFluxo.Qa, "success", EstadoEtapa.AguardandoAprovacao, null)]
        [InlineData(EtapaDoFluxo.Hml, "success", EstadoEtapa.AguardandoAprovacao, null)]
        [InlineData(EtapaDoFluxo.Qa, "failed", EstadoEtapa.Falhou, "pipeline.falhou")]
        [InlineData(EtapaDoFluxo.Hml, "canceled", EstadoEtapa.Falhou, "pipeline.cancelada")]
        [InlineData(EtapaDoFluxo.Prd, "failed", EstadoEtapa.Falhou, "pipeline.falhou")]
        [InlineData(EtapaDoFluxo.Prd, "canceled", EstadoEtapa.Falhou, "pipeline.cancelada")]
        [InlineData(EtapaDoFluxo.Prd, "success", EstadoEtapa.Concluido, null)]
        public void WebhookAplica(EtapaDoFluxo qual, string status, EstadoEtapa esperado, string? codigo)
        {
            var etapa = Fluxo().Etapa(qual);
            etapa.IniciarExecucao(5, T1);

            var r = etapa.AplicarStatusDaPipeline(status, T2);

            r.IsSuccess.Should().BeTrue();
            (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm, etapa.AtualizadaEm).Should().Be((esperado, codigo, T2, T2));
        }

        [Theory, DisplayName("Webhook: created/pending/running/outros -> sem mudança")]
        [InlineData("created")]
        [InlineData("pending")]
        [InlineData("running")]
        [InlineData("manual")]
        [InlineData("")]
        public void WebhookSemMudanca(string status)
        {
            var etapa = Fluxo().Etapa(EtapaDoFluxo.Qa);
            etapa.IniciarExecucao(5, T1);

            etapa.AplicarStatusDaPipeline(status, T2).IsSuccess.Should().BeTrue();

            (etapa.Estado, etapa.UltimoEventoEm).Should().Be((EstadoEtapa.Executando, T1));
        }

        [Theory, DisplayName("Evento mais velho (ou igual) que UltimoEventoEm é descartado, sem regredir")]
        [InlineData(-1)]
        [InlineData(0)]
        public void EventoMaisVelho(int deslocamentoMin)
        {
            var etapa = Fluxo().Etapa(EtapaDoFluxo.Qa);
            etapa.IniciarExecucao(5, T1);
            etapa.AplicarStatusDaPipeline("success", T2);

            var r = etapa.AplicarStatusDaPipeline("failed", T2.AddMinutes(deslocamentoMin));

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("etapa.evento-antigo");
            (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm).Should().Be((EstadoEtapa.AguardandoAprovacao, null, T2));
        }

        [Theory, DisplayName("Critério 9: webhook fora da tabela (Etapa que não está executando) mantém o estado")]
        [InlineData(EstadoEtapa.NaoIniciado)]
        [InlineData(EstadoEtapa.Concluido)]
        [InlineData(EstadoEtapa.Pulada)]
        [InlineData(EstadoEtapa.Falhou)]
        [InlineData(EstadoEtapa.AguardandoAprovacao)]
        public void WebhookForaDaTabela(EstadoEtapa origem)
        {
            var etapa = EtapaEm(origem);

            var r = etapa.AplicarStatusDaPipeline("success", T2);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("etapa.transicao-invalida");
            etapa.Estado.Should().Be(origem);
        }

        // Leva uma Etapa qa ao estado dado, só pelos métodos nomeados.
        private static EtapaFluxoEntity EtapaEm(EstadoEtapa estado)
        {
            var etapa = Fluxo().Etapa(EtapaDoFluxo.Qa);
            switch (estado)
            {
                case EstadoEtapa.Executando: etapa.IniciarExecucao(1, T1); break;
                case EstadoEtapa.AguardandoAprovacao: etapa.IniciarExecucao(1, T1); etapa.AplicarStatusDaPipeline("success", T1.AddMinutes(1)); break;
                case EstadoEtapa.Falhou: etapa.IniciarExecucao(1, T1); etapa.AplicarStatusDaPipeline("failed", T1.AddMinutes(1)); break;
                case EstadoEtapa.Concluido: etapa = Fluxo().Etapa(EtapaDoFluxo.Abertura); break;
                case EstadoEtapa.Pulada: etapa.Pular("p", "m", T1); break;
            }
            etapa.Estado.Should().Be(estado);
            return etapa;
        }
    }
}
