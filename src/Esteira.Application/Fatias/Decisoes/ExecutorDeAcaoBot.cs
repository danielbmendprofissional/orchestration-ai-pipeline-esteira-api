using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Fatias.Decisoes
{
    public enum SituacaoDaExecucao
    {
        Concluida,
        JaConcluida,
        AguardandoDependencia,
        // A pipeline do preview foi criada (ou já existia); quem conclui é a consulta periódica da pipeline.
        AguardandoPipeline,
        Falhou,
        Transitoria,
        NaoImplementada,
        NaoEncontrada
    }

    // O que o executor devolve ao consumer. Falhou e Transitoria levam o CodigoErro; a diferença é o que o consumer faz:
    // transitória é reentregue (política de retry), definitiva já foi gravada na Ação como falhou.
    public sealed record ResultadoDaExecucao(SituacaoDaExecucao Situacao, string? CodigoErro = null)
    {
        public static ResultadoDaExecucao Concluida { get; } = new(SituacaoDaExecucao.Concluida);
        public static ResultadoDaExecucao JaConcluida { get; } = new(SituacaoDaExecucao.JaConcluida);
        public static ResultadoDaExecucao AguardandoDependencia { get; } = new(SituacaoDaExecucao.AguardandoDependencia);
        public static ResultadoDaExecucao AguardandoPipeline { get; } = new(SituacaoDaExecucao.AguardandoPipeline);
        public static ResultadoDaExecucao NaoImplementada { get; } = new(SituacaoDaExecucao.NaoImplementada);
        public static ResultadoDaExecucao NaoEncontrada { get; } = new(SituacaoDaExecucao.NaoEncontrada);
        public static ResultadoDaExecucao Falhou(string codigo) => new(SituacaoDaExecucao.Falhou, codigo);
        public static ResultadoDaExecucao Transitoria(string codigo) => new(SituacaoDaExecucao.Transitoria, codigo);
    }

    // Executor da Ação do bot (spec P2b, "Consumer de AcaoBotPedida"): lê a Ação, leva pedida -> executando pelo
    // RegistrarProgressoDaAcao e executa no GitLab como o bot do grupo. Mensagem repetida de Ação já fora de
    // pedida/executando não escreve de novo; o estado que o GitLab já mostra (MR merged/closed, marcador da nota)
    // também não se repete. O texto do humano e o token de trigger nunca vão para o log.
    public sealed class ExecutorDeAcaoBot(
        IAcaoBotRepositorio acoes,
        IPessoaRepositorio pessoas,
        IGitLab gitlab,
        RegistrarProgressoDaAcao progresso,
        ContextoGitLabDaAcao contexto,
        ConclusaoDaAcaoBot conclusao,
        IUnitOfWork uow,
        TimeProvider relogio,
        ILogger<ExecutorDeAcaoBot> log)
    {
        private const string CodigoIndisponivel = "gitlab.indisponivel";
        private const string CodigoMrNaoEstaAberta = "gitlab.mr-nao-esta-aberta";
        private const string CodigoCiNaoVerde = "gitlab.ci-nao-verde";
        private const string CodigoMergeRecusado = "gitlab.merge-recusado";
        // Defensivo: a Ação aponta para uma Aplicação/Projeto que não existe (invariante de dados quebrada).
        private const string CodigoProjetoNaoEncontrado = "acao-bot.projeto-nao-encontrado";
        private const string StatusVerde = "success";
        private const string DescricaoDoToken = "esteira-acao-";
        private static readonly TimeSpan JanelaDeLimpeza = TimeSpan.FromHours(1);

        public async Task<ResultadoDaExecucao> ExecutarAsync(Guid acaoPublicId, CancellationToken cancellationToken = default)
        {
            var acao = await acoes.ObterPorPublicIdAsync(acaoPublicId, cancellationToken);
            if (acao is null)
            {
                log.LogWarning("Ação do bot {Acao} da mensagem AcaoBotPedida não existe.", acaoPublicId);
                return ResultadoDaExecucao.NaoEncontrada;
            }

            // Reentrega ou estado terminal: nada a escrever no GitLab (critério 10).
            if (acao.Estado is EstadoAcaoBot.Confirmada or EstadoAcaoBot.Falhou or EstadoAcaoBot.Cancelada)
                return ResultadoDaExecucao.JaConcluida;

            if (acao.Estado == EstadoAcaoBot.Pedida)
            {
                // API primeiro (ADR-0025): a Ação do front só começa com a da API confirmada (critério 11).
                if (acao.DependeDeAcaoId is { } dependeDeId)
                {
                    var dependencia = await acoes.ObterAsync(dependeDeId, cancellationToken);
                    if (dependencia is null || dependencia.Estado != EstadoAcaoBot.Confirmada)
                        return ResultadoDaExecucao.AguardandoDependencia;
                }

                var iniciada = await progresso.RegistrarAsync(acao.PublicId, EstadoAcaoBot.Executando, null, cancellationToken);
                if (!iniciada.IsSuccess)
                {
                    log.LogWarning("Ação {Acao} não pôde ir para executando ({Codigo}).",
                        acaoPublicId, iniciada.Errors[0].Code);
                    return ResultadoDaExecucao.Falhou(iniciada.Errors[0].Code);
                }
            }

            // Daqui em diante o estado é executando — inclusive quando a mensagem foi reentregue no meio (crash).
            return acao.Tipo switch
            {
                TipoAcaoBot.AbrirReview => await ExecutarAbrirReviewAsync(acao, cancellationToken),
                TipoAcaoBot.Aprovar => await ExecutarAprovarAsync(acao, cancellationToken),
                TipoAcaoBot.Reprovar => await ExecutarReprovarAsync(acao, cancellationToken),
                _ => ResultadoDaExecucao.NaoImplementada
            };
        }

        // abrir-review (critérios 5, 7 e 12): no Projeto que não é Angular confirma na hora, sem GitLab; no Angular cria
        // trigger token, dispara a pipeline `preview` na branch da MR e apaga o token em todo caminho. Grava o PipelineId
        // e devolve AguardandoPipeline — a Ação segue `executando` até a consulta ver o fim da pipeline (critério 6).
        private async Task<ResultadoDaExecucao> ExecutarAbrirReviewAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var doGitLab = await contexto.ResolverAsync(acao, ct);
            if (doGitLab is null)
                return await conclusao.FalharAsync(acao, CodigoProjetoNaoEncontrado, ct);

            if (doGitLab.Stack != Stack.Angular)
                return await conclusao.ConfirmarAsync(acao, ct);

            // Retomada (crash depois de criar a pipeline): não cria outra; a consulta conclui a que já existe.
            if (acao.PipelineId is not null)
                return ResultadoDaExecucao.AguardandoPipeline;

            var mr = await gitlab.ObterMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
            if (!mr.IsSuccess)
                return await TratarErroAsync(acao, mr.Errors[0], ct);
            if (!EstaAberta(mr.Value.Estado))
                return await conclusao.FalharAsync(acao, CodigoMrNaoEstaAberta, ct);

            await LimparTriggerTokensAntigosAsync(doGitLab, acao, ct);

            var token = await gitlab.CriarTriggerTokenAsync(
                doGitLab.Grupo, doGitLab.ProjetoGitLabId, $"{DescricaoDoToken}{acao.ChaveIdempotencia}", ct);
            if (!token.IsSuccess)
                return await TratarErroAsync(acao, token.Errors[0], ct);

            try
            {
                var variaveis = new Dictionary<string, string>
                {
                    ["PIPELINE_PROCESS"] = "preview",
                    ["PREVIEW_FATIA"] = acao.FatiaCodigo.ToLowerInvariant(),
                };
                var pipeline = await gitlab.CriarPipelinePorTriggerAsync(
                    doGitLab.Grupo, doGitLab.ProjetoGitLabId, token.Value.Token, mr.Value.BranchOrigem, variaveis, ct);
                if (!pipeline.IsSuccess)
                    return await TratarErroAsync(acao, pipeline.Errors[0], ct);

                acao.RegistrarPipeline(pipeline.Value);
                await uow.SaveChangesAsync();
                return ResultadoDaExecucao.AguardandoPipeline;
            }
            finally
            {
                // Apaga o token em todo caminho (sucesso, erro e cancelamento); falha só vai para o log, sem o valor.
                await ApagarTriggerTokenAsync(doGitLab, token.Value.Id, acao);
            }
        }

        private async Task<ResultadoDaExecucao> ExecutarAprovarAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var doGitLab = await contexto.ResolverAsync(acao, ct);
            if (doGitLab is null)
                return await conclusao.FalharAsync(acao, CodigoProjetoNaoEncontrado, ct);

            var mr = await gitlab.ObterMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
            if (!mr.IsSuccess)
                return await TratarErroAsync(acao, mr.Errors[0], ct);

            var estado = mr.Value.Estado;

            if (EstaAberta(estado))
            {
                // Critério 2: a pipeline da cabeça deixou de ser success depois do pedido -> falha sem mergear.
                if (!string.Equals(mr.Value.StatusPipeline, StatusVerde, StringComparison.OrdinalIgnoreCase))
                    return await conclusao.FalharAsync(acao, CodigoCiNaoVerde, ct);

                // Critério 1: merge commit, sem squash e sem apagar a branch de origem (a porta já manda assim).
                var merge = await gitlab.MergearMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
                if (!merge.IsSuccess)
                    return await TratarErroAsync(acao, merge.Errors[0], ct);
                if (!string.Equals(merge.Value.Estado, "merged", StringComparison.OrdinalIgnoreCase))
                    return await conclusao.FalharAsync(acao, CodigoMergeRecusado, ct);
            }
            else if (!string.Equals(estado, "merged", StringComparison.OrdinalIgnoreCase))
            {
                return await conclusao.FalharAsync(acao, CodigoMrNaoEstaAberta, ct);
            }

            // Comentário depois do merge e melhor-esforço: se falhar, não desfaz o merge nem falha a Ação (só log).
            var erroDoComentario = await ComentarSeAusenteAsync(acao, doGitLab, ct);
            if (erroDoComentario is not null)
                log.LogWarning("Ação {Acao}: comentário não postado depois do merge ({Codigo}); o merge já vale.",
                    acao.PublicId, erroDoComentario.Code);

            return await conclusao.ConfirmarAsync(acao, ct);
        }

        private async Task<ResultadoDaExecucao> ExecutarReprovarAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var doGitLab = await contexto.ResolverAsync(acao, ct);
            if (doGitLab is null)
                return await conclusao.FalharAsync(acao, CodigoProjetoNaoEncontrado, ct);

            var mr = await gitlab.ObterMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
            if (!mr.IsSuccess)
                return await TratarErroAsync(acao, mr.Errors[0], ct);

            var estado = mr.Value.Estado;
            var fechada = string.Equals(estado, "closed", StringComparison.OrdinalIgnoreCase);

            // merged ou locked: a MR não está aberta para reprovar.
            if (!EstaAberta(estado) && !fechada)
                return await conclusao.FalharAsync(acao, CodigoMrNaoEstaAberta, ct);

            // Critério 3: o comentário vem antes de fechar e é parte da Ação — se falhar, não fecha.
            var erroDoComentario = await ComentarSeAusenteAsync(acao, doGitLab, ct);
            if (erroDoComentario is not null)
                return await TratarErroAsync(acao, erroDoComentario, ct);

            // Já closed: idempotente, confirma sem repetir o fechamento (critério 10).
            if (fechada)
                return await conclusao.ConfirmarAsync(acao, ct);

            var resultadoFechar = await gitlab.FecharMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
            if (!resultadoFechar.IsSuccess)
                return await TratarErroAsync(acao, resultadoFechar.Errors[0], ct);
            if (!string.Equals(resultadoFechar.Value.Estado, "closed", StringComparison.OrdinalIgnoreCase))
                return await conclusao.FalharAsync(acao, CodigoMrNaoEstaAberta, ct);

            return await conclusao.ConfirmarAsync(acao, ct);
        }

        // Procura o marcador da Ação nas notas; só comenta quando ele não existe. Devolve o erro (sem o texto do
        // comentário) quando a leitura ou a escrita falha, ou null quando o comentário está garantido (postado ou já presente).
        private async Task<Erro?> ComentarSeAusenteAsync(AcaoBotEntity acao, ContextoGitLab doGitLab, CancellationToken ct)
        {
            var marcador = $"<!-- esteira-acao:{acao.ChaveIdempotencia} -->";

            var notas = await gitlab.ListarNotasMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, ct);
            if (!notas.IsSuccess)
            {
                // Sem ler as notas não dá para garantir que o comentário não seria duplicado.
                log.LogWarning("Ação {Acao}: não foi possível ler as notas da MR ({Codigo}).", acao.PublicId, notas.Errors[0].Code);
                return notas.Errors[0];
            }

            if (notas.Value.Any(nota => nota.Contains(marcador, StringComparison.Ordinal)))
                return null;

            var pessoa = await pessoas.ObterPorIdAsync(acao.PedidaPorPessoaId, ct);
            var corpo = CorpoDoComentario(acao, marcador, pessoa?.Nome);

            var comentario = await gitlab.ComentarMergeRequestAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, acao.MrIid, corpo, ct);
            if (!comentario.IsSuccess)
            {
                log.LogWarning("Ação {Acao}: falha ao comentar na MR ({Codigo}).", acao.PublicId, comentario.Errors[0].Code);
                return comentario.Errors[0];
            }

            return null;
        }

        private static string CorpoDoComentario(AcaoBotEntity acao, string marcador, string? nomeDoHumano)
        {
            var nome = string.IsNullOrWhiteSpace(nomeDoHumano) ? "a Esteira" : nomeDoHumano;

            // O marcador abre o corpo (idempotência); no Reprovar o texto entra citado, linha a linha, para que uma
            // linha como `/merge` não vire ação rápida do GitLab executada como o bot. O nome também é neutralizado.
            return acao.Tipo == TipoAcaoBot.Reprovar
                ? marcador + "\n" + CitacaoDoHumano.Citar(nome, acao.Comentario ?? string.Empty)
                : marcador + "\n> Aprovado por " + SemQuebras(nome) + ".";
        }

        private static string SemQuebras(string valor)
            => valor.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

        // Critério 12: no começo de cada abrir-review apaga os trigger tokens `esteira-acao-*` mais velhos que uma hora.
        // Falha ao listar/apagar só vai para o log, sem o valor do token.
        private async Task LimparTriggerTokensAntigosAsync(ContextoGitLab doGitLab, AcaoBotEntity acao, CancellationToken ct)
        {
            try
            {
                var tokens = await gitlab.ListarTriggerTokensAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, ct);
                if (!tokens.IsSuccess)
                {
                    log.LogWarning("Ação {Acao}: não foi possível listar os trigger tokens ({Codigo}).",
                        acao.PublicId, tokens.Errors[0].Code);
                    return;
                }

                var limite = relogio.GetUtcNow() - JanelaDeLimpeza;
                foreach (var token in tokens.Value.Where(t =>
                             t.Description.StartsWith(DescricaoDoToken, StringComparison.Ordinal) && t.CreatedAt < limite))
                    await ApagarTriggerTokenAsync(doGitLab, token.Id, acao);
            }
            catch (Exception ex)
            {
                log.LogWarning("Ação {Acao}: limpeza dos trigger tokens falhou ({Tipo}).", acao.PublicId, ex.GetType().Name);
            }
        }

        // Apaga o token sem deixar o cancelamento impedir a limpeza; qualquer falha só vai para o log (sem o valor).
        private async Task ApagarTriggerTokenAsync(ContextoGitLab doGitLab, long tokenId, AcaoBotEntity acao)
        {
            try
            {
                var apagado = await gitlab.ApagarTriggerTokenAsync(
                    doGitLab.Grupo, doGitLab.ProjetoGitLabId, tokenId, CancellationToken.None);
                if (!apagado.IsSuccess)
                    log.LogWarning("Ação {Acao}: trigger token não apagado ({Codigo}).", acao.PublicId, apagado.Errors[0].Code);
            }
            catch (Exception ex)
            {
                log.LogWarning("Ação {Acao}: falha ao apagar o trigger token ({Tipo}).", acao.PublicId, ex.GetType().Name);
            }
        }

        // Transitório (retry reentrega) x definitivo (a Ação já foi marcada falhou).
        private async Task<ResultadoDaExecucao> TratarErroAsync(AcaoBotEntity acao, Erro erro, CancellationToken ct)
            => EhTransitorio(erro)
                ? ResultadoDaExecucao.Transitoria(erro.Code)
                : await conclusao.FalharAsync(acao, erro.Code, ct);

        private static bool EhTransitorio(Erro erro)
            => erro.Code == CodigoIndisponivel || erro.Type == TipoErro.TooManyRequests;

        private static bool EstaAberta(string estado)
            => string.Equals(estado, "opened", StringComparison.OrdinalIgnoreCase);
    }
}
