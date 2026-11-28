using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E2 (spec 03-analises-e-gate.md, "Backend": EtapaFluxoEntity.Bloquear; critérios 10 e 11).
    [Trait("Fatia", "E2")]
    public class EtapaFluxoBloqueadaTests
    {
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = T0.AddMinutes(5);
        private static readonly DateTime T2 = T0.AddMinutes(10);
        private static readonly DateTime T3 = T0.AddMinutes(15);
        private static readonly AplicacaoEntity App = AplicacaoEntity.Criar("App", "APP", 1, "g").Value;
        private static readonly PessoaEntity Pessoa = PessoaEntity.Criar("sub", "Fulano", "f@x.com", T0);

        private static EtapaFluxoEntity Qa()
        {
            var itens = new[] { new FluxoDaEntrega(ProjetoEntity.Criar(0, 101, "g/p1", "p1", Stack.Dotnet), true, TipoMudanca.Minor, [new FatiaDaEntrega("B2", true)]) };
            return EntregaEntity.Abrir(App, Pessoa, itens, T0).Value.Fluxos[0].Etapa(EtapaDoFluxo.Qa);
        }

        private static EtapaFluxoEntity Bloqueada()
        {
            var etapa = Qa();
            etapa.IniciarExecucao(7, T1);
            etapa.Bloquear("gate.bloqueado", T2).IsSuccess.Should().BeTrue();
            return etapa;
        }

        private static EtapaFluxoEntity ComFalha()
        {
            var etapa = Qa();
            etapa.IniciarExecucao(7, T1);
            etapa.AplicarStatusDaPipeline("failed", T2);
            return etapa;
        }

        [Fact, DisplayName("Bloquear: executando -> bloqueada, guarda o motivo em CodigoFalha")]
        public void BloquearDeExecutando()
        {
            var etapa = Qa();
            etapa.IniciarExecucao(7, T1);

            var r = etapa.Bloquear("gate.bloqueado", T2);

            r.IsSuccess.Should().BeTrue();
            (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm).Should().Be((EstadoEtapa.Bloqueada, "gate.bloqueado", T2));
        }

        [Fact, DisplayName("Bloquear: aguardando-aprovacao -> bloqueada (resultado.bloqueado)")]
        public void BloquearDeAguardando()
        {
            var etapa = Qa();
            etapa.IniciarExecucao(7, T1);
            etapa.AplicarStatusDaPipeline("success", T2);

            etapa.Bloquear("resultado.bloqueado", T3).IsSuccess.Should().BeTrue();

            (etapa.Estado, etapa.CodigoFalha).Should().Be((EstadoEtapa.Bloqueada, "resultado.bloqueado"));
        }

        [Theory, DisplayName("Bloquear: de qualquer outro estado é no-op (falha de transição, estado intacto)")]
        [InlineData("nao-iniciado")]
        [InlineData("falhou")]
        [InlineData("bloqueada")]
        public void BloquearForaDaTabela(string origem)
        {
            var etapa = origem switch
            {
                "bloqueada" => Bloqueada(),
                "falhou" => ComFalha(),
                _ => Qa()
            };
            var antes = (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm);

            var r = etapa.Bloquear("gate.bloqueado", T3);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("etapa.transicao-invalida");
            (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm).Should().Be(antes);
        }

        [Fact, DisplayName("Critério 10: esteira-abrir numa Etapa bloqueada a devolve a executando e limpa o motivo")]
        public void IniciarDesdeBloqueada()
        {
            var etapa = Bloqueada();

            var r = etapa.IniciarExecucao(8, T3);

            r.IsSuccess.Should().BeTrue();
            (etapa.Estado, etapa.PipelineGitLabId, etapa.CodigoFalha).Should().Be((EstadoEtapa.Executando, (long?)8, (string?)null));
        }

        [Theory, DisplayName("Critério 10: webhook (success, failed, canceled) não altera Etapa bloqueada")]
        [InlineData("success")]
        [InlineData("failed")]
        [InlineData("canceled")]
        public void WebhookNaoAlteraBloqueada(string status)
        {
            var etapa = Bloqueada();

            var r = etapa.AplicarStatusDaPipeline(status, T3);

            r.IsSuccess.Should().BeFalse();
            (etapa.Estado, etapa.CodigoFalha, etapa.UltimoEventoEm).Should().Be((EstadoEtapa.Bloqueada, "gate.bloqueado", T2));
        }

        [Fact, DisplayName("Critério 11: Etapa bloqueada -> Entrega bloqueada-ou-falha")]
        public void EstadoDaEntregaComBloqueada()
        {
            var estado = EstadoDaEntrega.Calcular([Bloqueada()]);

            (estado.Estado, estado.VezDe).Should().Be((EstadoEntrega.BloqueadaOuFalha, (VezDe?)null));
        }

        [Fact, DisplayName("Critério 11: bloqueada vence aguardando-aprovacao no estado da Entrega")]
        public void BloqueadaVenceAguardando()
        {
            var aguardando = Qa();
            aguardando.IniciarExecucao(1, T1);
            aguardando.AplicarStatusDaPipeline("success", T2);

            EstadoDaEntrega.Calcular([aguardando, Bloqueada()]).Estado.Should().Be(EstadoEntrega.BloqueadaOuFalha);
        }
    }
}
