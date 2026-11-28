using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E1b (spec 02-tela-da-entrega.md, "Estado da Entrega", critério 14): calculado das Etapas, nunca gravado.
    [Trait("Fatia", "E1b")]
    public class EstadoDaEntregaTests
    {
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        private static readonly AplicacaoEntity App = AplicacaoEntity.Criar("App", "APP", 1, "g").Value;
        private static readonly PessoaEntity Pessoa = PessoaEntity.Criar("sub", "Fulano", "f@x.com", T0);

        private static EntregaEntity Entrega()
        {
            var itens = new[] { 1, 2 }.Select(i =>
                new FluxoDaEntrega(ProjetoEntity.Criar(0, 100 + i, $"g/p{i}", $"p{i}", Stack.Dotnet), true, TipoMudanca.Minor, [new FatiaDaEntrega("B2", true)])).ToList();
            return EntregaEntity.Abrir(App, Pessoa, itens, T0).Value;
        }

        private static void Executar(EtapaFluxoEntity e, string? status = null)
        {
            e.IniciarExecucao(1, T0.AddMinutes(1));
            if (status is not null) e.AplicarStatusDaPipeline(status, T0.AddMinutes(2));
        }

        [Fact, DisplayName("Critério 14: recém-aberta -> em-andamento, sem vezDe")]
        public void EmAndamento()
        {
            var estado = Entrega().Estado;
            (estado.Estado, estado.VezDe).Should().Be((EstadoEntrega.EmAndamento, null));
        }

        [Fact, DisplayName("Critério 14: Etapa executando continua em-andamento")]
        public void Executando()
        {
            var e = Entrega();
            Executar(e.Fluxos[0].Etapa(EtapaDoFluxo.Qa));
            e.Estado.Estado.Should().Be(EstadoEntrega.EmAndamento);
        }

        [Fact, DisplayName("Critério 14: alguma Etapa aguardando-aprovacao -> aguardando-aprovacao com vezDe aprovador-tecnico")]
        public void Aguardando()
        {
            var e = Entrega();
            Executar(e.Fluxos[1].Etapa(EtapaDoFluxo.Hml), "success");
            var estado = e.Estado;
            (estado.Estado, estado.VezDe).Should().Be((EstadoEntrega.AguardandoAprovacao, VezDe.AprovadorTecnico));
        }

        [Fact, DisplayName("Critério 14: alguma Etapa falhou -> bloqueada-ou-falha, mesmo com outra aguardando")]
        public void FalhaPrevalece()
        {
            var e = Entrega();
            Executar(e.Fluxos[0].Etapa(EtapaDoFluxo.Qa), "success");
            Executar(e.Fluxos[1].Etapa(EtapaDoFluxo.Qa), "failed");
            var estado = e.Estado;
            (estado.Estado, estado.VezDe).Should().Be((EstadoEntrega.BloqueadaOuFalha, null));
        }

        [Fact, DisplayName("Etapas puladas e concluídas não mudam o estado: em-andamento")]
        public void PuladaEConcluida()
        {
            var e = Entrega();
            e.Fluxos[0].Etapa(EtapaDoFluxo.Qa).Pular("a", "m", T0.AddMinutes(1));
            Executar(e.Fluxos[0].Etapa(EtapaDoFluxo.Prd), "success");
            e.Estado.Estado.Should().Be(EstadoEntrega.EmAndamento);
        }
    }
}
