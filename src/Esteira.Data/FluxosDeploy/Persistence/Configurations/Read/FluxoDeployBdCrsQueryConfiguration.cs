using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployBdCrsQueryConfiguration : IEntityTypeConfiguration<FluxoDeployBdCrsQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployBdCrsQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_BD_CRS");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_FLUXO_DEPLOY_BD_CRS");
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Crs).HasColumnName("DSC_CRS");
            builder.Property(a => a.Url).HasColumnName("DSC_URL_CRS").IsRequired(false);
        }
    }
}
