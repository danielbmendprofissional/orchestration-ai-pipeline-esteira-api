using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Mappers;
using Esteira.Application.DTOs.Request;

namespace Esteira.Application.Tests.Deploys.Mappers
{
    public class ArtefatoMapperTests
    {
        [Fact, DisplayName("Deve mapear o relatório do Semgrep para artefatos")]
        public void Semgrep_DeveMapear()
        {
            var semgrep = new SemgrepDtoRequest
            {
                Results = new()
                {
                    new ResultSemgrepDto
                    {
                        Path = "src/app.cs",
                        Start = new StartSemgrepDto { Line = 10 },
                        End = new EndSemgrepDto { Line = 12 },
                        Extra = new ExtraSemgrepDto
                        {
                            Severity = "ERROR",
                            Message = "mensagem",
                            Metadata = new MetadataSemgrepDto
                            {
                                References = new() { "https://ref" },
                                Cwe = new() { "CWE-79" },
                                Owasp = new() { "A03" },
                                VulnerabilityClass = new() { "XSS", "Outro" }
                            }
                        }
                    }
                }
            };

            var artefato = ((ArtefatoAbstractDtoRequest)semgrep).ParaArtefatos().Should().ContainSingle().Subject;

            artefato.Severidade.Should().Be("ERROR");
            artefato.LocalArquivo.Should().Be("src/app.cs");
            artefato.TituloVulnerabilidade.Should().Be("XSS");
            artefato.CatalogoCWE.Should().Equal("CWE-79");
            artefato.CatalogoOWASP.Should().Equal("A03");
            artefato.Referencias.Should().Equal("https://ref");
            artefato.DescricaoVulnerabilidade.Should().Be("mensagem");
            artefato.Linha.Should().Be("10 a 12");
        }

        [Fact, DisplayName("Deve mapear o relatório do Trivy colocando a URL principal primeiro e sem referências repetidas")]
        public void Trivy_DeveMapear()
        {
            var trivy = new TrivyDtoRequest
            {
                Results = new()
                {
                    new ResultTrivyDto
                    {
                        Vulnerabilities = new()
                        {
                            new VulnerabilityTrivyDto
                            {
                                Severity = "HIGH",
                                PkgName = "lib",
                                InstalledVersion = "1.0",
                                FixedVersion = "1.1",
                                Title = "titulo",
                                Description = "descricao",
                                PrimaryURL = "https://principal",
                                References = new() { "https://outra", "https://principal" },
                                CweIDs = new() { "CWE-1" }
                            }
                        }
                    },
                    new ResultTrivyDto { Vulnerabilities = null }
                }
            };

            var artefato = ((ArtefatoAbstractDtoRequest)trivy).ParaArtefatos().Should().ContainSingle().Subject;

            artefato.Severidade.Should().Be("HIGH");
            artefato.LocalArquivo.Should().Be("lib@1.0");
            artefato.PacoteCorrecao.Should().Be("1.1");
            artefato.Referencias.Should().Equal("https://principal", "https://outra");
            artefato.CatalogoCWE.Should().Equal("CWE-1");
        }

        [Fact, DisplayName("Deve retornar lista vazia quando não houver relatório")]
        public void SemRelatorio_DeveRetornarVazio()
        {
            ((ArtefatoAbstractDtoRequest)null).ParaArtefatos().Should().BeEmpty();
            new SemgrepDtoRequest().ParaArtefatos().Should().BeEmpty();
            new TrivyDtoRequest().ParaArtefatos().Should().BeEmpty();
        }
    }
}
