using Esteira.Application.Settings;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.ValueObjects;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.ValueObjects;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Entities;

using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Common
{
    internal static class Construtor
    {
        public static readonly IOptions<GerenciadorTarefasSettings> ConfiguracaoTarefas = Options.Create(new GerenciadorTarefasSettings
        {
            UrlTarefa = "https://tarefas.exemplo/issues/{0}",
            UrlNovaGmud = "https://tarefas.exemplo/gmud/nova"
        });

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

        public static T ComId<T>(this T entidade, int id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(entidade, id);
            return entidade;
        }

        public static FluxoDeployEntity Fluxo(int id = 1, params string[] etapas)
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null).ComId(id);

            for (var i = 0; i < etapas.Length; i++)
                fluxo.AdicionarEtapa(etapas[i], i + 1);

            return fluxo;
        }

        public static DeployEntity Deploy(
            int id = 1,
            int idFluxoDeploy = 1,
            string branch = "DEV",
            int idProjetoGit = 10,
            string tag = "v1.0.0",
            StatusEnum status = StatusEnum.EXECUTANDO)
            => DeployEntity.Criar(idFluxoDeploy, idProjetoGit, "projeto", status, branch, "java", tag).ComId(id);

        public static DeployEntity ComAnalise(this DeployEntity deploy, string ferramenta = "TRIVY", StatusEnum status = StatusEnum.CONCLUIDO, params string[] severidades)
        {
            deploy.AdicionarAnalise(ferramenta, "SCA", 1, "v1.0.0", null, status,
                severidades.Select(s => new DadosArtefatoValueObject(s)).ToList());
            return deploy;
        }

        public static AprovadorTecnicoEntity Aprovador(int id, string equipe = "INFRA", StatusEnum status = StatusEnum.ATIVO)
            => AprovadorTecnicoEntity.Criar($"usuario{id}", equipe, status).ComId(id);

        public static ConfiguracaoSistemaEntity Configuracao(int limite = 10)
            => ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(limite, limite, limite, limite));
    }
}
