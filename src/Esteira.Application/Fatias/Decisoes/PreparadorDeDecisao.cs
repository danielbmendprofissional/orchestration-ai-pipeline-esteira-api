using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Fatias.Decisoes
{
    // Erros das decisões sobre a Fatia (spec P2, "Regras de recusa"). Os de Aprovar/Reprovar entram nas tasks seguintes.
    public static class DecisaoErros
    {
        public static readonly Erro FatiaNaoEncontrada = new(
            "fatia.nao-encontrada", "Fatia não encontrada no mapa desta Aplicação.", TipoErro.NotFound);

        public static readonly Erro SemMrAberta = new(
            "fatia.sem-mr-aberta", "A Fatia precisa estar Em revisão, com merge request aberta.", TipoErro.Conflict);

        public static readonly Erro ReviewJaPedido = new(
            "fatia.review-ja-pedido", "O review desta Fatia já foi pedido.", TipoErro.Conflict);

        // Aprovar com alguma MR aberta sem pipeline `success`: a mensagem lista as MRs (ex.: "api !3, frontend !8").
        public static Erro CiNaoVerde(IEnumerable<MrAbertaDaFatia> mrs) => new(
            "fatia.ci-nao-verde",
            "Há MR aberta sem pipeline verde: " + string.Join(", ", mrs.Select(m => $"{PainelErros.Texto(m.Repositorio)} !{m.Iid}")) + ".",
            TipoErro.Conflict);

        public static readonly Erro PessoaNaoRegistrada = new(
            "acesso.pessoa-nao-registrada", "Sua pessoa ainda não foi registrada na Esteira. Entre de novo.", TipoErro.Unauthorized);
    }

    // MR aberta da Fatia num repositório de código da Aplicação. `ProjetoId` é o id interno do Projeto cadastrado.
    public sealed record MrAbertaDaFatia(RepositorioFatia Repositorio, long ProjetoId, long Iid, string? StatusPipeline);

    // O que as três decisões têm em comum depois de autorizar e reler o GitLab: a Fatia como está agora e suas MRs abertas
    // (por repositório, da mais antiga para a mais nova).
    public sealed record DecisaoPreparada(
        long AplicacaoId, string Sub, AplicacaoNoPainel Aplicacao, FatiaLida Fatia, IReadOnlyList<MrAbertaDaFatia> MrsAbertas);

    // Regras comuns de Abrir review, Aprovar e Reprovar: papel (404/403), estado fresco do GitLab, Fatia existente e Em revisão com MR aberta.
    public sealed class PreparadorDeDecisao(
        IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao, IPainelAplicacaoQuery apps, IPainelDeFatias painel, IUsuarioAtual usuario)
    {
        private static readonly Erro GitLabIndisponivel = new(
            "gitlab.indisponivel", "Não foi possível ler o GitLab agora. Tente novamente.", TipoErro.Failure);

        public async Task<Result<DecisaoPreparada>> PrepararAsync(
            Guid aplicacaoPublicId, string codigo, OperacaoAplicacao operacao, CancellationToken cancellationToken = default)
        {
            var id = await acesso.ResolverIdAsync(aplicacaoPublicId, cancellationToken);
            if (id is null)
                return Result<DecisaoPreparada>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var autorizado = await autorizacao.AutorizarAsync(id.Value, operacao, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<DecisaoPreparada>.Falha(autorizado.Errors.ToArray());

            var visiveis = await apps.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken);
            var app = visiveis.FirstOrDefault(a => a.Id == aplicacaoPublicId);
            if (app is null)
                return Result<DecisaoPreparada>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            // Estado fresco: nada de instantâneo guardado para decidir.
            var lido = await painel.ObterFrescoAsync(app.Entrada, cancellationToken);
            switch (lido.Situacao)
            {
                case SituacaoDoPainel.SemWorkspace:
                    return Result<DecisaoPreparada>.Falha(PainelErros.SemWorkspace);
                case SituacaoDoPainel.Falha:
                    return Result<DecisaoPreparada>.Falha(lido.Erro ?? GitLabIndisponivel);
            }

            var fatia = lido.Fatias.FirstOrDefault(f => string.Equals(f.Codigo, codigo?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (fatia is null)
                return Result<DecisaoPreparada>.Falha(DecisaoErros.FatiaNaoEncontrada);

            var abertas = fatia.Estado.Estado != EstadoDaFatia.EmRevisao
                ? []
                : (fatia.Detalhe?.Mrs ?? [])
                    .Where(m => string.Equals(m.Estado, "opened", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.Iid)
                    .Select(m => (Mr: m, Projeto: app.Projetos.FirstOrDefault(p => p.Repositorio == m.Repositorio)))
                    .Where(x => x.Projeto is not null)
                    .Select(x => new MrAbertaDaFatia(x.Mr.Repositorio, x.Projeto!.Id, x.Mr.Iid, x.Mr.StatusPipeline))
                    .ToList();

            if (abertas.Count == 0)
                return Result<DecisaoPreparada>.Falha(DecisaoErros.SemMrAberta);

            return Result<DecisaoPreparada>.Sucesso(new DecisaoPreparada(id.Value, usuario.Id, app, fatia, abertas));
        }
    }
}
