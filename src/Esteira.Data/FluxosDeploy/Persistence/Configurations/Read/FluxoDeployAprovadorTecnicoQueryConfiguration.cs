using Esteira.Data.Common.Persistence.Converters;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployAprovadorTecnicoQueryConfiguration : IEntityTypeConfiguration<FluxoDeployAprovadorTecnicoQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployAprovadorTecnicoQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_APROV_TEC");
            builder.HasKey(a => new { a.IdFluxoDeploy, a.IdAprovadorTecnico });
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdAprovadorTecnico).HasColumnName("ID_APROVADOR_TECNICO");
            builder.Property(a => a.Execucao).HasColumnName("DAT_EXECUCAO");
            builder.Property(a => a.Aprovado).HasColumnName("IND_APROVADO").HasConversion(new SimNaoConverter());
            builder.Property(a => a.Justificativa).HasColumnName("DSC_JUSTIFICATIVA").IsRequired(false);

            builder.HasOne(a => a.AprovadorTecnico)
                .WithMany()
                .HasForeignKey(a => a.IdAprovadorTecnico);
        }
    }
}
