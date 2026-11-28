using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Analises
{
    // E2 (spec 03-analises-e-gate.md, "Domínio": AvaliadorDoGate, função pura; critérios 1 a 4).
    [Trait("Fatia", "E2")]
    public class AvaliadorDoGateTests
    {
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        private static readonly IReadOnlySet<string> SemExcecoes = new HashSet<string>();

        private static AnaliseEntity Analise(string fonte = "opengrep", TipoAnalise tipo = TipoAnalise.Ferramenta,
            VereditoAnalise veredito = VereditoAnalise.Reprovado, params SeveridadeAchado[] severidades)
            => AnaliseEntity.Criar(1, AmbienteDeploy.Qa, EscopoAnalise.Artefato, null, fonte, tipo, veredito, null, 1, T0,
                severidades.Select((s, i) => new NovoAchado(s, $"achado {i}", $"regra-{i}", null, null, null)).ToList()).Value;

        private static AnaliseEntity Com(params SeveridadeAchado[] severidades)
            => Analise(severidades: severidades);

        private static PoliticaSeveridadeEntity Politica(int? c, int? a, int? m, int? b, ModoPolitica modo = ModoPolitica.Bloqueia)
            => PoliticaSeveridadeEntity.Criar(1, AmbientePolitica.Qa, c, a, m, b, modo).Value;

        private static AvaliacaoDoGate Avaliar(IReadOnlyList<AnaliseEntity> analises, PoliticaSeveridadeEntity politica,
            MomentoDoGate momento = MomentoDoGate.Gate, IReadOnlySet<string>? excecoes = null)
            => AvaliadorDoGate.Avaliar(analises, politica, excecoes ?? SemExcecoes, momento);

        [Fact, DisplayName("Sem achados: liberado, contagem zerada, sem motivo")]
        public void Liberado()
        {
            var r = Avaliar([Analise(veredito: VereditoAnalise.Aprovado)], Politica(0, 0, null, null));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Liberado, (string?)null));
            r.Contagem.Should().Be(new ContagemDeSeveridade(0, 0, 0, 0));
        }

        [Fact, DisplayName("Crítica acima do limite com Modo bloqueia: bloqueado, gate.bloqueado no gate")]
        public void BloqueiaNoGate()
        {
            var r = Avaliar([Com(SeveridadeAchado.Critica)], Politica(0, 0, null, null));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Bloqueado, "gate.bloqueado"));
            r.Contagem.Should().Be(new ContagemDeSeveridade(1, 0, 0, 0));
        }

        [Fact, DisplayName("No resultado o motivo é resultado.bloqueado")]
        public void BloqueiaNoResultado()
        {
            var r = Avaliar([Com(SeveridadeAchado.Alta)], Politica(0, 0, null, null), MomentoDoGate.Resultado);

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Bloqueado, "resultado.bloqueado"));
        }

        [Fact, DisplayName("Limite null = sem limite; contagem soma as Análises")]
        public void LimiteNuloESoma()
        {
            var analises = new[]
            {
                Com(SeveridadeAchado.Media, SeveridadeAchado.Media, SeveridadeAchado.Baixa),
                Analise("trivy-fs", severidades: [SeveridadeAchado.Media, SeveridadeAchado.Baixa])
            };

            var r = Avaliar(analises, Politica(0, 0, null, null));

            r.Decisao.Should().Be(DecisaoGate.Liberado);
            r.Contagem.Should().Be(new ContagemDeSeveridade(0, 0, 3, 2));
        }

        [Fact, DisplayName("Limite de média excedido bloqueia; igual ao limite não")]
        public void LimiteDeMedia()
        {
            Avaliar([Com(SeveridadeAchado.Media, SeveridadeAchado.Media)], Politica(0, 0, 2, null)).Decisao.Should().Be(DecisaoGate.Liberado);
            Avaliar([Com(SeveridadeAchado.Media, SeveridadeAchado.Media, SeveridadeAchado.Media)], Politica(0, 0, 2, null)).Decisao.Should().Be(DecisaoGate.Bloqueado);
        }

        [Fact, DisplayName("Excesso com Modo avisa: avisou, sem motivo")]
        public void Avisa()
        {
            var r = Avaliar([Com(SeveridadeAchado.Alta)], Politica(0, 0, null, null, ModoPolitica.Avisa));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Avisou, (string?)null));
        }

        [Fact, DisplayName("info é guardada, nunca conta (nem com todos os limites em 0)")]
        public void InfoNaoConta()
        {
            var r = Avaliar([Com(SeveridadeAchado.Info, SeveridadeAchado.Info)], Politica(0, 0, 0, 0));

            r.Decisao.Should().Be(DecisaoGate.Liberado);
            r.Contagem.Should().Be(new ContagemDeSeveridade(0, 0, 0, 0));
        }

        [Fact, DisplayName("Exceção vigente desconta os Achados cobertos pela ChaveDoTipo")]
        public void ExcecaoDesconta()
        {
            var analise = Com(SeveridadeAchado.Critica, SeveridadeAchado.Alta);
            IReadOnlySet<string> uma = new HashSet<string> { "opengrep:regra-0" };

            var r = Avaliar([analise], Politica(0, 0, null, null), excecoes: uma);

            r.Contagem.Should().Be(new ContagemDeSeveridade(0, 1, 0, 0));
            r.Decisao.Should().Be(DecisaoGate.Bloqueado);

            IReadOnlySet<string> tudo = new HashSet<string> { "opengrep:regra-0", "opengrep:regra-1" };
            var liberado = Avaliar([analise], Politica(0, 0, null, null), excecoes: tudo);
            (liberado.Decisao, liberado.Contagem).Should().Be((DecisaoGate.Liberado, new ContagemDeSeveridade(0, 0, 0, 0)));
        }

        [Fact, DisplayName("Gate: erro de ferramenta bloqueia com gate.analise-com-erro (fail-closed), mesmo sem Achados")]
        public void ErroDeFerramenta()
        {
            var r = Avaliar([Analise(veredito: VereditoAnalise.Erro)], Politica(0, 0, null, null));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Bloqueado, "gate.analise-com-erro"));
        }

        [Fact, DisplayName("Gate: erro de agente só é registrado; decide pelas contagens")]
        public void ErroDeAgente()
        {
            var r = Avaliar([Analise("agent-security", TipoAnalise.Agente, VereditoAnalise.Erro)], Politica(0, 0, null, null));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Liberado, (string?)null));
        }

        [Fact, DisplayName("Resultado: erro de ferramenta não bloqueia por si (a regra é só do gate)")]
        public void ErroNoResultado()
        {
            var r = Avaliar([Analise(veredito: VereditoAnalise.Erro)], Politica(0, 0, null, null), MomentoDoGate.Resultado);

            r.Decisao.Should().Be(DecisaoGate.Liberado);
        }

        [Fact, DisplayName("Lista vazia: bloqueado com gate.sem-analises (fail-closed)")]
        public void ListaVazia()
        {
            var r = Avaliar([], Politica(0, 0, null, null));

            (r.Decisao, r.Motivo).Should().Be((DecisaoGate.Bloqueado, "gate.sem-analises"));
            r.Contagem.Should().Be(new ContagemDeSeveridade(0, 0, 0, 0));
        }
    }
}
