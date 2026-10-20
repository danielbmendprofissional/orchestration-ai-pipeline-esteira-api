using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
namespace Esteira.Domain.Tests.Deploys.Entities
{
    public class ArtefatoEntityTests
    {
        [Fact, DisplayName("Deve criar o artefato com os dados informados e a ferramenta da análise")]
        public void AdicionarAnalise_DeveCriarArtefatoComDadosInformados()
        {
            var dados = new DadosArtefatoValueObject(
                Severidade: "HIGH",
                Referencias: new List<string> { "https://ref" },
                LocalArquivo: "pkg@1.0",
                CatalogoCWE: new List<string> { "CWE-79" },
                CatalogoOWASP: new List<string> { "A03" },
                CatalogoCVE: new List<string> { "CVE-1" },
                TituloVulnerabilidade: "titulo",
                DescricaoVulnerabilidade: "descricao",
                PacoteCorrecao: "1.1",
                Linha: "1 a 2");

            var analise = FluxoTestesSuporte.CriarDeploy()
                .AdicionarAnalise("TRIVY", "SCA", 1, "v1.0.0", null, StatusEnum.CONCLUIDO, new[] { dados });

            var artefato = analise.Artefatos.Should().ContainSingle().Subject;
            artefato.Severidade.Should().Be("HIGH");
            artefato.Referencias.Should().Equal("https://ref");
            artefato.LocalArquivo.Should().Be("pkg@1.0");
            artefato.CatalogoCWE.Should().Equal("CWE-79");
            artefato.CatalogoOWASP.Should().Equal("A03");
            artefato.CatalogoCVE.Should().Equal("CVE-1");
            artefato.TituloVulnerabilidade.Should().Be("titulo");
            artefato.DescricaoVulnerabilidade.Should().Be("descricao");
            artefato.PacoteCorrecao.Should().Be("1.1");
            artefato.Linha.Should().Be("1 a 2");
            artefato.Ferramenta.Should().Be("TRIVY");
        }

        [Fact, DisplayName("Deve manter nulos os dados opcionais não informados")]
        public void AdicionarAnalise_DadosOpcionais_DevemSerNulos()
        {
            var analise = FluxoTestesSuporte.CriarDeploy()
                .AdicionarAnalise("TRIVY", "SCA", 1, "v1.0.0", null, StatusEnum.CONCLUIDO, FluxoTestesSuporte.Artefatos("LOW"));

            var artefato = analise.Artefatos.Single();
            artefato.Referencias.Should().BeNull();
            artefato.PacoteCorrecao.Should().BeNull();
            artefato.Linha.Should().BeNull();
        }
    }
}
