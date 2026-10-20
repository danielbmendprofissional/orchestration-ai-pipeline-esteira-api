using Esteira.Domain.Deploys.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Write
{
    public sealed class AnaliseConfiguration : IEntityTypeConfiguration<AnaliseEntity>
    {
        public void Configure(EntityTypeBuilder<AnaliseEntity> builder)
        {
            builder.ToTable("ANALISE");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_ANALISE");
            builder.Property(a => a.IdDeploy).HasColumnName("ID_DEPLOY");
            builder.Property(a => a.IdPipeline).HasColumnName("COD_PIPELINE");
            builder.Property(a => a.Tag).HasColumnName("DSC_TAG");
            builder.Property(a => a.Ferramenta).HasColumnName("DSC_FERRAMENTA").HasMaxLength(30);
            builder.Property(a => a.TipoAnalise).HasColumnName("DSC_TIPO_ANALISE").HasMaxLength(10);
            builder.Property(a => a.ScanId).HasColumnName("COD_SCAN").HasMaxLength(50).IsRequired(false);
            builder.Property(a => a.UrlAlvo).HasColumnName("DSC_URL_ALVO").HasMaxLength(500).IsRequired(false);
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasMaxLength(15).HasConversion<string>();

            builder.HasIndex(a => a.UrlAlvo)
                .IsUnique()
                .HasFilter("\"DSC_TIPO_ANALISE\" = 'DAST' AND \"IND_STATUS\" = 'EXECUTANDO'")
                .HasDatabaseName("UK_ANALISE_URL_EXECUTANDO");

            builder.OwnsOne(a => a.Resumo, resumo =>
            {
                resumo.Property(r => r.VulnerabilidadeCritica).HasColumnName("NUM_VULNERABILIDADE_CRITICA");
                resumo.Property(r => r.VulnerabilidadeAlta).HasColumnName("NUM_VULNERABILIDADE_ALTA");
                resumo.Property(r => r.VulnerabilidadeMedia).HasColumnName("NUM_VULNERABILIDADE_MEDIA");
                resumo.Property(r => r.VulnerabilidadeBaixa).HasColumnName("NUM_VULNERABILIDADE_BAIXA");
            });

            builder.Navigation(a => a.Resumo).IsRequired();

            builder.HasMany(a => a.Artefatos)
                .WithOne()
                .HasForeignKey(a => a.IdAnalise)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(a => a.Artefatos).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
