using Esteira.Data.Entregas;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Entregas;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Pipeline
{
    // Projeto com caminho único, Ambientes habilitados, Destinos opcionais e uma Entrega aberta (B2 com flag, B3 sem), tudo gravado.
    internal sealed class CenarioDePipeline
    {
        public const string Sha = "0123456789abcdef0123456789abcdef01234567";

        public AplicacaoEntity Aplicacao = default!;
        public PessoaEntity Pessoa = default!;
        public ProjetoEntity Projeto = default!;
        public EntregaEntity Entrega = default!;
        public FluxoEntity Fluxo => Entrega.Fluxos[0];
        public string Caminho => Projeto.Caminho;

        public static async Task<CenarioDePipeline> CriarAsync(
            InfraestruturaFixture infra, bool hmlHabilitado = true, params (AmbienteDeploy Ambiente, string Nome, int Ordem, Dictionary<string, string> Config)[] destinos)
        {
            var ct = TestContext.Current.CancellationToken;
            var c = new CenarioDePipeline();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            c.Aplicacao = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            c.Pessoa = PessoaEntity.Criar(Guid.NewGuid().ToString(), "Fulano", "f@x.com", CenarioDeEntrega.T0);
            ctx.AddRange(c.Aplicacao, c.Pessoa);
            await ctx.SaveChangesAsync(ct);

            c.Projeto = ProjetoEntity.Criar(c.Aplicacao.Id, ContextoDoCadastro.IdUnico(), $"grupo/proj-{Guid.NewGuid():N}", "proj", Stack.Dotnet);
            ctx.Add(c.Projeto);
            await ctx.SaveChangesAsync(ct);

            ctx.Add(AmbienteProjetoEntity.Criar(c.Projeto.Id, AmbienteDeploy.Qa).Value);
            ctx.Add(AmbienteProjetoEntity.Criar(c.Projeto.Id, AmbienteDeploy.Hml, hmlHabilitado).Value);
            ctx.Add(AmbienteProjetoEntity.Criar(c.Projeto.Id, AmbienteDeploy.Producao).Value);
            foreach (var d in destinos)
                ctx.Add(DestinoEntity.Criar(c.Projeto.Id, d.Ambiente, d.Nome, d.Config, d.Ordem).Value);
            await ctx.SaveChangesAsync(ct);

            await c.NovaEntregaAsync(ctx);
            return c;
        }

        // Outra Entrega aberta para o mesmo Projeto (outro Fluxo), para a corrida de ocupação.
        public async Task<FluxoEntity> OutroFluxoAsync(InfraestruturaFixture infra)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var anterior = Entrega;
            await NovaEntregaAsync(ctx);
            var novo = Fluxo;
            Entrega = anterior;
            return novo;
        }

        private async Task NovaEntregaAsync(Esteira.Data.Common.Persistence.EsteiraContext ctx)
        {
            var ct = TestContext.Current.CancellationToken;
            Entrega = EntregaEntity.Abrir(Aplicacao, Pessoa,
                [new FluxoDaEntrega(Projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B2", true), new FatiaDaEntrega("B3", false)])],
                CenarioDeEntrega.T0).Value;
            await new EntregaRepositorio(ctx).AdicionarAsync(Entrega, ct);
            await ctx.SaveChangesAsync(ct);
        }
    }
}
