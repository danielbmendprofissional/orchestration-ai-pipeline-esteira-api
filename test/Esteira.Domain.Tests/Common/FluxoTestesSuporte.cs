using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.ValueObjects;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
namespace Esteira.Domain.Tests.Common
{
    internal static class FluxoTestesSuporte
    {
        public static readonly IReadOnlyList<EtapaEntity> Catalogo = new List<EtapaEntity>
        {
            EtapaEntity.Criar("ABER", "Abertura", 1, true),
            EtapaEntity.Criar("DEV", "Desenvolvimento", 2, true),
            EtapaEntity.Criar("QA", "Qualidade", 3, false),
            EtapaEntity.Criar("HML", "Homologação", 4, true),
            EtapaEntity.Criar("AAN", "Área de Negócio", 5, false),
            EtapaEntity.Criar("PRD", "Produção", 6, true),
            EtapaEntity.Criar("BD", "Banco de Dados", 7, false),
            EtapaEntity.Criar("AET", "Equipe Técnica", 8, false),
            EtapaEntity.Criar("GMUD", "Gmud", 9, false),
            EtapaEntity.Criar("VERSAO", "Versão das tarefas", 10, false)
        };

        public static FluxoDeployEntity CriarFluxo(int id, params string[] etapas)
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null);
            fluxo.Id = id;

            for (var i = 0; i < etapas.Length; i++)
                fluxo.AdicionarEtapa(etapas[i], i + 1);

            return fluxo;
        }

        public static DeployEntity CriarDeploy(
            int id = 1,
            int idFluxoDeploy = 1,
            int idProjetoGit = 10,
            string tag = "v1.0.0",
            StatusEnum status = StatusEnum.EXECUTANDO,
            string branch = "BRANCH")
        {
            var deploy = DeployEntity.Criar(idFluxoDeploy, idProjetoGit, "projeto", status, branch, "java", tag);
            deploy.Id = id;
            return deploy;
        }

        public static IReadOnlyList<DadosArtefatoValueObject> Artefatos(params string[] severidades)
            => severidades.Select(s => new DadosArtefatoValueObject(Severidade: s)).ToList();

        public static AnaliseEntity CriarAnalise(string ferramenta = "TRIVY", StatusEnum status = StatusEnum.CONCLUIDO, params string[] severidades)
            => CriarDeploy().AdicionarAnalise(ferramenta, "SCA", 1, "v1.0.0", null, status, Artefatos(severidades));
    }
}
