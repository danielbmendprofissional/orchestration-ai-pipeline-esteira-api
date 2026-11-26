using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;

namespace Esteira.Application.Tests.Fatias
{
    // P2b.5, critério 18 (lado da API): a gaveta devolve `podeReexecutar` por Ação, com o motivo quando não pode.
    [Trait("Fatia", "P2b")]
    public class PodeReexecutarTests
    {
        private static readonly AplicacaoNoPainel App = new(
            Guid.NewGuid(), "App", "APP", "g", 10, "g/ws",
            [new ProjetoNoPainel(RepositorioFatia.Api, 1, "g/api", 100), new ProjetoNoPainel(RepositorioFatia.Frontend, 2, "g/front", 200)]);

        private static readonly DateTime T0 = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        private static AcessoAplicacao Acesso(params PapelAplicacao[] papeis) => new(false, papeis);

        private static FatiaLida Fatia(EstadoDaFatia estado = EstadoDaFatia.EmRevisao, string mrApi = "opened") => new(
            "P2", "T", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x.md", 7,
            new ResultadoDeEstado(estado, null, false, false), false,
            new DetalheDaFatia(null, null, [], [], [
                new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — T", mrApi, "feature/p2", "success"),
                new MrDaGaveta(RepositorioFatia.Frontend, 8, "P2 — T", "opened", "feature/p2", "success")]));

        private static AcaoNaFatia Acao(TipoAcaoBot tipo, EstadoAcaoBot estado, int minutos, RepositorioFatia repo = RepositorioFatia.Api)
            => new(Guid.NewGuid(), "P2", tipo, estado, repo == RepositorioFatia.Api ? 100 : 200, repo, repo == RepositorioFatia.Api ? 3 : 8,
                "Ana", T0.AddMinutes(minutos), T0.AddMinutes(minutos + 1), estado == EstadoAcaoBot.Falhou ? "gitlab.indisponivel" : null,
                tipo == TipoAcaoBot.Reprovar ? "x" : null);

        private static PermissaoDaAcaoDto PodeReexecutar(AcessoAplicacao acesso, FatiaLida fatia, AcaoNaFatia alvo, params AcaoNaFatia[] outras)
            => DerivadorDeRevisao.Derivar(acesso, App, fatia, [alvo, .. outras], null).Lista.Single(a => a.Id == alvo.Id).PodeReexecutar;

        [Theory, DisplayName("18: Ação falhou e última do tipo na MR, com papel da operação: pode")]
        [InlineData(TipoAcaoBot.Aprovar, PapelAplicacao.AprovadorTecnico)]
        [InlineData(TipoAcaoBot.Reprovar, PapelAplicacao.AprovadorTecnico)]
        [InlineData(TipoAcaoBot.AbrirReview, PapelAplicacao.Membro)]
        public void Pode(TipoAcaoBot tipo, PapelAplicacao papel)
        {
            PodeReexecutar(Acesso(papel), Fatia(), Acao(tipo, EstadoAcaoBot.Falhou, 0)).Should().Be(new PermissaoDaAcaoDto(true, null));
        }

        [Theory, DisplayName("18: papel sem a operação do pedido original: motivo papel")]
        [InlineData(TipoAcaoBot.Aprovar, PapelAplicacao.Membro)]
        [InlineData(TipoAcaoBot.Reprovar, PapelAplicacao.Membro)]
        [InlineData(TipoAcaoBot.AbrirReview, PapelAplicacao.Analista)]
        public void Sem_a_operacao(TipoAcaoBot tipo, PapelAplicacao papel)
        {
            PodeReexecutar(Acesso(papel), Fatia(), Acao(tipo, EstadoAcaoBot.Falhou, 0)).Should().Be(new PermissaoDaAcaoDto(false, "papel"));
        }

        [Theory, DisplayName("18: Ação que não falhou: motivo nao-falhou")]
        [InlineData(EstadoAcaoBot.Pedida)]
        [InlineData(EstadoAcaoBot.Executando)]
        [InlineData(EstadoAcaoBot.Confirmada)]
        [InlineData(EstadoAcaoBot.Cancelada)]
        public void Nao_falhou(EstadoAcaoBot estado)
        {
            PodeReexecutar(Acesso(PapelAplicacao.AprovadorTecnico), Fatia(), Acao(TipoAcaoBot.Aprovar, estado, 0))
                .Should().Be(new PermissaoDaAcaoDto(false, "nao-falhou"));
        }

        [Fact, DisplayName("18: Ação falhada que já tem outra mais nova do mesmo tipo na MR: motivo nao-e-a-ultima")]
        public void Nao_e_a_ultima()
        {
            var velha = Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 0);

            PodeReexecutar(Acesso(PapelAplicacao.AprovadorTecnico), Fatia(), velha, Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Confirmada, 10))
                .Should().Be(new PermissaoDaAcaoDto(false, "nao-e-a-ultima"));
        }

        [Theory, DisplayName("18: Fatia fora de Em revisão ou MR da Ação não aberta: motivo sem-mr-aberta")]
        [InlineData(EstadoDaFatia.EmExecucao, "opened")]
        [InlineData(EstadoDaFatia.EmRevisao, "merged")]
        public void Sem_mr_aberta(EstadoDaFatia estado, string mr)
        {
            PodeReexecutar(Acesso(PapelAplicacao.AprovadorTecnico), Fatia(estado, mr), Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 0))
                .Should().Be(new PermissaoDaAcaoDto(false, "sem-mr-aberta"));
        }

        [Fact, DisplayName("18: decisão (aprovar ou reprovar) viva na Fatia: motivo decisao-em-andamento")]
        public void Decisao_em_andamento()
        {
            PodeReexecutar(Acesso(PapelAplicacao.AprovadorTecnico), Fatia(), Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou, 0),
                    Acao(TipoAcaoBot.Reprovar, EstadoAcaoBot.Pedida, 10))
                .Should().Be(new PermissaoDaAcaoDto(false, "decisao-em-andamento"));
        }

        [Fact, DisplayName("18: MOR pode reexecutar qualquer Ação falhada reexecutável")]
        public void Mor_pode()
        {
            PodeReexecutar(new AcessoAplicacao(true, []), Fatia(), Acao(TipoAcaoBot.Reprovar, EstadoAcaoBot.Falhou, 0))
                .Should().Be(new PermissaoDaAcaoDto(true, null));
        }
    }
}
