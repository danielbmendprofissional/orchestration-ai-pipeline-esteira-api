using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Fatias.Consultas
{
    // P1.7. `Estado`: aguardando-a-correcao | mr-aberta | pronto-para-entrega. `Repositorio` da MR: api | frontend.
    public sealed record HotfixMrDto(string Repositorio, long Iid, string Titulo, string Estado, string Branch, string? Link);

    public sealed record HotfixDto(long Issue, string Motivo, string Estado, string? LinkIssue, HotfixMrDto? Mr);

    public sealed record HotfixesDto(Guid AplicacaoId, IReadOnlyList<HotfixDto> Itens, AvisoDeHotfixDto? Aviso);

    public sealed record AvisoDeHotfixDto(string Codigo, string Mensagem);

    public sealed record ObterHotfixesQuery(Guid Aplicacao) : IRequest<Result<HotfixesDto>>;

    // Lê direto do GitLab (sem o instantâneo do Painel): a lista de hotfix é pequena e a E3c precisa do estado fresco
    // para decidir se mostra "Abrir Entrega". Nunca lança: falha do GitLab vira Result com erro.
    public sealed class ObterHotfixesHandler(IPainelAplicacaoQuery aplicacoes, IGitLab gitlab, IUsuarioAtual usuario, LinksDoGitLab links)
        : IRequestHandler<ObterHotfixesQuery, Result<HotfixesDto>>
    {
        private static readonly Erro Indisponivel = new("gitlab.indisponivel", "Não foi possível ler o GitLab agora. Tente novamente.", TipoErro.Failure);

        public async Task<Result<HotfixesDto>> Handle(ObterHotfixesQuery request, CancellationToken cancellationToken)
        {
            var visiveis = await aplicacoes.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken);
            var app = visiveis.FirstOrDefault(a => a.Id == request.Aplicacao);
            if (app is null)
                return Result<HotfixesDto>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            if (app.WorkspaceId is not { } workspace)
                return Result<HotfixesDto>.Sucesso(new HotfixesDto(
                    app.Id, [], new AvisoDeHotfixDto(PainelErros.SemWorkspace.Code, PainelErros.SemWorkspace.Message)));

            try
            {
                var entrada = app.Entrada;
                var repositorios = entrada.Projetos.ToList();
                var issuesTarefa = gitlab.ListarIssuesAsync(app.GrupoCaminho, workspace, cancellationToken);
                var mrsTarefas = repositorios.Select(p => gitlab.ListarMergeRequestsAsync(app.GrupoCaminho, p.Value, cancellationToken)).ToList();
                await Task.WhenAll(new Task[] { issuesTarefa }.Concat(mrsTarefas));

                var issues = await issuesTarefa;
                if (!issues.IsSuccess)
                    return Result<HotfixesDto>.Falha(issues.Errors[0]);

                var mrs = new List<(RepositorioFatia, MergeRequestGitLabDto)>();
                for (var i = 0; i < repositorios.Count; i++)
                {
                    var r = await mrsTarefas[i];
                    if (!r.IsSuccess)
                        return Result<HotfixesDto>.Falha(r.Errors[0]);
                    mrs.AddRange(r.Value.Select(m => (repositorios[i].Key, m)));
                }

                var itens = DerivadorDeHotfix.Derivar(issues.Value, mrs).Select(h => Montar(app, h)).ToList();
                return Result<HotfixesDto>.Sucesso(new HotfixesDto(app.Id, itens, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<HotfixesDto>.Falha(Indisponivel);
            }
        }

        private HotfixDto Montar(AplicacaoNoPainel app, HotfixLido h)
        {
            var mr = h.Mr is null
                ? null
                : new HotfixMrDto(
                    PainelErros.Texto(h.Mr.Repositorio), h.Mr.Iid, PainelErros.Cortar(h.Mr.Titulo), h.Mr.Estado,
                    PainelErros.Cortar(h.Mr.Branch), links.Mr(app.CaminhoDe(h.Mr.Repositorio), h.Mr.Iid));

            return new HotfixDto(h.Iid, PainelErros.Cortar(h.Motivo), Texto(h.Estado), links.Issue(app.WorkspaceCaminho, h.Iid), mr);
        }

        private static string Texto(EstadoDoHotfix estado) => estado switch
        {
            EstadoDoHotfix.MrAberta => "mr-aberta",
            EstadoDoHotfix.ProntoParaEntrega => "pronto-para-entrega",
            _ => "aguardando-a-correcao",
        };
    }
}
