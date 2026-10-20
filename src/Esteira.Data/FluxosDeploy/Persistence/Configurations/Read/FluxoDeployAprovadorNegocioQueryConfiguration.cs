using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployAprovadorNegocioQueryConfiguration : IEntityTypeConfiguration<FluxoDeployAprovadorNegocioQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployAprovadorNegocioQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_APROV_NEG");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_APROVADOR_NEGOCIO");
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdTarefa).HasColumnName("COD_TAREFA");
            builder.Property(a => a.Url).HasColumnName("DSC_URL_TAREFA_APROVACAO").IsRequired(false);
        }
    }
}
