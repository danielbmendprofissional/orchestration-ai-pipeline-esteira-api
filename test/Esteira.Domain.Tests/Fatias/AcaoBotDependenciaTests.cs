using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Fatias;

namespace Esteira.Domain.Tests.Fatias
{
    // P2.3, critério 10: a Ação do front (DependeDeAcaoId) só começa com a da API confirmada; se a da API falhar, ela é cancelada.
    [Trait("Fatia", "P2")]
    public class AcaoBotDependenciaTests
    {
        private static readonly DateTime T0 = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = T0.AddMinutes(1);

        private static AcaoBotEntity Api(EstadoAcaoBot estado)
        {
            var a = AcaoBotEntity.Criar(7, "p2", 11, 42, TipoAcaoBot.Aprovar, 1, null, 5, null, T0).Value;
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(a, 100L);
            switch (estado)
            {
                case EstadoAcaoBot.Executando: a.Iniciar(T0); break;
                case EstadoAcaoBot.Confirmada: a.Iniciar(T0); a.Confirmar(T0); break;
                case EstadoAcaoBot.Falhou: a.Iniciar(T0); a.Falhar("gitlab.erro", T0); break;
                case EstadoAcaoBot.Cancelada: a.Cancelar(T0); break;
            }
            return a;
        }

        private static AcaoBotEntity Front() => AcaoBotEntity.Criar(7, "p2", 12, 8, TipoAcaoBot.Aprovar, 1, 100, 5, null, T0).Value;

        [Fact, DisplayName("10: a Ação do front passa a executando com a da API confirmada")]
        public void Inicia_com_api_confirmada()
        {
            var front = Front();

            var r = front.IniciarAposDependencia(Api(EstadoAcaoBot.Confirmada), T1);

            r.IsSuccess.Should().BeTrue();
            front.Estado.Should().Be(EstadoAcaoBot.Executando);
            front.AtualizadaEm.Should().Be(T1);
        }

        [Theory, DisplayName("10: a Ação do front não passa a executando com a da API em qualquer outro estado")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Falhou)]
        [InlineData(EstadoAcaoBot.Cancelada)]
        public void Nao_inicia_sem_api_confirmada(EstadoAcaoBot estadoApi)
        {
            var front = Front();

            var r = front.IniciarAposDependencia(Api(estadoApi), T1);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acao-bot.dependencia-nao-confirmada");
            front.Estado.Should().Be(EstadoAcaoBot.Pedida);
        }

        [Fact, DisplayName("10: dependência ausente ou que não é a apontada impede o início")]
        public void Nao_inicia_com_dependencia_errada()
        {
            var front = Front();
            var outra = Api(EstadoAcaoBot.Confirmada);
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(outra, 999L);

            front.IniciarAposDependencia(null, T1).IsSuccess.Should().BeFalse();
            front.IniciarAposDependencia(outra, T1).IsSuccess.Should().BeFalse();
            front.Estado.Should().Be(EstadoAcaoBot.Pedida);
        }

        [Fact, DisplayName("10: Ação sem dependência (a da API) inicia direto")]
        public void Sem_dependencia_inicia()
        {
            var api = Api(EstadoAcaoBot.Pedida);

            api.IniciarAposDependencia(null, T1).IsSuccess.Should().BeTrue();
            api.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        [Fact, DisplayName("10: se a Ação da API falhou, a do front vira cancelada")]
        public void Api_falhou_cancela_o_front()
        {
            var front = Front();

            var r = front.CancelarSeDependenciaFalhou(Api(EstadoAcaoBot.Falhou), T1);

            r.IsSuccess.Should().BeTrue();
            front.Estado.Should().Be(EstadoAcaoBot.Cancelada);
            front.AtualizadaEm.Should().Be(T1);
        }

        [Theory, DisplayName("10: a Ação do front não é cancelada enquanto a da API não falhou")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Confirmada)]
        public void Api_nao_falhou_nao_cancela(EstadoAcaoBot estadoApi)
        {
            var front = Front();

            var r = front.CancelarSeDependenciaFalhou(Api(estadoApi), T1);

            r.IsSuccess.Should().BeFalse();
            front.Estado.Should().Be(EstadoAcaoBot.Pedida);
        }
    }
}
