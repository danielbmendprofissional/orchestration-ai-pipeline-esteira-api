using Esteira.Domain.Common.Enums;

namespace Esteira.Data.Deploys.Persistence.QueryModels
{
    public sealed class DeployQueryModel
    {
        public int Id { get; private set; }
        public int IdFluxoDeploy { get; private set; }
        public int IdProjetoGit { get; private set; }
        public string NomeProjetoGit { get; private set; }
        public StatusEnum Status { get; private set; }
        public string Branch { get; private set; }
        public string Linguagem { get; private set; }
        public string VersaoAplicacao { get; private set; }

        public ICollection<AnaliseQueryModel> Analises { get; private set; } = new List<AnaliseQueryModel>();
    }
}
