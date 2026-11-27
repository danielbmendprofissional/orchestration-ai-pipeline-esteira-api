using Esteira.Data.Entregas;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Entregas
{
    // Aplicação, Projetos e Pessoa gravados; Entrega aberta pelo domínio e gravada pelo repositório (E1).
    internal sealed class CenarioDeEntrega
    {
        public static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        public AplicacaoEntity Aplicacao = default!;
        public PessoaEntity Pessoa = default!;
        public List<ProjetoEntity> Projetos = [];
        public EntregaEntity Entrega = default!;

        public FluxoEntity Fluxo(int indice = 0) => Entrega.Fluxos[indice];

        public static async Task<CenarioDeEntrega> CriarAsync(InfraestruturaFixture infra, int projetos = 1, params string[] fatias)
        {
            var ct = TestContext.Current.CancellationToken;
            var c = new CenarioDeEntrega();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            c.Aplicacao = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            c.Pessoa = PessoaEntity.Criar(Guid.NewGuid().ToString(), "Fulano", "f@x.com", T0);
            ctx.AddRange(c.Aplicacao, c.Pessoa);
            await ctx.SaveChangesAsync(ct);

            for (var i = 0; i < projetos; i++)
                c.Projetos.Add(ProjetoEntity.Criar(c.Aplicacao.Id, ContextoDoCadastro.IdUnico(), $"g/p{i}", $"p{i}", Stack.Dotnet));
            ctx.AddRange(c.Projetos);
            await ctx.SaveChangesAsync(ct);

            var lista = fatias.Length == 0 ? ["B2", "B3"] : fatias;
            var fluxos = c.Projetos
                .Select(p => new FluxoDaEntrega(p, true, TipoMudanca.Minor, lista.Select((f, n) => new FatiaDaEntrega(f, n % 2 == 0)).ToList()))
                .ToList();
            c.Entrega = EntregaEntity.Abrir(c.Aplicacao, c.Pessoa, fluxos, T0).Value;
            await new EntregaRepositorio(ctx).AdicionarAsync(c.Entrega, ct);
            await ctx.SaveChangesAsync(ct);
            return c;
        }
    }
}
