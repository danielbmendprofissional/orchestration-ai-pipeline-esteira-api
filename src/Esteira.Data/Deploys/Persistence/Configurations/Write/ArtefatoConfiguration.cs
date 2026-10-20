using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Deploys.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Deploys.Persistence.Configurations.Write
{
    public sealed class ArtefatoConfiguration : IEntityTypeConfiguration<ArtefatoEntity>
    {
        public void Configure(EntityTypeBuilder<ArtefatoEntity> builder)
        {
            builder.ToTable("ARTEFATO");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_ARTEFATO");
            builder.Property(a => a.IdAnalise).HasColumnName("ID_ANALISE");
            builder.Property(a => a.Severidade).HasColumnName("IND_SEVERIDADE").HasMaxLength(10);
            builder.Property(a => a.LocalArquivo).HasColumnName("DSC_LOCAL_ARQUIVO").HasMaxLength(400).IsRequired(false);
            builder.Property(a => a.TituloVulnerabilidade).HasColumnName("DSC_TITULO_VULNERABILIDADE").HasMaxLength(200).IsRequired(false);
            builder.Property(a => a.DescricaoVulnerabilidade).HasColumnName("CLB_VULNERABILIDADE").IsRequired(false);
            builder.Property(a => a.PacoteCorrecao).HasColumnName("CLB_PACOTE_CORRECAO").HasMaxLength(800).IsRequired(false);
            builder.Property(a => a.Linha).HasColumnName("DSC_LINHA_VULNERABILIDADE").IsRequired(false);
            builder.Property(a => a.Referencias).ComoListaTexto("CLB_REFERENCIAS");
            builder.Property(a => a.CatalogoCWE).ComoListaTexto("IND_CWE_VULNERABILIDADE", 200);
            builder.Property(a => a.CatalogoOWASP).ComoListaTexto("IND_OWASP_VULNERABILIDADE", 200);
            builder.Property(a => a.CatalogoCVE).ComoListaTexto("IND_CVE_VULNERABILIDADE", 200);

            builder.Ignore(a => a.Ferramenta);
        }
    }
}
