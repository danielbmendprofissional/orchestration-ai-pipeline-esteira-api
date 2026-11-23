using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;

namespace Esteira.Domain.Tests.Fatias
{
    // P2 (spec 02-review-e-aprovacao.md, "Dados"; critérios 7 e 11): estados e chave de idempotência da AcaoBot.
    [Trait("Fatia", "P2")]
    public class AcaoBotEntityTests
    {
        private static readonly DateTime T0 = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        private static AcaoBotEntity Nova(TipoAcaoBot tipo = TipoAcaoBot.AbrirReview, int tentativa = 1, string? comentario = null)
            => AcaoBotEntity.Criar(7, "p2", 11, 42, tipo, tentativa, null, 5, comentario, T0).Value;

        private static AcaoBotEntity Em(EstadoAcaoBot estado)
        {
            var a = Nova();
            switch (estado)
            {
                case EstadoAcaoBot.Executando: a.Iniciar(T0); break;
                case EstadoAcaoBot.Confirmada: a.Iniciar(T0); a.Confirmar(T0); break;
                case EstadoAcaoBot.Falhou: a.Iniciar(T0); a.Falhar("gitlab.erro", T0); break;
                case EstadoAcaoBot.Cancelada: a.Cancelar(T0); break;
            }
            return a;
        }

        [Fact, DisplayName("Deve nascer pedida, com PublicId v7 e a chave <aplicação>:<fatia>:<tipo>:<projeto>:<mr>:<tentativa>")]
        public void Criar_NascePedidaComChave()
        {
            var a = Nova(TipoAcaoBot.Aprovar, 2);

            a.Should().BeAssignableTo<EntityBase>();
            a.PublicId.Version.Should().Be(7);
            a.Estado.Should().Be(EstadoAcaoBot.Pedida);
            a.ChaveIdempotencia.Should().Be("7:p2:aprovar:11:42:2");
            a.PedidaEm.Should().Be(T0);
            a.AtualizadaEm.Should().Be(T0);
        }

        [Fact, DisplayName("Deve gerar a mesma chave para a mesma decisão repetida (duplo clique) e outra para decisão diferente")]
        public void Chave_MesmaDecisaoMesmaChave()
        {
            Nova(TipoAcaoBot.Aprovar).ChaveIdempotencia.Should().Be(Nova(TipoAcaoBot.Aprovar).ChaveIdempotencia);
            Nova(TipoAcaoBot.Aprovar).ChaveIdempotencia.Should().NotBe(Nova(TipoAcaoBot.Reprovar, comentario: "x").ChaveIdempotencia);
            Nova(TipoAcaoBot.Aprovar, 1).ChaveIdempotencia.Should().NotBe(Nova(TipoAcaoBot.Aprovar, 2).ChaveIdempotencia);
        }

        [Theory, DisplayName("Deve aplicar as transições válidas")]
        [InlineData(EstadoAcaoBot.Pedida, "iniciar", EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Pedida, "cancelar", EstadoAcaoBot.Cancelada)]
        [InlineData(EstadoAcaoBot.Executando, "confirmar", EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Executando, "falhar", EstadoAcaoBot.Falhou)]
        public void Transicoes_Validas(EstadoAcaoBot origem, string operacao, EstadoAcaoBot destino)
        {
            var a = Em(origem);
            var quando = T0.AddMinutes(3);

            var r = Aplicar(a, operacao, quando);

            r.IsSuccess.Should().BeTrue();
            a.Estado.Should().Be(destino);
            a.AtualizadaEm.Should().Be(quando);
        }

        [Theory, DisplayName("Deve recusar qualquer outra transição, sem mudar o estado")]
        [InlineData(EstadoAcaoBot.Pedida, "confirmar")]
        [InlineData(EstadoAcaoBot.Pedida, "falhar")]
        [InlineData(EstadoAcaoBot.Executando, "iniciar")]
        [InlineData(EstadoAcaoBot.Executando, "cancelar")]
        [InlineData(EstadoAcaoBot.Confirmada, "iniciar")]
        [InlineData(EstadoAcaoBot.Confirmada, "confirmar")]
        [InlineData(EstadoAcaoBot.Confirmada, "falhar")]
        [InlineData(EstadoAcaoBot.Confirmada, "cancelar")]
        [InlineData(EstadoAcaoBot.Falhou, "iniciar")]
        [InlineData(EstadoAcaoBot.Falhou, "confirmar")]
        [InlineData(EstadoAcaoBot.Falhou, "falhar")]
        [InlineData(EstadoAcaoBot.Falhou, "cancelar")]
        [InlineData(EstadoAcaoBot.Cancelada, "iniciar")]
        [InlineData(EstadoAcaoBot.Cancelada, "confirmar")]
        [InlineData(EstadoAcaoBot.Cancelada, "falhar")]
        [InlineData(EstadoAcaoBot.Cancelada, "cancelar")]
        public void Transicoes_Invalidas(EstadoAcaoBot origem, string operacao)
        {
            var a = Em(origem);
            var atualizadaEm = a.AtualizadaEm;

            var r = Aplicar(a, operacao, T0.AddHours(1));

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "acao-bot.transicao-invalida" && e.Type == TipoErro.Conflict);
            a.Estado.Should().Be(origem);
            a.AtualizadaEm.Should().Be(atualizadaEm);
        }

        [Fact, DisplayName("Deve guardar o código de erro ao falhar e nenhum nas demais")]
        public void Falhar_GuardaCodigoDeErro()
        {
            var falha = Em(EstadoAcaoBot.Falhou);
            falha.CodigoErro.Should().Be("gitlab.erro");

            Em(EstadoAcaoBot.Confirmada).CodigoErro.Should().BeNull();
        }

        [Theory, DisplayName("Deve exigir comentário de 1 a 2000 caracteres só no reprovar")]
        [InlineData(TipoAcaoBot.Reprovar, null, false)]
        [InlineData(TipoAcaoBot.Reprovar, "  ", false)]
        [InlineData(TipoAcaoBot.Reprovar, "ok", true)]
        [InlineData(TipoAcaoBot.Aprovar, "não cabe", false)]
        [InlineData(TipoAcaoBot.AbrirReview, "não cabe", false)]
        [InlineData(TipoAcaoBot.Aprovar, null, true)]
        public void Criar_Comentario(TipoAcaoBot tipo, string? comentario, bool valido)
        {
            var r = AcaoBotEntity.Criar(7, "p2", 11, 42, tipo, 1, null, 5, comentario, T0);

            r.IsSuccess.Should().Be(valido);
            if (!valido)
                r.Errors.Should().ContainSingle().Which.Code.Should().Be("fatia.comentario-invalido");
        }

        [Fact, DisplayName("Deve aceitar comentário de 2000 caracteres e recusar 2001")]
        public void Criar_ComentarioLimite()
        {
            AcaoBotEntity.Criar(7, "p2", 11, 42, TipoAcaoBot.Reprovar, 1, null, 5, new string('a', 2000), T0).IsSuccess.Should().BeTrue();
            AcaoBotEntity.Criar(7, "p2", 11, 42, TipoAcaoBot.Reprovar, 1, null, 5, new string('a', 2001), T0).IsSuccess.Should().BeFalse();
        }

        [Fact, DisplayName("Deve guardar a dependência da Ação da API na Ação do front")]
        public void Criar_Dependencia()
        {
            AcaoBotEntity.Criar(7, "p2", 12, 43, TipoAcaoBot.Aprovar, 1, 99, 5, null, T0).Value.DependeDeAcaoId.Should().Be(99);
            Nova().DependeDeAcaoId.Should().BeNull();
        }

        private static Result Aplicar(AcaoBotEntity a, string operacao, DateTime quando) => operacao switch
        {
            "iniciar" => a.Iniciar(quando),
            "confirmar" => a.Confirmar(quando),
            "falhar" => a.Falhar("x.erro", quando),
            "cancelar" => a.Cancelar(quando),
            _ => throw new ArgumentOutOfRangeException(nameof(operacao))
        };
    }
}
