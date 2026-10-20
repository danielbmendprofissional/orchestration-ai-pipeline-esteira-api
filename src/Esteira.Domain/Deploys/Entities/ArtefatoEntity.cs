using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Deploys.ValueObjects;
namespace Esteira.Domain.Deploys.Entities
{
    public class ArtefatoEntity : EntityBase
    {
        private ArtefatoEntity() { }

        public int IdAnalise { get; private set; }
        public List<string> Referencias { get; private set; }
        public string Severidade { get; private set; }
        public string LocalArquivo { get; private set; }
        public List<string> CatalogoCWE { get; private set; }
        public List<string> CatalogoOWASP { get; private set; }
        public List<string> CatalogoCVE { get; private set; }
        public string TituloVulnerabilidade { get; private set; }
        public string DescricaoVulnerabilidade { get; private set; }
        public string PacoteCorrecao { get; private set; }
        public string Linha { get; private set; }
        public string Ferramenta { get; private set; }

        internal ArtefatoEntity(DadosArtefatoValueObject dados, string ferramenta)
        {
            Guard.AgainstNull(dados, nameof(dados));

            Severidade = dados.Severidade;
            Referencias = dados.Referencias;
            LocalArquivo = dados.LocalArquivo;
            CatalogoCWE = dados.CatalogoCWE;
            CatalogoOWASP = dados.CatalogoOWASP;
            CatalogoCVE = dados.CatalogoCVE;
            TituloVulnerabilidade = dados.TituloVulnerabilidade;
            DescricaoVulnerabilidade = dados.DescricaoVulnerabilidade;
            PacoteCorrecao = dados.PacoteCorrecao;
            Linha = dados.Linha;
            Ferramenta = ferramenta;
        }
    }
}
