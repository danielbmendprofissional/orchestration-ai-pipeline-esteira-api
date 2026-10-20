using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployVersaoTarefasQueryConfiguration : IEntityTypeConfiguration<FluxoDeployVersaoTarefasQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployVersaoTarefasQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_VERSAO_TAREFAS");
            builder.HasKey(a => a.IdFluxoDeploy);
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdProjeto).HasColumnName("COD_PROJETO");
            builder.Property(a => a.IdVersao).HasColumnName("COD_VERSAO");
        }
    }
}
