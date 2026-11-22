using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Fatias.Consultas
{
    // P1.6. `Aba`: andamento (padrão) | prontas | todas. `Projeto`: api | frontend. `Aplicacoes`: PublicIds separados por vírgula.
    public sealed record ListarFatiasQuery(PaginacaoDto Paginacao, string? Aba, string? Fatia, string? Projeto, string? Aplicacoes)
        : IRequest<Result<PainelListaDto>>;

    public sealed record AtualizarFatiasCommand(Guid Aplicacao) : IRequest<Result>;

    public sealed record ObterGavetaQuery(Guid Aplicacao, string Codigo) : IRequest<Result<GavetaDto>>;

    public static class PainelErros
    {
        public static readonly Erro AbaInvalida = new(
            "painel.aba-invalida", "A aba deve ser 'andamento', 'prontas' ou 'todas'.", TipoErro.Validation);

        public static readonly Erro ProjetoInvalido = new(
            "painel.projeto-invalido", "O projeto deve ser 'api' ou 'frontend'.", TipoErro.Validation);

        public static readonly Erro FatiaNaoEncontrada = new(
            "painel.fatia-nao-encontrada", "Fatia não encontrada.", TipoErro.NotFound);

        public static readonly Erro SemWorkspace = new(
            "painel.sem-workspace", "Cadastre o workspace desta Aplicação para ver as Fatias.", TipoErro.NotFound);

        public static string Texto(EstadoDaFatia estado) => estado switch
        {
            EstadoDaFatia.Planejada => "planejada",
            EstadoDaFatia.ComSpec => "com-spec",
            EstadoDaFatia.EmExecucao => "em-execucao",
            EstadoDaFatia.EmRevisao => "em-revisao",
            _ => "pronta-para-entrega",
        };

        public static string Texto(RepositorioFatia repositorio) => repositorio == RepositorioFatia.Api ? "api" : "frontend";

        // Texto do GitLab é dado: só se corta o tamanho (a Api devolve JSON, nunca HTML).
        public static string Cortar(string? texto, int maximo = 200)
            => texto is null ? string.Empty : texto.Length > maximo ? texto[..maximo] : texto;
    }

    public sealed class ListarFatiasHandler(IPainelAplicacaoQuery aplicacoes, IPainelDeFatias painel, IUsuarioAtual usuario)
        : IRequestHandler<ListarFatiasQuery, Result<PainelListaDto>>
    {
        private static readonly string[] Ordenaveis = ["aplicacao", "codigo", "titulo", "estado", "risco"];

        public async Task<Result<PainelListaDto>> Handle(ListarFatiasQuery request, CancellationToken cancellationToken)
        {
            var erros = new List<Erro>();

            var aba = string.IsNullOrWhiteSpace(request.Aba) ? "andamento" : request.Aba.Trim().ToLowerInvariant();
            if (aba is not ("andamento" or "prontas" or "todas"))
                erros.Add(PainelErros.AbaInvalida);

            RepositorioFatia? projeto = null;
            if (!string.IsNullOrWhiteSpace(request.Projeto))
            {
                projeto = request.Projeto.Trim().ToLowerInvariant() switch
                {
                    "api" => RepositorioFatia.Api,
                    "frontend" => RepositorioFatia.Frontend,
                    _ => null,
                };
                if (projeto is null)
                    erros.Add(PainelErros.ProjetoInvalido);
            }

            var ordenacao = request.Paginacao.Ordenacao?.Trim().ToLowerInvariant();
            if (ordenacao is not null && !Ordenaveis.Contains(ordenacao))
                erros.Add(PaginacaoErros.OrdenacaoInvalida(request.Paginacao.Ordenacao!));

            if (erros.Count > 0)
                return Result<PainelListaDto>.Falha(erros.ToArray());

            var visiveis = await aplicacoes.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken);
            var escolhidas = Escolher(visiveis, request.Aplicacoes).OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList();

            var resultados = escolhidas.Count == 0
                ? []
                : await painel.ObterAsync(escolhidas.Select(a => a.Entrada).ToList(), cancellationToken);
            var porChave = resultados.ToDictionary(r => r.Chave);

            var avisos = new List<AvisoDoPainelDto>();
            var apps = new List<AplicacaoDoPainelDto>();
            var linhas = new List<(FatiaDoPainelDto Item, string Nome, int Ordem, EstadoDaFatia Estado)>();

            foreach (var app in escolhidas)
            {
                porChave.TryGetValue(app.Id.ToString(), out var resultado);
                apps.Add(new AplicacaoDoPainelDto(app.Id, app.Nome, app.Sigla, resultado?.GeradoEm, resultado?.Desatualizado ?? false));

                if (resultado is null)
                    continue;

                if (resultado.Situacao == SituacaoDoPainel.SemWorkspace)
                    avisos.Add(new AvisoDoPainelDto(app.Id, app.Nome, PainelErros.SemWorkspace.Code, PainelErros.SemWorkspace.Message));
                else if (resultado.Situacao == SituacaoDoPainel.Falha && resultado.Erro is { } erro)
                    avisos.Add(new AvisoDoPainelDto(app.Id, app.Nome, erro.Code, erro.Message));

                var ordem = 0;
                foreach (var f in resultado.Fatias)
                {
                    var atual = ordem++;
                    if (!DaAba(aba, f.Estado.Estado) || !CasaFatia(f, request.Fatia) || (projeto is { } p && !f.Repositorios.Contains(p)))
                        continue;

                    linhas.Add((Linha(app, f), app.Nome, atual, f.Estado.Estado));
                }
            }

            var ordenadas = Ordenar(linhas, ordenacao, string.Equals(request.Paginacao.Sentido, "desc", StringComparison.OrdinalIgnoreCase));

            var pagina = Math.Max(1, request.Paginacao.Pagina);
            var tamanho = Math.Clamp(request.Paginacao.TamanhoPagina, 1, PaginacaoDto.TamanhoMaximo);
            var itens = ordenadas.Skip((pagina - 1) * tamanho).Take(tamanho).ToList();

            var geradoEm = apps.Where(a => a.GeradoEm is not null).Select(a => a.GeradoEm).DefaultIfEmpty(null).Min();
            return Result<PainelListaDto>.Sucesso(new PainelListaDto(
                itens, pagina, tamanho, ordenadas.Count, avisos, apps, geradoEm, apps.Any(a => a.Desatualizado)));
        }

        // Sem `?aplicacoes=`: todas as visíveis. Com ele: só as pedidas que a pessoa pode ver (as demais somem em silêncio).
        private static IEnumerable<AplicacaoNoPainel> Escolher(IReadOnlyList<AplicacaoNoPainel> visiveis, string? filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
                return visiveis;

            var pedidas = filtro.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => Guid.TryParse(t, out var g) ? g : (Guid?)null)
                .Where(g => g is not null).Select(g => g!.Value).ToHashSet();
            return visiveis.Where(a => pedidas.Contains(a.Id));
        }

        private static bool DaAba(string aba, EstadoDaFatia estado) => aba switch
        {
            "prontas" => estado == EstadoDaFatia.ProntaParaEntrega,
            "todas" => true,
            _ => estado is EstadoDaFatia.ComSpec or EstadoDaFatia.EmExecucao or EstadoDaFatia.EmRevisao,
        };

        private static bool CasaFatia(FatiaLida f, string? busca)
            => string.IsNullOrWhiteSpace(busca)
               || f.Codigo.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase)
               || f.Titulo.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase);

        private static FatiaDoPainelDto Linha(AplicacaoNoPainel app, FatiaLida f) => new(
            app.Id, app.Nome, app.Sigla, f.Codigo, PainelErros.Cortar(f.Titulo), PainelErros.Texto(f.Estado.Estado),
            f.Estado.Progresso, f.Estado.EmCorrecao, f.FormatoAnterior, f.Risco, PainelErros.Cortar(f.Status), f.StatusReconhecido,
            f.Repositorios.Select(PainelErros.Texto).ToList(), f.SpecDisponivel);

        private static List<FatiaDoPainelDto> Ordenar(
            List<(FatiaDoPainelDto Item, string Nome, int Ordem, EstadoDaFatia Estado)> linhas, string? campo, bool desc)
        {
            Comparison<(FatiaDoPainelDto Item, string Nome, int Ordem, EstadoDaFatia Estado)> principal = (campo ?? "aplicacao") switch
            {
                "codigo" => (a, b) => Natural(a.Item.Codigo, b.Item.Codigo),
                "titulo" => (a, b) => string.Compare(a.Item.Titulo, b.Item.Titulo, StringComparison.OrdinalIgnoreCase),
                "estado" => (a, b) => a.Estado.CompareTo(b.Estado),
                "risco" => (a, b) => string.Compare(a.Item.Risco, b.Item.Risco, StringComparison.OrdinalIgnoreCase),
                _ => (a, b) => string.Compare(a.Nome, b.Nome, StringComparison.OrdinalIgnoreCase),
            };

            var copia = linhas.ToList();
            // Ordenação estável: empate cai na aplicação e na ordem do mapa.
            var indexadas = copia.Select((l, i) => (l, i)).ToList();
            indexadas.Sort((x, y) =>
            {
                var c = principal(x.l, y.l);
                if (desc) c = -c;
                if (c != 0) return c;
                c = string.Compare(x.l.Nome, y.l.Nome, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                c = x.l.Ordem.CompareTo(y.l.Ordem);
                return c != 0 ? c : x.i.CompareTo(y.i);
            });
            return indexadas.Select(x => x.l.Item).ToList();
        }

        // `A2` < `A10`: números comparados como números.
        private static int Natural(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    var ini = i; var inj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    var na = a[ini..i].TrimStart('0'); var nb = b[inj..j].TrimStart('0');
                    var c = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }

    public sealed class AtualizarFatiasHandler(IPainelAplicacaoQuery aplicacoes, IPainelDeFatias painel, IUsuarioAtual usuario)
        : IRequestHandler<AtualizarFatiasCommand, Result>
    {
        public async Task<Result> Handle(AtualizarFatiasCommand request, CancellationToken cancellationToken)
        {
            var visiveis = await aplicacoes.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken);
            var app = visiveis.FirstOrDefault(a => a.Id == request.Aplicacao);
            if (app is null)
                return Result.Falha(AutorizacaoAplicacao.NaoEncontrada);

            return await painel.AtualizarAsync(app.Entrada, cancellationToken);
        }
    }

    public sealed class ObterGavetaHandler(IPainelAplicacaoQuery aplicacoes, IPainelDeFatias painel, IUsuarioAtual usuario, LinksDoGitLab links)
        : IRequestHandler<ObterGavetaQuery, Result<GavetaDto>>
    {
        public async Task<Result<GavetaDto>> Handle(ObterGavetaQuery request, CancellationToken cancellationToken)
        {
            var visiveis = await aplicacoes.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken);
            var app = visiveis.FirstOrDefault(a => a.Id == request.Aplicacao);
            if (app is null)
                return Result<GavetaDto>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var resultado = await painel.ObterAsync(app.Entrada, cancellationToken);
            if (resultado.Situacao == SituacaoDoPainel.Falha && resultado.Erro is { } erro)
                return Result<GavetaDto>.Falha(erro);

            var fatia = resultado.Fatias.FirstOrDefault(f => string.Equals(f.Codigo, request.Codigo, StringComparison.OrdinalIgnoreCase));
            return fatia is null
                ? Result<GavetaDto>.Falha(PainelErros.FatiaNaoEncontrada)
                : Result<GavetaDto>.Sucesso(Montar(app, fatia));
        }

        private GavetaDto Montar(AplicacaoNoPainel app, FatiaLida f)
        {
            var d = f.Detalhe;
            var ws = app.WorkspaceCaminho;

            var spec = new GavetaSpecDto(f.SpecDisponivel, f.CaminhoSpec, links.Spec(ws, f.CaminhoSpec), f.Risco, f.FormatoAnterior);
            var mae = f.IssueMae is { } n
                ? new GavetaIssueDto(n, d?.MaeTitulo is null ? null : PainelErros.Cortar(d.MaeTitulo), d?.MaeEstado, links.Issue(ws, n))
                : null;
            var filhas = (d?.Filhas ?? []).Select(x => new GavetaIssueDto(x.Iid, PainelErros.Cortar(x.Titulo), x.Estado, links.Issue(ws, x.Iid))).ToList();

            var mrs = d?.Mrs ?? [];
            var repositorios = f.Repositorios.Concat(mrs.Select(m => m.Repositorio)).Distinct().ToList();
            var branchPor = repositorios.ToDictionary(r => r, r => Preferida(mrs.Where(m => m.Repositorio == r))?.Branch);

            var gaveta = repositorios.Select(r =>
            {
                var caminho = app.CaminhoDe(r);
                return new GavetaRepositorioDto(
                    PainelErros.Texto(r), branchPor[r], links.Branch(caminho, branchPor[r]),
                    mrs.Where(m => m.Repositorio == r).OrderBy(m => m.Iid).Select(m => new GavetaMrDto(
                        m.Iid, PainelErros.Cortar(m.Titulo), m.Estado, PainelErros.Cortar(m.Branch), m.StatusPipeline,
                        links.Mr(caminho, m.Iid), links.Branch(caminho, m.Branch))).ToList());
            }).ToList();

            var tasks = (d?.Tasks ?? []).Select(t =>
            {
                var caminho = t.Repositorio is { } r ? app.CaminhoDe(r) : null;
                var link = links.Commit(caminho, t.Sha)
                           ?? (t.Repositorio is { } rr ? links.Branch(caminho, branchPor.GetValueOrDefault(rr)) : null);
                return new GavetaTaskDto(
                    t.Codigo, t.NumeroTask, t.IssueFilha, links.Issue(ws, t.IssueFilha), t.Feita, t.EmAndamento, t.ReviewPass,
                    t.Sha, t.Executor is null ? null : PainelErros.Cortar(t.Executor), t.Repositorio is { } rp ? PainelErros.Texto(rp) : null, link);
            }).ToList();

            return new GavetaDto(
                app.Id, f.Codigo, PainelErros.Cortar(f.Titulo), PainelErros.Texto(f.Estado.Estado), f.Estado.Progresso,
                f.Estado.EmCorrecao, f.FormatoAnterior, spec, mae, filhas, gaveta, tasks);
        }

        // Aberta > mergeada > fechada; dentro do grupo, a mais nova.
        private static MrDaGaveta? Preferida(IEnumerable<MrDaGaveta> mrs)
            => mrs.OrderBy(m => m.Estado.Equals("opened", StringComparison.OrdinalIgnoreCase) ? 0 : m.Estado.Equals("merged", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenByDescending(m => m.Iid).FirstOrDefault();
    }
}
