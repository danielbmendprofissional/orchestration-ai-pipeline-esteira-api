using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.Deploys.ValueObjects;
namespace Esteira.Domain.Deploys.Entities
{
    public class AnaliseEntity : EntityBase
    {
        public static readonly StatusEnum[] StatusConsiderados = { StatusEnum.CONCLUIDO, StatusEnum.EXECUTANDO, StatusEnum.ERRO };
        public const string TipoDinamica = "DAST";

        private AnaliseEntity() { }

        public int IdDeploy { get; private set; }
        public string Ferramenta { get; private set; }
        public string TipoAnalise { get; private set; }
        public int IdPipeline { get; private set; }
        public string Tag { get; private set; }
        public string ScanId { get; private set; }
        public string UrlAlvo { get; private set; }
        public StatusEnum Status { get; private set; }
        public ResumoSeveridadeValueObject Resumo { get; private set; }

        private readonly List<ArtefatoEntity> _artefatos = new();
        public IReadOnlyCollection<ArtefatoEntity> Artefatos => _artefatos;


        internal AnaliseEntity(
            string ferramenta,
            string tipoAnalise,
            int idPipeline,
            string tag,
            string scanId,
            StatusEnum status,
            string urlAlvo = null)
        {
            Guard.Against<DomainException>(string.IsNullOrWhiteSpace(ferramenta), "Ferramenta é obrigatória");
            Guard.Against<DomainException>(string.IsNullOrWhiteSpace(tipoAnalise), "Tipo de Análise é obrigatória");

            Ferramenta = ferramenta;
            TipoAnalise = tipoAnalise;
            IdPipeline = idPipeline;
            Tag = tag;
            ScanId = scanId;
            Status = status;
            UrlAlvo = urlAlvo;
            Resumo = ResumoSeveridadeValueObject.Vazio;
        }

        internal void AtualizarResultado(StatusEnum status, IEnumerable<DadosArtefatoValueObject> artefatos)
        {
            SubstituirArtefatos(artefatos);
            Status = status;
        }

        internal void SubstituirArtefatos(IEnumerable<DadosArtefatoValueObject> artefatos)
        {
            Guard.AgainstNull(artefatos, nameof(artefatos));

            _artefatos.Clear();

            foreach (var artefato in artefatos)
                _artefatos.Add(new ArtefatoEntity(artefato, Ferramenta));

            Resumo = ResumoSeveridadeValueObject.DeSeveridades(_artefatos.Select(a => a.Severidade));
        }

        internal void AtualizarStatus(StatusEnum status)
        {
            Status = status;
        }

        internal void AtribuirScan(string scanId)
        {
            Guard.Against<DomainException>(string.IsNullOrWhiteSpace(scanId), "Identificador do scan é obrigatório");
            Guard.Against<DomainException>(Status != StatusEnum.EXECUTANDO, $"Análise '{Id}' não está em execução");

            ScanId = scanId;
        }
    }
}
