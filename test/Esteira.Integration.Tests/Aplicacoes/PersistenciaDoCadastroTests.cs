using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // Dados da A1: unicidades, cascata e mapeamento (jsonb, enums em texto).
    [Trait("Fatia", "A1")]
    public class PersistenciaDoCadastroTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<AplicacaoEntity> NovaAplicacaoAsync(string? sigla = null, long? grupo = null)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", sigla ?? ContextoDoCadastro.SiglaUnica(), grupo ?? ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            return app;
        }

        private async Task<ProjetoEntity> NovoProjetoAsync(long aplicacaoId, long? gitlabId = null)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var projeto = ProjetoEntity.Criar(aplicacaoId, gitlabId ?? ContextoDoCadastro.IdUnico(), "grupo/p", "p", Stack.Dotnet);
            ctx.Add(projeto);
            await ctx.SaveChangesAsync(Ct);
            return projeto;
        }

        private async Task ExigirViolacaoDeUnicidadeAsync(params object[] entidades)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            ctx.AddRange(entidades);

            var act = () => ctx.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        private async Task<int> ContarAsync<T>(Func<IQueryable<T>, IQueryable<T>> filtro) where T : class
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await filtro(ctx.Set<T>().AsNoTracking()).CountAsync(Ct);
        }

        [Fact, DisplayName("Deve recusar Aplicação com sigla repetida")]
        public async Task Aplicacao_SiglaUnica()
        {
            var a = await NovaAplicacaoAsync();

            await ExigirViolacaoDeUnicidadeAsync(AplicacaoEntity.Criar("Outra", a.Sigla, ContextoDoCadastro.IdUnico(), "g2").Value);
        }

        [Fact, DisplayName("Deve recusar Aplicação com grupo do GitLab já vinculado")]
        public async Task Aplicacao_GrupoUnico()
        {
            var a = await NovaAplicacaoAsync();

            await ExigirViolacaoDeUnicidadeAsync(AplicacaoEntity.Criar("Outra", ContextoDoCadastro.SiglaUnica(), a.GrupoGitLabId, "g2").Value);
        }

        [Fact, DisplayName("Deve recusar Projeto com ProjetoGitLabId repetido, mesmo em outra Aplicação")]
        public async Task Projeto_GitLabIdUnico()
        {
            var a1 = await NovaAplicacaoAsync();
            var a2 = await NovaAplicacaoAsync();
            var p = await NovoProjetoAsync(a1.Id);

            await ExigirViolacaoDeUnicidadeAsync(ProjetoEntity.Criar(a2.Id, p.ProjetoGitLabId, "g/x", "x", Stack.Outro));
        }

        [Fact, DisplayName("Deve recusar Destino com mesmo nome no mesmo Projeto e Ambiente, e aceitar em outro Ambiente")]
        public async Task Destino_NomeUnicoPorProjetoEAmbiente()
        {
            var a = await NovaAplicacaoAsync();
            var p = await NovoProjetoAsync(a.Id);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(DestinoEntity.Criar(p.Id, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string>(), 0).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var ok = ContextoDoCadastro.Novo(infra))
            {
                ok.Add(DestinoEntity.Criar(p.Id, AmbienteDeploy.Qa, "k8s", new Dictionary<string, string>(), 0).Value);
                await ok.SaveChangesAsync(Ct);
            }

            await ExigirViolacaoDeUnicidadeAsync(DestinoEntity.Criar(p.Id, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string>(), 1).Value);
        }

        [Fact, DisplayName("Deve recusar segunda Política para a mesma Aplicação e Ambiente")]
        public async Task Politica_UmaPorAplicacaoEAmbiente()
        {
            var a = await NovaAplicacaoAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(PoliticaSeveridadeEntity.Criar(a.Id, AmbientePolitica.Qa, 0, 0, null, null, ModoPolitica.Bloqueia).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await ExigirViolacaoDeUnicidadeAsync(
                PoliticaSeveridadeEntity.Criar(a.Id, AmbientePolitica.Qa, null, null, null, null, ModoPolitica.Avisa).Value);
        }

        [Fact, DisplayName("Deve recusar o mesmo Papel da mesma pessoa na mesma Aplicação, mas aceitar outro Papel")]
        public async Task Papel_UnicoPorAplicacaoUsuarioEPapel()
        {
            var a = await NovaAplicacaoAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(PapelAplicacaoEntity.Criar(a.Id, "sub-1", PapelAplicacao.Analista));
                ctx.Add(PapelAplicacaoEntity.Criar(a.Id, "sub-1", PapelAplicacao.Membro));
                await ctx.SaveChangesAsync(Ct);
            }

            await ExigirViolacaoDeUnicidadeAsync(PapelAplicacaoEntity.Criar(a.Id, "sub-1", PapelAplicacao.Analista));
        }

        [Fact, DisplayName("Deve recusar o mesmo Ambiente duas vezes no Projeto")]
        public async Task Ambiente_UnicoPorProjeto()
        {
            var a = await NovaAplicacaoAsync();
            var p = await NovoProjetoAsync(a.Id);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(AmbienteProjetoEntity.Criar(p.Id, AmbienteDeploy.Qa).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await ExigirViolacaoDeUnicidadeAsync(AmbienteProjetoEntity.Criar(p.Id, AmbienteDeploy.Qa).Value);
        }

        [Fact, DisplayName("Deve recusar Pessoa com Sub repetido")]
        public async Task Pessoa_SubUnico()
        {
            var sub = $"sub-{Guid.NewGuid():N}";
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(PessoaEntity.Criar(sub, "Ana", "ana@x.com", DateTime.UtcNow));
                await ctx.SaveChangesAsync(Ct);
            }

            await ExigirViolacaoDeUnicidadeAsync(PessoaEntity.Criar(sub, "Ana 2", "ana2@x.com", DateTime.UtcNow));
        }

        [Fact, DisplayName("Deve ter índice único em PublicId nas nove tabelas do cadastro")]
        public async Task PublicId_UnicoEmTodas()
        {
            string[] tabelas = ["Aplicacao", "Projeto", "DependenciaProjeto", "AmbienteProjeto", "Destino",
                "PoliticaSeveridade", "PapelAplicacao", "Pessoa", "RegistroAuditoria"];

            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand(
                "select tablename from pg_indexes where schemaname = 'esteira' and indexdef ilike 'CREATE UNIQUE INDEX%(\"PublicId\")%'", conexao);
            var comIndice = new List<string>();
            await using var leitor = await cmd.ExecuteReaderAsync(Ct);
            while (await leitor.ReadAsync(Ct))
                comIndice.Add(leitor.GetString(0));

            comIndice.Should().Contain(tabelas);
        }

        [Fact, DisplayName("Deve apagar em cascata Ambientes, Destinos e Dependências ao remover o Projeto")]
        public async Task Cascata_ProjetoApagaFilhos()
        {
            var a = await NovaAplicacaoAsync();
            var p1 = await NovoProjetoAsync(a.Id);
            var p2 = await NovoProjetoAsync(a.Id);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(AmbienteProjetoEntity.Criar(p1.Id, AmbienteDeploy.Qa).Value);
                ctx.Add(DestinoEntity.Criar(p1.Id, AmbienteDeploy.Qa, "d", new Dictionary<string, string>(), 0).Value);
                ctx.Add(DependenciaProjetoEntity.Criar(p1.Id, p2.Id).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Remove(await ctx.Set<ProjetoEntity>().SingleAsync(x => x.Id == p1.Id, Ct));
                await ctx.SaveChangesAsync(Ct);
            }

            (await ContarAsync<AmbienteProjetoEntity>(q => q.Where(x => x.ProjetoId == p1.Id))).Should().Be(0);
            (await ContarAsync<DestinoEntity>(q => q.Where(x => x.ProjetoId == p1.Id))).Should().Be(0);
            (await ContarAsync<DependenciaProjetoEntity>(q => q.Where(x => x.ProjetoId == p1.Id))).Should().Be(0);
            (await ContarAsync<ProjetoEntity>(q => q.Where(x => x.Id == p2.Id))).Should().Be(1);
        }

        [Fact, DisplayName("Deve apagar em cascata Projetos, Políticas, Papéis e Dependências apontando para o Projeto ao remover a Aplicação")]
        public async Task Cascata_AplicacaoApagaTudo()
        {
            var a = await NovaAplicacaoAsync();
            var p1 = await NovoProjetoAsync(a.Id);
            var p2 = await NovoProjetoAsync(a.Id);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(AmbienteProjetoEntity.Criar(p1.Id, AmbienteDeploy.Hml).Value);
                ctx.Add(DestinoEntity.Criar(p1.Id, AmbienteDeploy.Hml, "d", new Dictionary<string, string>(), 0).Value);
                ctx.Add(DependenciaProjetoEntity.Criar(p1.Id, p2.Id).Value);
                ctx.Add(PoliticaSeveridadeEntity.Criar(a.Id, AmbientePolitica.Review, null, null, null, null, ModoPolitica.Avisa).Value);
                ctx.Add(PapelAplicacaoEntity.Criar(a.Id, "sub-x", PapelAplicacao.Publicador));
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Remove(await ctx.Set<AplicacaoEntity>().SingleAsync(x => x.Id == a.Id, Ct));
                await ctx.SaveChangesAsync(Ct);
            }

            (await ContarAsync<ProjetoEntity>(q => q.Where(x => x.AplicacaoId == a.Id))).Should().Be(0);
            (await ContarAsync<AmbienteProjetoEntity>(q => q.Where(x => x.ProjetoId == p1.Id))).Should().Be(0);
            (await ContarAsync<DestinoEntity>(q => q.Where(x => x.ProjetoId == p1.Id))).Should().Be(0);
            (await ContarAsync<DependenciaProjetoEntity>(q => q.Where(x => x.ProjetoId == p1.Id || x.DependeDeProjetoId == p2.Id))).Should().Be(0);
            (await ContarAsync<PoliticaSeveridadeEntity>(q => q.Where(x => x.AplicacaoId == a.Id))).Should().Be(0);
            (await ContarAsync<PapelAplicacaoEntity>(q => q.Where(x => x.AplicacaoId == a.Id))).Should().Be(0);
        }

        [Fact, DisplayName("Deve guardar a configuração do Destino em jsonb e devolver igual")]
        public async Task Destino_ConfiguracaoEmJsonb()
        {
            var a = await NovaAplicacaoAsync();
            var p = await NovoProjetoAsync(a.Id);
            var config = new Dictionary<string, string> { ["KUBE_CONTEXT"] = "hml", ["NAMESPACE"] = "valuto" };
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(DestinoEntity.Criar(p.Id, AmbienteDeploy.Hml, "k8s", config, 0).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var leitura = ContextoDoCadastro.Novo(infra);
            (await leitura.Set<DestinoEntity>().SingleAsync(x => x.ProjetoId == p.Id, Ct)).Configuracao.Should().BeEquivalentTo(config);

            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand(
                "select data_type from information_schema.columns where table_schema = 'esteira' and table_name = 'Destino' and column_name = 'Configuracao'", conexao);
            (await cmd.ExecuteScalarAsync(Ct)).Should().Be("jsonb");
        }

        [Fact, DisplayName("Deve guardar enums como texto em kebab-case e aceitar limites null")]
        public async Task Enums_EmTextoKebab_ELimitesNulos()
        {
            var a = await NovaAplicacaoAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(PapelAplicacaoEntity.Criar(a.Id, "sub-k", PapelAplicacao.AprovadorTecnico));
                ctx.Add(PoliticaSeveridadeEntity.Criar(a.Id, AmbientePolitica.Producao, 0, 0, null, null, ModoPolitica.Bloqueia).Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(Ct);
            await using var papel = new NpgsqlCommand("select \"Papel\" from esteira.\"PapelAplicacao\" where \"AplicacaoId\" = @id", conexao);
            papel.Parameters.AddWithValue("id", a.Id);
            (await papel.ExecuteScalarAsync(Ct)).Should().Be("aprovador-tecnico");

            await using var pol = new NpgsqlCommand(
                "select \"Ambiente\", \"Modo\", \"Media\" is null and \"Baixa\" is null from esteira.\"PoliticaSeveridade\" where \"AplicacaoId\" = @id", conexao);
            pol.Parameters.AddWithValue("id", a.Id);
            await using var r = await pol.ExecuteReaderAsync(Ct);
            (await r.ReadAsync(Ct)).Should().BeTrue();
            (r.GetString(0), r.GetString(1), r.GetBoolean(2)).Should().Be(("producao", "bloqueia", true));
        }

        [Fact, DisplayName("Deve ler Aplicação pelo contexto de leitura (sem tracking)")]
        public async Task ContextoDeLeitura_LeAplicacao()
        {
            var a = await NovaAplicacaoAsync();

            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var lida = await leitura.Set<AplicacaoEntity>().SingleAsync(x => x.PublicId == a.PublicId, Ct);

            lida.Sigla.Should().Be(a.Sigla);
        }
    }
}
