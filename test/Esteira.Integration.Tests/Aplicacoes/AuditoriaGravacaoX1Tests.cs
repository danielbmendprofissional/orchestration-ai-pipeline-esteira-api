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
    // X1.2, critérios 1 e 10: a sobrecarga com DadosDoRegistro, o atalho antigo e as três origens, contra o PostgreSQL real.
    [Trait("Fatia", "X1")]
    public class AuditoriaGravacaoX1Tests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => agora;
        }

        private async Task<RegistroAuditoriaEntity> UmAsync(Guid publicId)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().SingleAsync(x => x.EntidadePublicId == publicId, Ct);
        }

        private async Task<AplicacaoEntity> AplicacaoAsync()
        {
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            await using var ctx = ContextoDoCadastro.Novo(infra);
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            return app;
        }

        private async Task GravarAsync(string sub, Action<IAuditoria> registrar)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            registrar(new AuditoriaEf(ctx, new UsuarioFalso(sub), new RelogioFixo(DateTimeOffset.UtcNow)));
            await ctx.SaveChangesAsync(Ct);
        }

        [Fact, DisplayName("10: origem tela grava o nome de quem agiu (IUsuarioAtual.Nome) e todos os campos de DadosDoRegistro")]
        public async Task Tela_GravaNomeEDados()
        {
            var app = await AplicacaoAsync();
            var id = Guid.CreateVersion7();

            await GravarAsync("sub-ana", a => a.Registrar("AcaoBot", id, AcaoAuditoria.Criou, null, new { x = 1 },
                new DadosDoRegistro(app.Id, CategoriaAuditoria.Aprovacao, OrigemAuditoria.Tela, null, "aprovou a Fatia V4", "V4", "Fatia V4", "porque sim", true)));

            var r = await UmAsync(id);
            (r.Origem, r.UsuarioNome, r.Usuario).Should().Be((OrigemAuditoria.Tela, "Usuário", "sub-ana"));
            (r.AplicacaoId, r.Categoria, r.Verbo, r.ObjetoCodigo, r.ObjetoNome, r.Motivo, r.Autoaprovacao)
                .Should().Be((app.Id, CategoriaAuditoria.Aprovacao, "aprovou a Fatia V4", "V4", "Fatia V4", "porque sim", true));
        }

        [Fact, DisplayName("10: origem bot grava 'bot da Esteira' mesmo sem usuário na sessão")]
        public async Task Bot_GravaNomeDoBot()
        {
            var id = Guid.CreateVersion7();

            await GravarAsync(null!, a => a.Registrar("AcaoBot", id, AcaoAuditoria.Alterou, null, new { x = 1 },
                new DadosDoRegistro(null, CategoriaAuditoria.Fatias, OrigemAuditoria.Bot)));

            var r = await UmAsync(id);
            (r.Origem, r.UsuarioNome).Should().Be((OrigemAuditoria.Bot, "bot da Esteira"));
            r.Usuario.Should().NotBeNullOrWhiteSpace();
        }

        [Fact, DisplayName("10: origem pipeline grava o nome informado ('pipeline #n') e o ator do webhook")]
        public async Task Pipeline_GravaNomeDaPipeline()
        {
            var id = Guid.CreateVersion7();

            await GravarAsync("webhook-gitlab", a => a.Registrar("AcaoBot", id, AcaoAuditoria.Alterou, null, new { x = 1 },
                new DadosDoRegistro(null, CategoriaAuditoria.Fatias, OrigemAuditoria.Pipeline, "pipeline #42")));

            var r = await UmAsync(id);
            (r.Origem, r.UsuarioNome, r.Usuario).Should().Be((OrigemAuditoria.Pipeline, "pipeline #42", "webhook-gitlab"));
        }

        [Theory, DisplayName("1: o atalho antigo grava origem tela e a categoria deduzida da entidade")]
        [InlineData("Aplicacao", CategoriaAuditoria.Cadastro)]
        [InlineData("Projeto", CategoriaAuditoria.Cadastro)]
        [InlineData("AmbienteProjeto", CategoriaAuditoria.Cadastro)]
        [InlineData("Destino", CategoriaAuditoria.Cadastro)]
        [InlineData("PoliticaSeveridade", CategoriaAuditoria.Politica)]
        [InlineData("PapelAplicacao", CategoriaAuditoria.Papeis)]
        [InlineData("AcaoBot", CategoriaAuditoria.Fatias)]
        [InlineData("Entrega", CategoriaAuditoria.Entrega)]
        [InlineData("EntidadeQueNaoExiste", CategoriaAuditoria.Cadastro)]
        public async Task Atalho_DeduzCategoria(string entidade, CategoriaAuditoria esperada)
        {
            var id = Guid.CreateVersion7();

            await GravarAsync("sub-ana", a => a.Registrar(entidade, id, AcaoAuditoria.Criou, null, new { x = 1 }));

            var r = await UmAsync(id);
            (r.Origem, r.Categoria, r.UsuarioNome, r.AplicacaoId).Should().Be((OrigemAuditoria.Tela, esperada, "Usuário", (long?)null));
        }
    }
}
