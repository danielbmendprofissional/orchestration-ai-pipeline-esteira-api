using Esteira.Domain.Deploys.Entities;
namespace Esteira.Domain.Deploys.ValueObjects
{
    public sealed record DadosArtefatoValueObject(
        string Severidade,
        List<string> Referencias = null,
        string LocalArquivo = null,
        List<string> CatalogoCWE = null,
        List<string> CatalogoOWASP = null,
        List<string> CatalogoCVE = null,
        string TituloVulnerabilidade = null,
        string DescricaoVulnerabilidade = null,
        string PacoteCorrecao = null,
        string Linha = null);
}
