using Esteira.Data.Aplicacoes.Write;
using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Analises.Write
{
    // E2 (spec 03-analises-e-gate.md, "Dados"). Valem também no contexto de leitura (mesmas tabelas).
    internal sealed class AnaliseConfiguration : IEntityTypeConfiguration<AnaliseEntity>
    {
        public void Configure(EntityTypeBuilder<AnaliseEntity> b)
        {
            b.ConfigurarBase("Analise");
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbienteDeploy>());
            b.Property(x => x.Escopo).HasConversion(new EnumKebabConverter<EscopoAnalise>());
            b.Property(x => x.Tipo).HasConversion(new EnumKebabConverter<TipoAnalise>());
            b.Property(x => x.Veredito).HasConversion(new EnumKebabConverter<VereditoAnalise>());
            b.Property(x => x.DestinoNome).HasMaxLength(AnaliseEntity.MaxDestino);
            b.Property(x => x.Fonte).HasMaxLength(64).IsRequired();
            b.Property(x => x.CustoUsd).HasPrecision(12, 4);
            // Reenvio do mesmo job substitui (nunca duplica). Destino nulo conta como igual ('') no índice.
            b.HasIndex(x => new { x.FluxoId, x.Ambiente, x.Escopo, x.Fonte, x.DestinoNome, x.PipelineGitLabId }).IsUnique().AreNullsDistinct(false);
            b.ToTable(t => t.HasCheckConstraint("CK_Analise_CustoNaoNegativo", "\"CustoUsd\" IS NULL OR \"CustoUsd\" >= 0"));
            b.HasOne<FluxoEntity>().WithMany().HasForeignKey(x => x.FluxoId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Achados).WithOne().HasForeignKey(a => a.AnaliseId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Achados).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class AchadoConfiguration : IEntityTypeConfiguration<AchadoEntity>
    {
        public void Configure(EntityTypeBuilder<AchadoEntity> b)
        {
            b.ConfigurarBase("Achado");
            b.Property(x => x.Severidade).HasConversion(new EnumKebabConverter<SeveridadeAchado>());
            b.Property(x => x.Titulo).HasMaxLength(AchadoEntity.MaxTitulo).IsRequired();
            b.Property(x => x.RegraId).HasMaxLength(AchadoEntity.MaxRegraId);
            b.Property(x => x.Arquivo).HasMaxLength(AchadoEntity.MaxArquivo);
            b.Property(x => x.Descricao).HasMaxLength(AchadoEntity.MaxDescricao);
            b.Property(x => x.ChaveDoTipo).HasMaxLength(AchadoEntity.MaxChave).IsRequired();
            b.HasIndex(x => x.AnaliseId);
            b.HasIndex(x => x.ChaveDoTipo);
        }
    }

    internal sealed class DecisaoDoGateConfiguration : IEntityTypeConfiguration<DecisaoDoGateEntity>
    {
        public void Configure(EntityTypeBuilder<DecisaoDoGateEntity> b)
        {
            b.ConfigurarBase("DecisaoDoGate");
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbienteDeploy>());
            b.Property(x => x.Momento).HasConversion(new EnumKebabConverter<MomentoDoGate>());
            b.Property(x => x.Decisao).HasConversion(new EnumKebabConverter<DecisaoGate>());
            b.Property(x => x.Motivo).HasMaxLength(100);
            b.HasIndex(x => new { x.FluxoId, x.Ambiente, x.Momento, x.PipelineGitLabId }).IsUnique();
            b.HasOne<FluxoEntity>().WithMany().HasForeignKey(x => x.FluxoId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
