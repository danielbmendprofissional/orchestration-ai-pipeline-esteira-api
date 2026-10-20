using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployGmudQueryConfiguration : IEntityTypeConfiguration<FluxoDeployGmudQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployGmudQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_GMUD");
            builder.HasKey(a => a.IdFluxoDeploy);
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Url).HasColumnName("DSC_URL_GMUD").IsRequired(false);
            builder.Property(a => a.Justificativa).HasColumnName("DSC_JUSTIFICATIVA").IsRequired(false);
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO").IsRequired(false);
            builder.Property(a => a.Execucao).HasColumnName("DAT_EXECUCAO");
        }
    }
}
