using Esteira.Data.Deploys.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Read
{
    public sealed class AnaliseQueryConfiguration : IEntityTypeConfiguration<AnaliseQueryModel>
    {
        public void Configure(EntityTypeBuilder<AnaliseQueryModel> builder)
        {
            builder.ToTable("ANALISE");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_ANALISE");
            builder.Property(a => a.IdDeploy).HasColumnName("ID_DEPLOY");
            builder.Property(a => a.IdPipeline).HasColumnName("COD_PIPELINE");
            builder.Property(a => a.Tag).HasColumnName("DSC_TAG").IsRequired(false);
            builder.Property(a => a.Ferramenta).HasColumnName("DSC_FERRAMENTA");
            builder.Property(a => a.TipoAnalise).HasColumnName("DSC_TIPO_ANALISE");
            builder.Property(a => a.ScanId).HasColumnName("COD_SCAN").IsRequired(false);
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.VulnerabilidadeCritica).HasColumnName("NUM_VULNERABILIDADE_CRITICA");
            builder.Property(a => a.VulnerabilidadeAlta).HasColumnName("NUM_VULNERABILIDADE_ALTA");
            builder.Property(a => a.VulnerabilidadeMedia).HasColumnName("NUM_VULNERABILIDADE_MEDIA");
            builder.Property(a => a.VulnerabilidadeBaixa).HasColumnName("NUM_VULNERABILIDADE_BAIXA");
        }
    }
}
