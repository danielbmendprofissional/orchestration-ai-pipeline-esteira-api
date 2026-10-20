using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response;
using Esteira.Domain.Deploys.ValueObjects;

namespace Esteira.Application.Deploys.Mappers
{
    public static class ArtefatoMapper
    {
        public static List<ArtefatoCompletoDto> ParaArtefatos(this ArtefatoAbstractDtoRequest request)
            => request switch
            {
                SemgrepDtoRequest semgrep => semgrep.ParaArtefatos(),
                TrivyDtoRequest trivy => trivy.ParaArtefatos(),
                _ => new List<ArtefatoCompletoDto>()
            };

        public static List<ArtefatoCompletoDto> ParaArtefatos(this SemgrepDtoRequest request)
            => (request.Results ?? Enumerable.Empty<ResultSemgrepDto>())
                .Select(item => new ArtefatoCompletoDto
                {
                    Severidade = item.Extra.Severity,
                    Referencias = item.Extra.Metadata.References,
                    LocalArquivo = item.Path,
                    TituloVulnerabilidade = item.Extra.Metadata.VulnerabilityClass?.FirstOrDefault(),
                    CatalogoCWE = item.Extra.Metadata.Cwe,
                    CatalogoOWASP = item.Extra.Metadata.Owasp,
                    DescricaoVulnerabilidade = item.Extra.Message,
                    Linha = $"{item.Start.Line} a {item.End.Line}"
                })
                .ToList();

        public static List<ArtefatoCompletoDto> ParaArtefatos(this TrivyDtoRequest request)
            => (request.Results ?? Enumerable.Empty<ResultTrivyDto>())
                .SelectMany(result => result.Vulnerabilities ?? Enumerable.Empty<VulnerabilityTrivyDto>())
                .Select(item => new ArtefatoCompletoDto
                {
                    Severidade = item.Severity,
                    Referencias = new[] { item.PrimaryURL }
                        .Concat(item.References ?? Enumerable.Empty<string>())
                        .Distinct()
                        .ToList(),
                    LocalArquivo = $"{item.PkgName}@{item.InstalledVersion}",
                    TituloVulnerabilidade = item.Title,
                    CatalogoCWE = item.CweIDs,
                    PacoteCorrecao = item.FixedVersion,
                    DescricaoVulnerabilidade = item.Description
                })
                .ToList();

        public static IReadOnlyList<DadosArtefatoValueObject> ParaDadosArtefato(this IEnumerable<ArtefatoCompletoDto> artefatos)
            => (artefatos ?? Enumerable.Empty<ArtefatoCompletoDto>())
                .Select(a => new DadosArtefatoValueObject(
                    a.Severidade,
                    a.Referencias,
                    a.LocalArquivo,
                    a.CatalogoCWE,
                    a.CatalogoOWASP,
                    a.CatalogoCVE,
                    a.TituloVulnerabilidade,
                    a.DescricaoVulnerabilidade,
                    a.PacoteCorrecao,
                    a.Linha))
                .ToList();
    }
}
