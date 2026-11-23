using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Domain.Fatias;

namespace Esteira.Application.Fatias.Decisoes
{
    // `Motivo` quando `Pode` é falso: papel | sem-mr-aberta | ci-nao-verde | decisao-em-andamento | review-ja-pedido.
    public sealed record PermissaoDaAcaoDto(bool Pode, string? Motivo);

    public sealed record AcoesDaFatiaDto(PermissaoDaAcaoDto PodeAbrirReview, PermissaoDaAcaoDto PodeAprovar, PermissaoDaAcaoDto PodeReprovar);

    // `Estado`: nenhum | pedido | aberto | falhou. `Link` só com o review aberto, Fatia que toca o front e `Review:UrlModelo` válido.
    public sealed record ReviewDaFatiaDto(string Estado, string? Link);

    // `Comentario` só na Ação de Reprovar (texto do humano, nunca HTML).
    public sealed record AcaoDaFatiaDto(
        Guid Id, string Tipo, string Estado, string? Repositorio, long MrIid, string? PedidaPor, DateTime PedidaEm, DateTime AtualizadaEm,
        string? CodigoErro, string? Comentario);

    // `Decisao`: nenhuma | aprovando | reprovando | falhou.
    public sealed record RevisaoDaFatia(AcoesDaFatiaDto Acoes, ReviewDaFatiaDto Review, string Decisao, IReadOnlyList<AcaoDaFatiaDto> Lista);

    // Regras de apresentação da P2 (spec, "Painel e gaveta"): só papel, estado da Fatia e Ações gravadas; nada vem do GitLab além
    // do que a P1 já leu (MRs abertas e o pipeline de cada uma).
    public static class DerivadorDeRevisao
    {
        public const string Papel = "papel";
        public const string SemMrAberta = "sem-mr-aberta";
        public const string CiNaoVerde = "ci-nao-verde";
        public const string DecisaoEmAndamento = "decisao-em-andamento";
        public const string ReviewJaPedido = "review-ja-pedido";

        private static readonly PermissaoDaAcaoDto Sim = new(true, null);

        public static RevisaoDaFatia Derivar(
            AcessoAplicacao acesso, AplicacaoNoPainel app, FatiaLida fatia, IReadOnlyList<AcaoNaFatia> acoes, string? urlModelo)
        {
            var daFatia = acoes.Where(a => string.Equals(a.FatiaCodigo, fatia.Codigo, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.PedidaEm).ToList();

            var abertas = fatia.Estado.Estado != EstadoDaFatia.EmRevisao
                ? []
                : (fatia.Detalhe?.Mrs ?? []).Where(m => string.Equals(m.Estado, "opened", StringComparison.OrdinalIgnoreCase)).ToList();

            // A mesma escolha do comando Abrir review: a MR aberta mais antiga de cada repositório.
            var alvos = abertas.GroupBy(m => m.Repositorio).OrderBy(g => g.Key).Select(g => g.OrderBy(m => m.Iid).First())
                .Select(m => (Mr: m, ProjetoId: app.Projetos.FirstOrDefault(p => p.Repositorio == m.Repositorio)?.Id ?? 0)).ToList();

            var review = DerivarReview(fatia, daFatia, alvos, urlModelo);
            var decisao = DerivarDecisao(daFatia);
            var emAndamento = daFatia.Any(a => a.Tipo is TipoAcaoBot.Aprovar or TipoAcaoBot.Reprovar && a.Estado is EstadoAcaoBot.Pedida or EstadoAcaoBot.Executando);

            PermissaoDaAcaoDto Barrar(OperacaoAplicacao operacao, Func<PermissaoDaAcaoDto?> mais)
                => !acesso.Pode(operacao) ? new(false, Papel)
                    : abertas.Count == 0 ? new(false, SemMrAberta)
                    : mais() ?? Sim;

            var podeAbrir = Barrar(OperacaoAplicacao.AbrirReview, () => alvos.Any(a => daFatia.Any(x =>
                    x.Tipo == TipoAcaoBot.AbrirReview && x.ProjetoId == a.ProjetoId && x.MrIid == a.Mr.Iid
                    && x.Estado is EstadoAcaoBot.Pedida or EstadoAcaoBot.Executando or EstadoAcaoBot.Confirmada))
                ? new(false, ReviewJaPedido) : null);
            var podeAprovar = Barrar(OperacaoAplicacao.DecidirFatia, () =>
                emAndamento ? new(false, DecisaoEmAndamento)
                : abertas.Any(m => !string.Equals(m.StatusPipeline, "success", StringComparison.OrdinalIgnoreCase)) ? new(false, CiNaoVerde)
                : null);
            var podeReprovar = Barrar(OperacaoAplicacao.DecidirFatia, () => emAndamento ? new(false, DecisaoEmAndamento) : null);

            var lista = daFatia.Select(a => new AcaoDaFatiaDto(
                a.Id, EnumKebab.Texto(a.Tipo), EnumKebab.Texto(a.Estado), a.Repositorio is { } r ? PainelErros.Texto(r) : null, a.MrIid,
                a.PedidaPor, a.PedidaEm, a.AtualizadaEm, a.CodigoErro, a.Tipo == TipoAcaoBot.Reprovar ? a.Comentario : null)).ToList();

            return new RevisaoDaFatia(new AcoesDaFatiaDto(podeAbrir, podeAprovar, podeReprovar), review, decisao, lista);
        }

        private static ReviewDaFatiaDto DerivarReview(
            FatiaLida fatia, List<AcaoNaFatia> daFatia, List<(MrDaGaveta Mr, long ProjetoId)> alvos, string? urlModelo)
        {
            // Estado de cada MR-alvo pela última Ação de abrir-review que não foi cancelada.
            var estados = alvos.Select(a => daFatia
                .Where(x => x.Tipo == TipoAcaoBot.AbrirReview && x.ProjetoId == a.ProjetoId && x.MrIid == a.Mr.Iid && x.Estado != EstadoAcaoBot.Cancelada)
                .Select(x => (EstadoAcaoBot?)x.Estado).LastOrDefault()).ToList();

            if (estados.Count == 0 || estados.All(e => e is null))
                return new ReviewDaFatiaDto("nenhum", null);

            if (estados.All(e => e == EstadoAcaoBot.Confirmada))
                return new ReviewDaFatiaDto("aberto", fatia.Repositorios.Contains(RepositorioFatia.Frontend) ? MontarLink(urlModelo, fatia.Codigo) : null);

            return new ReviewDaFatiaDto(estados.Any(e => e == EstadoAcaoBot.Falhou) ? "falhou" : "pedido", null);
        }

        private static string DerivarDecisao(List<AcaoNaFatia> daFatia)
        {
            var decisoes = daFatia.Where(a => a.Tipo is TipoAcaoBot.Aprovar or TipoAcaoBot.Reprovar).ToList();
            var viva = decisoes.LastOrDefault(a => a.Estado is EstadoAcaoBot.Pedida or EstadoAcaoBot.Executando);
            if (viva is not null)
                return viva.Tipo == TipoAcaoBot.Aprovar ? "aprovando" : "reprovando";

            return decisoes.LastOrDefault()?.Estado == EstadoAcaoBot.Falhou ? "falhou" : "nenhuma";
        }

        // `https` com `{fatia}`; o código entra em minúsculo e só se for letra, número ou hífen. Inválido = sem link (nunca erro).
        private static string? MontarLink(string? modelo, string codigo)
        {
            if (string.IsNullOrWhiteSpace(modelo) || !modelo.Contains("{fatia}", StringComparison.Ordinal)
                || codigo.Length == 0 || !codigo.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
                return null;

            var url = modelo.Trim().Replace("{fatia}", codigo.ToLowerInvariant(), StringComparison.Ordinal);
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 ? url : null;
        }
    }

    // O que o Painel carregou de Ações e papéis para as Aplicações da resposta; `Montar` devolve null com a flag desligada.
    public sealed class ContextoDeRevisao
    {
        public static readonly ContextoDeRevisao Desligado = new(null, new Dictionary<Guid, (AcessoAplicacao, IReadOnlyList<AcaoNaFatia>)>());

        private readonly string? _urlModelo;
        private readonly Dictionary<Guid, (AcessoAplicacao Acesso, IReadOnlyList<AcaoNaFatia> Acoes)> _porAplicacao;
        private readonly bool _ligado;

        internal ContextoDeRevisao(string? urlModelo, Dictionary<Guid, (AcessoAplicacao, IReadOnlyList<AcaoNaFatia>)> porAplicacao, bool ligado = false)
        {
            _urlModelo = urlModelo;
            _porAplicacao = porAplicacao.ToDictionary(p => p.Key, p => (p.Value.Item1, p.Value.Item2));
            _ligado = ligado;
        }

        public RevisaoDaFatia? Montar(AplicacaoNoPainel app, FatiaLida fatia)
            => _ligado && _porAplicacao.TryGetValue(app.Id, out var x) ? DerivadorDeRevisao.Derivar(x.Acesso, app, fatia, x.Acoes, _urlModelo) : null;

        internal static ContextoDeRevisao Ligado(string? urlModelo, Dictionary<Guid, (AcessoAplicacao, IReadOnlyList<AcaoNaFatia>)> porAplicacao)
            => new(urlModelo, porAplicacao, true);
    }

    public sealed class RevisaoDoPainel(
        IOpcoesDeRevisao opcoes, IAcoesDaFatiaQuery query, IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao)
    {
        public async Task<ContextoDeRevisao> CarregarAsync(IReadOnlyList<AplicacaoNoPainel> apps, CancellationToken cancellationToken = default)
        {
            if (!opcoes.Ligado || apps.Count == 0)
                return ContextoDeRevisao.Desligado;

            var acoes = await query.ListarAsync(apps.Select(a => a.Id).ToList(), cancellationToken);
            var porAplicacao = new Dictionary<Guid, (AcessoAplicacao, IReadOnlyList<AcaoNaFatia>)>();
            foreach (var app in apps)
            {
                var id = await acesso.ResolverIdAsync(app.Id, cancellationToken);
                if (id is null)
                    continue;

                var permissao = await autorizacao.ObterAcessoAsync(id.Value, cancellationToken);
                porAplicacao[app.Id] = (permissao, acoes.TryGetValue(app.Id, out var lista) ? lista : []);
            }

            return ContextoDeRevisao.Ligado(opcoes.UrlModelo, porAplicacao);
        }
    }
}
