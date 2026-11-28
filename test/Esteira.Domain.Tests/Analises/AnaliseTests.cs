using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Tests.Analises
{
    // E2 (spec 03-analises-e-gate.md, "Dados" e "Domínio": AnaliseEntity.Criar e ChaveDoTipo; critérios 4 e 15).
    [Trait("Fatia", "E2")]
    public class AnaliseTests
    {
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

        private static NovoAchado Achado(string titulo = "SQL injection", string? regra = "csharp.sqli", SeveridadeAchado sev = SeveridadeAchado.Alta,
            string? arquivo = "src/Repo.cs", int? linha = 42, string? descricao = "texto")
            => new(sev, titulo, regra, arquivo, linha, descricao);

        private static Result<AnaliseEntity> Criar(
            string fonte = "opengrep", TipoAnalise tipo = TipoAnalise.Ferramenta, EscopoAnalise escopo = EscopoAnalise.Artefato,
            string? destino = null, decimal? custo = null, IReadOnlyList<NovoAchado>? achados = null)
            => AnaliseEntity.Criar(10, AmbienteDeploy.Qa, escopo, destino, fonte, tipo, VereditoAnalise.Reprovado, custo, 2841, T0, achados ?? [Achado()]);

        [Fact, DisplayName("Criar válida guarda os campos e os Achados")]
        public void CriaValida()
        {
            var r = Criar();

            r.IsSuccess.Should().BeTrue();
            var a = r.Value;
            (a.FluxoId, a.Ambiente, a.Escopo, a.Fonte, a.Tipo, a.Veredito, a.PipelineGitLabId, a.RecebidaEm)
                .Should().Be((10L, AmbienteDeploy.Qa, EscopoAnalise.Artefato, "opengrep", TipoAnalise.Ferramenta, VereditoAnalise.Reprovado, 2841L, T0));
            (a.DestinoNome, a.CustoUsd).Should().Be(((string?)null, (decimal?)null));
            var achado = a.Achados.Should().ContainSingle().Which;
            (achado.Severidade, achado.Titulo, achado.RegraId, achado.Arquivo, achado.Linha, achado.Descricao)
                .Should().Be((SeveridadeAchado.Alta, "SQL injection", "csharp.sqli", "src/Repo.cs", (int?)42, "texto"));
        }

        [Fact, DisplayName("Critério 15: custo_usd de agente é guardado, inclusive 0 e valores altos (a Esteira não recusa)")]
        public void GuardaCusto()
        {
            Criar("agent-security", TipoAnalise.Agente, custo: 0.31m).Value.CustoUsd.Should().Be(0.31m);
            Criar("agent-qa", TipoAnalise.Agente, custo: 0m).Value.CustoUsd.Should().Be(0m);
            Criar("agent-qa", TipoAnalise.Agente, custo: 9999m).Value.CustoUsd.Should().Be(9999m);
        }

        [Fact, DisplayName("custo_usd negativo é recusado")]
        public void CustoNegativo()
            => Criar("agent-qa", TipoAnalise.Agente, custo: -0.01m).Errors.Should().ContainSingle().Which.Code.Should().Be("analise.custo-invalido");

        [Theory, DisplayName("Fonte fora de [a-z0-9-] é recusada; fonte desconhecida válida é aceita")]
        [InlineData("", false)]
        [InlineData("Opengrep", false)]
        [InlineData("trivy_fs", false)]
        [InlineData("a b", false)]
        [InlineData("fonte-nova-9", true)]
        public void Fonte(string fonte, bool valida)
        {
            var r = Criar(fonte);

            r.IsSuccess.Should().Be(valida);
            if (!valida) r.Errors.Should().ContainSingle().Which.Code.Should().Be("analise.fonte-invalida");
        }

        [Fact, DisplayName("Fonte com 65 caracteres é recusada; com 64 é aceita")]
        public void FonteTamanho()
        {
            Criar(new string('a', 65)).IsSuccess.Should().BeFalse();
            Criar(new string('a', 64)).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("destino só no escopo deploy; no deploy aceita até 100 caracteres")]
        public void Destino()
        {
            Criar(escopo: EscopoAnalise.Artefato, destino: "api").Errors.Should().ContainSingle().Which.Code.Should().Be("analise.destino-invalido");
            Criar(escopo: EscopoAnalise.Deploy, destino: "api").Value.DestinoNome.Should().Be("api");
            Criar(escopo: EscopoAnalise.Deploy, destino: new string('d', 101)).Errors.Should().ContainSingle().Which.Code.Should().Be("analise.destino-invalido");
        }

        [Fact, DisplayName("Mais de 2000 Achados é recusado; 2000 é aceito")]
        public void AchadosDemais()
        {
            var muitos = Enumerable.Range(0, 2001).Select(_ => Achado()).ToList();

            Criar(achados: muitos).Errors.Should().ContainSingle().Which.Code.Should().Be("analise.achados-demais");
            Criar(achados: muitos.Take(2000).ToList()).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Achado: título vazio, título > 300, regra > 200, arquivo > 500, linha < 1 e descrição > 2000 são recusados")]
        public void AchadoExcessos()
        {
            foreach (var ruim in new[]
            {
                Achado(titulo: ""),
                Achado(titulo: new string('t', 301)),
                Achado(regra: new string('r', 201)),
                Achado(arquivo: new string('a', 501)),
                Achado(linha: 0),
                Achado(descricao: new string('d', 2001))
            })
                Criar(achados: [ruim]).Errors.Should().ContainSingle().Which.Code.Should().Be("achado.invalido");
        }

        [Fact, DisplayName("Achado no limite exato (300/200/500/2000, linha 1) é aceito")]
        public void AchadoNoLimite()
            => Criar(achados: [Achado(titulo: new string('t', 300), regra: new string('r', 200), arquivo: new string('a', 500), linha: 1, descricao: new string('d', 2000))])
                .IsSuccess.Should().BeTrue();

        [Fact, DisplayName("ChaveDoTipo com regra_id: <fonte>:<regra_id>")]
        public void ChaveComRegra()
            => Criar(achados: [Achado(regra: "csharp.sqli")]).Value.Achados[0].ChaveDoTipo.Should().Be("opengrep:csharp.sqli");

        [Fact, DisplayName("ChaveDoTipo sem regra_id: <fonte>:<título em minúsculas, espaços colapsados>")]
        public void ChaveSemRegra()
        {
            var a = Criar(achados: [Achado(titulo: "  SQL   Injection\tNo Repo ", regra: null), Achado(titulo: "X", regra: "  ")]).Value;

            a.Achados[0].ChaveDoTipo.Should().Be("opengrep:sql injection no repo");
            a.Achados[1].ChaveDoTipo.Should().Be("opengrep:x");
        }

        [Fact, DisplayName("ChaveDoTipo nunca passa de 300 caracteres")]
        public void ChaveLimitada()
            => Criar(achados: [Achado(titulo: new string('T', 300), regra: null)]).Value.Achados[0].ChaveDoTipo.Length.Should().BeLessThanOrEqualTo(300);
    }
}
