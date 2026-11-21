using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // Critério 14 da A1: quem, quando, antes/depois, na mesma transação do cadastro.
    [Trait("Fatia", "A1")]
    public class AuditoriaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => agora;
        }

        private async Task<List<RegistroAuditoriaEntity>> RegistrosAsync(Guid publicId)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == publicId).ToListAsync(Ct);
        }

        private static IAuditoria Auditoria(EsteiraContext ctx, DateTimeOffset agora, string sub = "sub-auditor")
            => new AuditoriaEf(ctx, new UsuarioFalso(sub), new RelogioFixo(agora));

        [Fact, DisplayName("Deve gravar o registro de auditoria junto com a alteração no mesmo SaveChanges")]
        public async Task Auditoria_GravaNoMesmoCommit()
        {
            var agora = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var antes = new { nome = "App", ativa = true };
                ctx.Add(app);
                Auditoria(ctx, agora).Registrar("Aplicacao", app.PublicId, AcaoAuditoria.Alterou, antes, new { nome = "App 2", ativa = false });
                await ctx.SaveChangesAsync(Ct);
            }

            var registros = await RegistrosAsync(app.PublicId);
            var r = registros.Should().ContainSingle().Subject;
            r.Usuario.Should().Be("sub-auditor");
            r.Quando.Should().Be(agora.UtcDateTime);
            r.Entidade.Should().Be("Aplicacao");
            r.Acao.Should().Be(AcaoAuditoria.Alterou);
            System.Text.Json.JsonDocument.Parse(r.Antes!).RootElement.GetProperty("nome").GetString().Should().Be("App");
            var depois = System.Text.Json.JsonDocument.Parse(r.Depois!).RootElement;
            (depois.GetProperty("nome").GetString(), depois.GetProperty("ativa").GetBoolean()).Should().Be(("App 2", false));
        }

        [Fact, DisplayName("Deve aceitar antes e depois ausentes (criação e remoção)")]
        public async Task Auditoria_AntesEDepoisOpcionais()
        {
            var id = Guid.CreateVersion7();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var auditoria = Auditoria(ctx, DateTimeOffset.UtcNow);
                auditoria.Registrar("Projeto", id, AcaoAuditoria.Criou, null, new { x = 1 });
                auditoria.Registrar("Projeto", id, AcaoAuditoria.Removeu, new { x = 1 }, null);
                await ctx.SaveChangesAsync(Ct);
            }

            var registros = await RegistrosAsync(id);
            registros.Should().HaveCount(2);
            registros.Single(x => x.Acao == AcaoAuditoria.Criou).Antes.Should().BeNull();
            registros.Single(x => x.Acao == AcaoAuditoria.Removeu).Depois.Should().BeNull();
        }

        [Fact, DisplayName("Deve desfazer a auditoria junto com a alteração num rollback")]
        public async Task Auditoria_RollbackDesfazTudo()
        {
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var uow = new UnitOfWork(ctx);
                await uow.BeginTransactionAsync();
                ctx.Add(app);
                Auditoria(ctx, DateTimeOffset.UtcNow).Registrar("Aplicacao", app.PublicId, AcaoAuditoria.Criou, null, new { app.Nome });
                await uow.SaveChangesAsync();
                await uow.RollbackAsync();
            }

            (await RegistrosAsync(app.PublicId)).Should().BeEmpty();
            await using var leitura = ContextoDoCadastro.Novo(infra);
            (await leitura.Set<AplicacaoEntity>().AnyAsync(x => x.PublicId == app.PublicId, Ct)).Should().BeFalse();
        }

        [Fact, DisplayName("Deve confirmar a auditoria junto com a alteração num commit")]
        public async Task Auditoria_CommitGravaTudo()
        {
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var uow = new UnitOfWork(ctx);
                await uow.BeginTransactionAsync();
                ctx.Add(app);
                Auditoria(ctx, DateTimeOffset.UtcNow).Registrar("Aplicacao", app.PublicId, AcaoAuditoria.Criou, null, new { app.Nome });
                await uow.CommitAsync();
            }

            (await RegistrosAsync(app.PublicId)).Should().ContainSingle();
            await using var leitura = ContextoDoCadastro.Novo(infra);
            (await leitura.Set<AplicacaoEntity>().AnyAsync(x => x.PublicId == app.PublicId, Ct)).Should().BeTrue();
        }

        [Fact, DisplayName("Não deve gravar nada antes do SaveChanges (Registrar só enfileira no contexto)")]
        public async Task Auditoria_NaoGravaAntesDoSave()
        {
            var id = Guid.CreateVersion7();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                Auditoria(ctx, DateTimeOffset.UtcNow).Registrar("Projeto", id, AcaoAuditoria.Criou, null, null);
            }

            (await RegistrosAsync(id)).Should().BeEmpty();
        }
    }
}
