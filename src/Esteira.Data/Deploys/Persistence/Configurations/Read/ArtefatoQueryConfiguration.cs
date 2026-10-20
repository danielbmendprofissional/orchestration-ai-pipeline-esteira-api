using Esteira.Data.Common.Persistence.Converters;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Read
{
    public sealed class ArtefatoQueryConfiguration : IEntityTypeConfiguration<ArtefatoQueryModel>
    {
        public void Configure(EntityTypeBuilder<ArtefatoQueryModel> builder)
        {
            builder.ToTable("ARTEFATO");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_ARTEFATO");
            builder.Property(a => a.IdAnalise).HasColumnName("ID_ANALISE");
            builder.Property(a => a.Severidade).HasColumnName("IND_SEVERIDADE");
            builder.Property(a => a.LocalArquivo).HasColumnName("DSC_LOCAL_ARQUIVO").IsRequired(false);
            builder.Property(a => a.TituloVulnerabilidade).HasColumnName("DSC_TITULO_VULNERABILIDADE").IsRequired(false);
            builder.Property(a => a.DescricaoVulnerabilidade).HasColumnName("CLB_VULNERABILIDADE").IsRequired(false);
            builder.Property(a => a.PacoteCorrecao).HasColumnName("CLB_PACOTE_CORRECAO").IsRequired(false);
            builder.Property(a => a.Linha).HasColumnName("DSC_LINHA_VULNERABILIDADE").IsRequired(false);
            builder.Property(a => a.Referencias).ComoListaTexto("CLB_REFERENCIAS");
            builder.Property(a => a.CatalogoCWE).ComoListaTexto("IND_CWE_VULNERABILIDADE");
            builder.Property(a => a.CatalogoOWASP).ComoListaTexto("IND_OWASP_VULNERABILIDADE");
            builder.Property(a => a.CatalogoCVE).ComoListaTexto("IND_CVE_VULNERABILIDADE");

            builder.HasOne(a => a.Analise)
                .WithMany()
                .HasForeignKey(a => a.IdAnalise);
        }
    }
}
