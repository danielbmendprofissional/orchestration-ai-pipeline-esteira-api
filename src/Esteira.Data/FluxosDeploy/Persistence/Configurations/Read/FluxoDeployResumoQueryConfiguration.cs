using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployResumoQueryConfiguration : IEntityTypeConfiguration<FluxoDeployResumoQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployResumoQueryModel> builder)
        {
            builder.ToView("VW_FLUXO_DEPLOY_RESUMO");
            builder.HasKey(a => a.IdFluxoDeploy);

            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Inicio).HasColumnName("DAT_INICIO");
            builder.Property(a => a.Fim).HasColumnName("DAT_FIM");
            builder.Property(a => a.UsuarioCriacao).HasColumnName("DSC_USUARIO_CRIACAO");
            builder.Property(a => a.Sistema).HasColumnName("SGL_SISTEMA");
            builder.Property(a => a.Versao).HasColumnName("DSC_VERSAO");
            builder.Property(a => a.Linguagens).HasColumnName("DSC_LINGUAGENS");
            builder.Property(a => a.ProjetosGit).HasColumnName("DSC_PROJETOS_GIT");
            builder.Property(a => a.VulnerabilidadeCritica).HasColumnName("NUM_VULN_CRITICA");
            builder.Property(a => a.VulnerabilidadeAlta).HasColumnName("NUM_VULN_ALTA");
            builder.Property(a => a.VulnerabilidadeMedia).HasColumnName("NUM_VULN_MEDIA");
            builder.Property(a => a.VulnerabilidadeBaixa).HasColumnName("NUM_VULN_BAIXA");
            builder.Property(a => a.TarefasNegocio).HasColumnName("DSC_TAREFAS_NEGOCIO");
            builder.Property(a => a.AprovacaoTecnica).HasColumnName("DSC_APROVACAO_TECNICA");
            builder.Property(a => a.Crs).HasColumnName("DSC_CRS");
            builder.Property(a => a.Etapas).HasColumnName("DSC_ETAPAS");
            builder.Property(a => a.Gmuds).HasColumnName("DSC_GMUDS");
            builder.Property(a => a.VersoesTarefas).HasColumnName("DSC_VERSOES_TAREFAS");
        }
    }
}
