using Esteira.Data.Deploys.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Read
{
    public sealed class DeployQueryConfiguration : IEntityTypeConfiguration<DeployQueryModel>
    {
        public void Configure(EntityTypeBuilder<DeployQueryModel> builder)
        {
            builder.ToTable("DEPLOY");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_DEPLOY");
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdProjetoGit).HasColumnName("COD_PROJETO_GIT");
            builder.Property(a => a.NomeProjetoGit).HasColumnName("DSC_PROJETO_GIT");
            builder.Property(a => a.Linguagem).HasColumnName("DSC_LINGUAGEM");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Branch).HasColumnName("DSC_BRANCH");
            builder.Property(a => a.VersaoAplicacao).HasColumnName("DSC_VERSAO_APLICACAO");

            builder.HasMany(a => a.Analises)
                .WithOne()
                .HasForeignKey(a => a.IdDeploy);
        }
    }
}
