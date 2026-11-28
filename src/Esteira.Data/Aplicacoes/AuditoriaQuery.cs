using Esteira.Application.Auditoria;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    // X1.3: leitura da trilha no EsteiraReadContext. Todo caminho parte de `Visiveis` (IEscopoDeAuditoria).
    public sealed class AuditoriaQuery(EsteiraReadContext contexto, IEscopoDeAuditoria escopo, TimeProvider relogio) : IAuditoriaQuery
    {
        // QueryModel: linha plana lida do banco; o DTO final sai em memória (verbo padrão, resumo, kebab).
        public sealed class RegistroAuditoriaLinha
        {
            public Guid Id { get; init; }
            public DateTime Quando { get; init; }
            public string Usuario { get; init; } = default!;
            public string? UsuarioNome { get; init; }
            public OrigemAuditoria Origem { get; init; }
            public CategoriaAuditoria Categoria { get; init; }
            public AcaoAuditoria Acao { get; init; }
            public string? Verbo { get; init; }
            public string Entidade { get; init; } = default!;
            public Guid EntidadePublicId { get; init; }
            public string? ObjetoCodigo { get; init; }
            public string? ObjetoNome { get; init; }
            public Guid? AplicacaoPublicId { get; init; }
            public string? AplicacaoNome { get; init; }
            public bool Autoaprovacao { get; init; }
            public string? Antes { get; init; }
            public string? Depois { get; init; }
            public string? Motivo { get; init; }
        }

        private static readonly OrdenacaoPadrao<RegistroAuditoriaLinha> Padrao =
            OrdenacaoPadrao<RegistroAuditoriaLinha>.Por(l => l.Quando, descendente: true).Depois(l => l.Id, descendente: true);

        private IQueryable<RegistroAuditoriaEntity> Visiveis()
            => escopo.Restringir(contexto.Set<RegistroAuditoriaEntity>(), contexto.Set<PapelAplicacaoEntity>());

        public async Task<Result<PaginaDto<RegistroAuditoriaListaDto>>> ListarAsync(
            PaginacaoDto paginacao, FiltroDeAuditoria filtro, CancellationToken cancellationToken = default)
        {
            var aplicacoes = contexto.Set<AplicacaoEntity>();

            var pagina = await Filtrar(Visiveis(), filtro).PaginarAsync(
                paginacao,
                r => new RegistroAuditoriaLinha
                {
                    Id = r.PublicId,
                    Quando = r.Quando,
                    Usuario = r.Usuario,
                    UsuarioNome = r.UsuarioNome,
                    Origem = r.Origem,
                    Categoria = r.Categoria,
                    Acao = r.Acao,
                    Verbo = r.Verbo,
                    Entidade = r.Entidade,
                    EntidadePublicId = r.EntidadePublicId,
                    ObjetoCodigo = r.ObjetoCodigo,
                    ObjetoNome = r.ObjetoNome,
                    AplicacaoPublicId = aplicacoes.Where(a => a.Id == r.AplicacaoId).Select(a => (Guid?)a.PublicId).FirstOrDefault(),
                    AplicacaoNome = aplicacoes.Where(a => a.Id == r.AplicacaoId).Select(a => a.Nome).FirstOrDefault(),
                    Autoaprovacao = r.Autoaprovacao,
                    Antes = r.Antes,
                    Depois = r.Depois
                },
                Padrao,
                cancellationToken);

            if (!pagina.IsSuccess)
                return Result<PaginaDto<RegistroAuditoriaListaDto>>.Falha(pagina.Errors.ToArray());

            var p = pagina.Value;
            return Result<PaginaDto<RegistroAuditoriaListaDto>>.Sucesso(new PaginaDto<RegistroAuditoriaListaDto>
            {
                Itens = [.. p.Itens.Select(Montar)],
                Pagina = p.Pagina,
                TamanhoPagina = p.TamanhoPagina,
                Total = p.Total
            });
        }

        public async Task<RegistroAuditoriaDetalheDto?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var aplicacoes = contexto.Set<AplicacaoEntity>();

            var l = await Visiveis().Where(r => r.PublicId == id).Select(r => new RegistroAuditoriaLinha
            {
                Id = r.PublicId,
                Quando = r.Quando,
                Usuario = r.Usuario,
                UsuarioNome = r.UsuarioNome,
                Origem = r.Origem,
                Categoria = r.Categoria,
                Acao = r.Acao,
                Verbo = r.Verbo,
                Entidade = r.Entidade,
                EntidadePublicId = r.EntidadePublicId,
                ObjetoCodigo = r.ObjetoCodigo,
                ObjetoNome = r.ObjetoNome,
                AplicacaoPublicId = aplicacoes.Where(a => a.Id == r.AplicacaoId).Select(a => (Guid?)a.PublicId).FirstOrDefault(),
                AplicacaoNome = aplicacoes.Where(a => a.Id == r.AplicacaoId).Select(a => a.Nome).FirstOrDefault(),
                Autoaprovacao = r.Autoaprovacao,
                Antes = r.Antes,
                Depois = r.Depois,
                Motivo = r.Motivo
            }).FirstOrDefaultAsync(cancellationToken);

            if (l is null)
                return null;

            var lista = Montar(l);
            return new RegistroAuditoriaDetalheDto
            {
                Id = lista.Id,
                Quando = lista.Quando,
                Quem = lista.Quem,
                Categoria = lista.Categoria,
                Verbo = lista.Verbo,
                Objeto = lista.Objeto,
                Aplicacao = lista.Aplicacao,
                Autoaprovacao = lista.Autoaprovacao,
                Mudancas = MudancasDeAuditoria.Montar(l.Acao, l.Antes, l.Depois),
                Motivo = l.Motivo
            };
        }

        public async Task<IReadOnlyList<CategoriaContagemDto>> ContarPorCategoriaAsync(FiltroDeAuditoria filtro, CancellationToken cancellationToken = default)
        {
            var contagens = await Filtrar(Visiveis(), filtro.SemCategoria())
                .GroupBy(r => r.Categoria)
                .Select(g => new { Categoria = g.Key, Total = g.Count() })
                .ToListAsync(cancellationToken);

            return [.. Enum.GetValues<CategoriaAuditoria>().Select(c => new CategoriaContagemDto(
                CategoriasDeAuditoria.Texto(c), contagens.FirstOrDefault(x => x.Categoria == c)?.Total ?? 0))];
        }

        public async Task<FiltrosDeAuditoriaDto> FiltrosAsync(CancellationToken cancellationToken = default)
        {
            var visiveis = Visiveis();

            var idsDeAplicacao = visiveis.Where(r => r.AplicacaoId != null).Select(r => r.AplicacaoId!.Value).Distinct();
            var aplicacoes = await contexto.Set<AplicacaoEntity>()
                .Where(a => idsDeAplicacao.Contains(a.Id))
                .OrderBy(a => a.Nome).ThenBy(a => a.Id)
                .Select(a => new AplicacaoDoRegistroDto(a.PublicId, a.Nome))
                .ToListAsync(cancellationToken);

            var subs = await visiveis.Where(r => r.Origem == OrigemAuditoria.Tela).Select(r => r.Usuario).Distinct().ToListAsync(cancellationToken);
            var nomesCadastrados = await contexto.Set<PessoaEntity>().Where(p => subs.Contains(p.Sub)).ToDictionaryAsync(p => p.Sub, p => p.Nome, cancellationToken);
            var nomesGravados = await visiveis.Where(r => r.Origem == OrigemAuditoria.Tela && r.UsuarioNome != null)
                .OrderBy(r => r.Quando)
                .Select(r => new { r.Usuario, Nome = r.UsuarioNome! })
                .ToListAsync(cancellationToken);
            var ultimoNome = nomesGravados.GroupBy(x => x.Usuario).ToDictionary(g => g.Key, g => g.Last().Nome);

            var pessoas = subs
                .Select(s => new PessoaDoFiltroDto(s, NomeNaoVazio(nomesCadastrados.GetValueOrDefault(s)) ?? ultimoNome.GetValueOrDefault(s) ?? s))
                .OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal)
                .ToList();

            if (await visiveis.AnyAsync(r => r.Origem == OrigemAuditoria.Bot, cancellationToken))
                pessoas.Add(new PessoaDoFiltroDto(FiltroDeAuditoria.QuemBot, "bot da Esteira"));
            if (await visiveis.AnyAsync(r => r.Origem == OrigemAuditoria.Pipeline, cancellationToken))
                pessoas.Add(new PessoaDoFiltroDto(FiltroDeAuditoria.QuemPipeline, "pipeline"));

            return new FiltrosDeAuditoriaDto { Aplicacoes = aplicacoes, Pessoas = pessoas };
        }

        private static string? NomeNaoVazio(string? nome) => string.IsNullOrWhiteSpace(nome) ? null : nome;

        private IQueryable<RegistroAuditoriaEntity> Filtrar(IQueryable<RegistroAuditoriaEntity> consulta, FiltroDeAuditoria f)
        {
            var aplicacoes = contexto.Set<AplicacaoEntity>();

            if (f.AplicacaoPublicId is { } publicId)
                consulta = consulta.Where(r => aplicacoes.Any(a => a.PublicId == publicId && a.Id == r.AplicacaoId));

            if (f.Quem is { } quem)
                consulta = quem switch
                {
                    FiltroDeAuditoria.QuemBot => consulta.Where(r => r.Origem == OrigemAuditoria.Bot),
                    FiltroDeAuditoria.QuemPipeline => consulta.Where(r => r.Origem == OrigemAuditoria.Pipeline),
                    _ => consulta.Where(r => r.Origem == OrigemAuditoria.Tela && r.Usuario == quem)
                };

            if (f.Categoria is { } categoria)
                consulta = consulta.Where(r => r.Categoria == categoria);

            if (f.PeriodoDias is { } dias)
            {
                var desde = relogio.GetUtcNow().UtcDateTime.AddDays(-dias);
                consulta = consulta.Where(r => r.Quando >= desde);
            }

            if (f.Objeto is { } objeto)
                consulta = consulta.Where(r => r.ObjetoCodigo == objeto);

            if (f.Busca is { } busca)
            {
                var padrao = $"%{Escapar(busca)}%";
                // Campos alterados = nomes das chaves do JSON (só nome, nunca valor: a busca não vaza segredo).
                // ILIKE do Postgres já usa a barra invertida como escape.
                // Nome com cara de segredo fica fora da busca (critério 9).
                var segredos = CamposSecretos.PadraoRegex;
                var comCampo = contexto.Database.SqlQuery<long>($$"""
                    SELECT r."Id" AS "Value" FROM esteira."RegistroAuditoria" r
                    WHERE EXISTS (
                        SELECT 1 FROM jsonb_object_keys(CASE WHEN jsonb_typeof(r."Antes") = 'object' THEN r."Antes" ELSE '{}'::jsonb END) k WHERE k ILIKE {{padrao}} AND k !~* {{segredos}})
                    OR EXISTS (
                        SELECT 1 FROM jsonb_object_keys(CASE WHEN jsonb_typeof(r."Depois") = 'object' THEN r."Depois" ELSE '{}'::jsonb END) k WHERE k ILIKE {{padrao}} AND k !~* {{segredos}})
                    """);

                consulta = consulta.Where(r =>
                    EF.Functions.ILike(r.UsuarioNome ?? r.Usuario, padrao, "\\")
                    || EF.Functions.ILike(r.Verbo ?? "", padrao, "\\")
                    || EF.Functions.ILike(r.Entidade, padrao, "\\")
                    || EF.Functions.ILike(r.ObjetoCodigo ?? "", padrao, "\\")
                    || EF.Functions.ILike(r.ObjetoNome ?? "", padrao, "\\")
                    || EF.Functions.ILike(r.Motivo ?? "", padrao, "\\")
                    || comCampo.Contains(r.Id));
            }

            return consulta;
        }

        private static RegistroAuditoriaListaDto Montar(RegistroAuditoriaLinha l) => new()
        {
            Id = l.Id,
            Quando = DateTime.SpecifyKind(l.Quando, DateTimeKind.Utc),
            Quem = new QuemDto(l.UsuarioNome ?? l.Usuario, CategoriasDeAuditoria.Texto(l.Origem)),
            Categoria = CategoriasDeAuditoria.Texto(l.Categoria),
            Verbo = VerboPadrao.Montar(l.Verbo, l.Acao, l.Entidade),
            Objeto = new ObjetoDto(l.ObjetoCodigo ?? l.Entidade, l.ObjetoNome, l.Entidade, l.EntidadePublicId),
            Aplicacao = l.AplicacaoPublicId is { } id ? new AplicacaoDoRegistroDto(id, l.AplicacaoNome ?? "") : null,
            Autoaprovacao = l.Autoaprovacao,
            ResumoMudancas = ResumoDeMudancas.Montar(l.Acao, l.Antes, l.Depois)
        };

        private static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
