using System.Text.Json;
using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Aplicacoes.Write
{
    // Mapeamento do cadastro da A1 (spec 01-cadastro-aplicacao.md, "Dados"). Tabelas no schema esteira
    // (ADR-0016); Id long identity + PublicId GUID v7 único (ADR-0014). Aplicado também no contexto de leitura.
    internal static class MapeamentoBase
    {
        public static EntityTypeBuilder<T> ConfigurarBase<T>(this EntityTypeBuilder<T> b, string tabela) where T : EntityBase
        {
            b.ToTable(tabela);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.PublicId).ValueGeneratedNever();
            b.HasIndex(x => x.PublicId).IsUnique();
            return b;
        }
    }

    internal sealed class AplicacaoConfiguration : IEntityTypeConfiguration<AplicacaoEntity>
    {
        public void Configure(EntityTypeBuilder<AplicacaoEntity> b)
        {
            b.ConfigurarBase("Aplicacao");
            b.Property(x => x.Nome).IsRequired();
            b.Property(x => x.Sigla).IsRequired();
            b.Property(x => x.GrupoGitLabCaminho).IsRequired();
            b.HasIndex(x => x.Sigla).IsUnique();
            b.HasIndex(x => x.GrupoGitLabId).IsUnique();
        }
    }

    internal sealed class ProjetoConfiguration : IEntityTypeConfiguration<ProjetoEntity>
    {
        public void Configure(EntityTypeBuilder<ProjetoEntity> b)
        {
            b.ConfigurarBase("Projeto");
            b.Property(x => x.Caminho).IsRequired();
            b.Property(x => x.Nome).IsRequired();
            b.Property(x => x.Stack).HasConversion(new EnumKebabConverter<Stack>());
            b.HasIndex(x => x.ProjetoGitLabId).IsUnique();
            b.HasOne<AplicacaoEntity>().WithMany().HasForeignKey(x => x.AplicacaoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class DependenciaProjetoConfiguration : IEntityTypeConfiguration<DependenciaProjetoEntity>
    {
        public void Configure(EntityTypeBuilder<DependenciaProjetoEntity> b)
        {
            b.ConfigurarBase("DependenciaProjeto");
            b.HasIndex(x => new { x.ProjetoId, x.DependeDeProjetoId }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("CK_DependenciaProjeto_NaoPropria", "\"ProjetoId\" <> \"DependeDeProjetoId\""));
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.DependeDeProjetoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class AmbienteProjetoConfiguration : IEntityTypeConfiguration<AmbienteProjetoEntity>
    {
        public void Configure(EntityTypeBuilder<AmbienteProjetoEntity> b)
        {
            b.ConfigurarBase("AmbienteProjeto");
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbienteDeploy>());
            b.HasIndex(x => new { x.ProjetoId, x.Ambiente }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("CK_AmbienteProjeto_ProducaoHabilitado", "\"Ambiente\" <> 'producao' OR \"Habilitado\""));
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class DestinoConfiguration : IEntityTypeConfiguration<DestinoEntity>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<DestinoEntity> b)
        {
            b.ConfigurarBase("Destino");
            b.Property(x => x.Nome).IsRequired();
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbienteDeploy>());

            var comparer = new ValueComparer<Dictionary<string, string>>(
                (a, c) => a != null && c != null && a.Count == c.Count && !a.Except(c).Any(),
                v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
                v => new Dictionary<string, string>(v));

            b.Property(x => x.Configuracao)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, Json),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, Json) ?? new Dictionary<string, string>(),
                    comparer)
                .HasColumnType("jsonb")
                .IsRequired();

            b.HasIndex(x => new { x.ProjetoId, x.Ambiente, x.Nome }).IsUnique();
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class PoliticaSeveridadeConfiguration : IEntityTypeConfiguration<PoliticaSeveridadeEntity>
    {
        public void Configure(EntityTypeBuilder<PoliticaSeveridadeEntity> b)
        {
            b.ConfigurarBase("PoliticaSeveridade");
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbientePolitica>());
            b.Property(x => x.Modo).HasConversion(new EnumKebabConverter<ModoPolitica>());
            b.HasIndex(x => new { x.AplicacaoId, x.Ambiente }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint(
                "CK_PoliticaSeveridade_LimitesNaoNegativos",
                "(\"Critica\" IS NULL OR \"Critica\" >= 0) AND (\"Alta\" IS NULL OR \"Alta\" >= 0) AND (\"Media\" IS NULL OR \"Media\" >= 0) AND (\"Baixa\" IS NULL OR \"Baixa\" >= 0)"));
            b.HasOne<AplicacaoEntity>().WithMany().HasForeignKey(x => x.AplicacaoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class PapelAplicacaoConfiguration : IEntityTypeConfiguration<PapelAplicacaoEntity>
    {
        public void Configure(EntityTypeBuilder<PapelAplicacaoEntity> b)
        {
            b.ConfigurarBase("PapelAplicacao");
            b.Property(x => x.Usuario).IsRequired();
            b.Property(x => x.Papel).HasConversion(new EnumKebabConverter<PapelAplicacao>());
            b.HasIndex(x => new { x.AplicacaoId, x.Usuario, x.Papel }).IsUnique();
            b.HasOne<AplicacaoEntity>().WithMany().HasForeignKey(x => x.AplicacaoId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class PessoaConfiguration : IEntityTypeConfiguration<PessoaEntity>
    {
        public void Configure(EntityTypeBuilder<PessoaEntity> b)
        {
            b.ConfigurarBase("Pessoa");
            b.Property(x => x.Sub).IsRequired();
            b.Property(x => x.Nome).IsRequired();
            b.Property(x => x.Email).IsRequired();
            b.HasIndex(x => x.Sub).IsUnique();
        }
    }

    internal sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoriaEntity>
    {
        public void Configure(EntityTypeBuilder<RegistroAuditoriaEntity> b)
        {
            b.ConfigurarBase("RegistroAuditoria");
            b.Property(x => x.Usuario).IsRequired();
            b.Property(x => x.Entidade).IsRequired();
            b.Property(x => x.Acao).HasConversion(new EnumKebabConverter<AcaoAuditoria>());
            b.Property(x => x.Antes).HasColumnType("jsonb");
            b.Property(x => x.Depois).HasColumnType("jsonb");
            // Consulta da trilha (X1): por entidade e por quando.
            b.HasIndex(x => new { x.Entidade, x.EntidadePublicId });
            b.HasIndex(x => x.Quando);
        }
    }
}
