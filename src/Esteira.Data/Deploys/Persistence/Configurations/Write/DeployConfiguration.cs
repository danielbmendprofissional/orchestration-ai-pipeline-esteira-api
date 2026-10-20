using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Write
{
    public sealed class DeployConfiguration : IEntityTypeConfiguration<DeployEntity>
    {
        public void Configure(EntityTypeBuilder<DeployEntity> builder)
        {
            builder.ToTable("DEPLOY");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_DEPLOY");
            builder.Property(a => a.IdProjetoGit).HasColumnName("COD_PROJETO_GIT");
            builder.Property(a => a.NomeProjetoGit).HasColumnName("DSC_PROJETO_GIT").HasMaxLength(50);
            builder.Property(a => a.Linguagem).HasColumnName("DSC_LINGUAGEM").HasMaxLength(30);
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasMaxLength(15).HasConversion<string>();
            builder.Property(a => a.Branch).HasColumnName("DSC_BRANCH").HasMaxLength(20);
            builder.Property(a => a.VersaoAplicacao).HasColumnName("DSC_VERSAO_APLICACAO").HasMaxLength(30);
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");

            builder.Ignore(a => a.DomainEvents);

            builder.HasOne<FluxoDeployEntity>()
                .WithMany()
                .HasForeignKey(a => a.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Analises)
                .WithOne()
                .HasForeignKey(a => a.IdDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(a => a.Analises).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
