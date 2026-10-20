using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Deploys.ValueObjects;
using Esteira.Domain.FluxosDeploy.Events;
namespace Esteira.Domain.Deploys.Entities
{
    public sealed class DeployEntity : AggregateRoot
    {
        public const int TamanhoMaximoBranch = 20;

        private DeployEntity() { }

        public int IdProjetoGit { get; private set; }
        public string NomeProjetoGit { get; private set; }
        public StatusEnum Status { get; private set; }
        public string Branch { get; private set; }
        public string Linguagem { get; private set; }
        public string VersaoAplicacao { get; private set; }

        public int IdFluxoDeploy { get; private set; }

        private readonly List<AnaliseEntity> _analises = new();
        public IReadOnlyCollection<AnaliseEntity> Analises => _analises;

        internal DeployEntity(int idFluxoDeploy, int idProjetoGit, string nomeProjetoGit, StatusEnum status, string branch, string linguagem, string tag)
        {
            Guard.Against<DomainException>(idFluxoDeploy <= 0, "Fluxo de deploy inválido");
            Guard.AgainstNullOrWhiteSpace(branch, nameof(branch));
            Guard.Against<DomainException>(
                branch.Length > TamanhoMaximoBranch,
                $"A branch deve ter no máximo {TamanhoMaximoBranch} caracteres.");

            IdFluxoDeploy = idFluxoDeploy;
            IdProjetoGit = idProjetoGit;
            NomeProjetoGit = nomeProjetoGit;
            Branch = branch;
            Linguagem = linguagem;
            Status = status;
            VersaoAplicacao = VersaoAplicacaoValueObject.DaTag(tag).ToString();
        }

        public static DeployEntity Criar(int idFluxoDeploy, int idProjetoGit, string nomeProjetoGit, StatusEnum status, string branch, string linguagem, string tag)
            => new DeployEntity(idFluxoDeploy, idProjetoGit, nomeProjetoGit, status, branch, linguagem, tag);

        public void AtualizarStatus(StatusEnum status)
        {
            Status = status;
        }

        public void Concluir()
        {
            Guard.Against<DomainException>(
                Status != StatusEnum.EXECUTANDO,
                $"Deploy {Id} não está em execução");

            Status = StatusEnum.CONCLUIDO;
        }

        public void Cancelar()
        {
            Status = StatusEnum.CANCELADO;

            foreach (var analise in _analises)
                analise.AtualizarStatus(StatusEnum.CANCELADO);
        }

        public AnaliseEntity AdicionarAnalise(
            string ferramenta,
            string tipoAnalise,
            int idPipeline,
            string tag,
            string scanId,
            StatusEnum status,
            IEnumerable<DadosArtefatoValueObject> artefatos = null,
            string urlAlvo = null)
        {
            var analise = new AnaliseEntity(
                ferramenta,
                tipoAnalise,
                idPipeline,
                tag,
                scanId,
                status,
                urlAlvo);

            analise.SubstituirArtefatos(artefatos ?? Enumerable.Empty<DadosArtefatoValueObject>());

            foreach (var anterior in _analises.Where(a =>
                         string.Equals(a.Ferramenta, ferramenta, StringComparison.OrdinalIgnoreCase) &&
                         a.Status != StatusEnum.CANCELADO))
            {
                anterior.AtualizarStatus(StatusEnum.CANCELADO);
            }

            _analises.Add(analise);
            return analise;
        }

        public AnaliseEntity IniciarAnaliseDinamica(string ferramenta, int idPipeline, string tag, string urlAlvo)
        {
            Guard.Against<DomainException>(string.IsNullOrWhiteSpace(urlAlvo), "URL da análise é obrigatória");

            return AdicionarAnalise(ferramenta, AnaliseEntity.TipoDinamica, idPipeline, tag, null, StatusEnum.EXECUTANDO, urlAlvo: urlAlvo);
        }

        public void AtribuirScanAnalise(int idAnalise, string scanId)
            => ObterAnalise(idAnalise).AtribuirScan(scanId);

        public void FalharAnalise(int idAnalise)
            => ObterAnalise(idAnalise).AtualizarStatus(StatusEnum.ERRO);

        private AnaliseEntity ObterAnalise(int idAnalise)
        {
            var analise = _analises.FirstOrDefault(a => a.Id == idAnalise);

            Guard.Against<NotFoundException>(
                analise is null,
                $"Análise '{idAnalise}' não encontrada no deploy '{Id}'");

            return analise;
        }

        public void RegistrarResultadoAnalise(
            int idAnalise,
            StatusEnum status,
            IEnumerable<DadosArtefatoValueObject> artefatos)
        {
            ObterAnalise(idAnalise).AtualizarResultado(status, artefatos);
        }

        public void ValidarVersaoAplicacao(string tag)
        {
            Guard.Against<DomainException>(
                Status == StatusEnum.EXECUTANDO && !VersaoAplicacaoValueObject.Criar(VersaoAplicacao).AceitaTag(tag),
                $"Existe um deploy em execução da versão: {VersaoAplicacao}. Só serão aceitas tags nessa iniciadas nessa versão.");
        }
    }
}
