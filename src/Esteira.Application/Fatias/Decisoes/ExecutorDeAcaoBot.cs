using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Fatias.Decisoes
{
    public enum SituacaoDaExecucao
    {
        Concluida,
        JaConcluida,
        AguardandoDependencia,
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
        public static ResultadoDaExecucao NaoImplementada { get; } = new(SituacaoDaExecucao.NaoImplementada);
        public static ResultadoDaExecucao NaoEncontrada { get; } = new(SituacaoDaExecucao.NaoEncontrada);
        public static ResultadoDaExecucao Falhou(string codigo) => new(SituacaoDaExecucao.Falhou, codigo);
        public static ResultadoDaExecucao Transitoria(string codigo) => new(SituacaoDaExecucao.Transitoria, codigo);
    }

    // Executor da Ação do bot (spec P2b, "Consumer de AcaoBotPedida"): lê a Ação, leva pedida -> executando pelo
    // RegistrarProgressoDaAcao e executa aprovar/reprovar no GitLab como o bot do grupo. Mensagem repetida de Ação já
    // fora de pedida/executando não escreve de novo; o estado que o GitLab já mostra (MR merged/closed, marcador da nota)
    // também não se repete. O texto do humano nunca vai para o log.
    public sealed class ExecutorDeAcaoBot(
        IAcaoBotRepositorio acoes,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IPessoaRepositorio pessoas,
        IGitLab gitlab,
        RegistrarProgressoDaAcao progresso,
        IPublishEndpoint publicador,
        ILogger<ExecutorDeAcaoBot> log)
    {
        private const string CodigoIndisponivel = "gitlab.indisponivel";
        private const string CodigoMrNaoEstaAberta = "gitlab.mr-nao-esta-aberta";
        private const string CodigoCiNaoVerde = "gitlab.ci-nao-verde";
        private const string CodigoMergeRecusado = "gitlab.merge-recusado";
        // Defensivo: a Ação aponta para uma Aplicação/Projeto que não existe (invariante de dados quebrada).
        private const string CodigoProjetoNaoEncontrado = "acao-bot.projeto-nao-encontrado";
        private const string StatusVerde = "success";

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

            // abrir-review (trigger token, pipeline e consulta) é da P2b.3: aqui não muda estado nem escreve.
            if (acao.Tipo is not (TipoAcaoBot.Aprovar or TipoAcaoBot.Reprovar))
            {
                log.LogInformation("Ação {Acao} do tipo {Tipo} ainda não é executada por este executor.",
                    acaoPublicId, EnumKebab.Texto(acao.Tipo));
                return ResultadoDaExecucao.NaoImplementada;
            }

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
            return acao.Tipo == TipoAcaoBot.Aprovar
                ? await ExecutarAprovarAsync(acao, cancellationToken)
                : await ExecutarReprovarAsync(acao, cancellationToken);
        }

        private async Task<ResultadoDaExecucao> ExecutarAprovarAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var contexto = await ResolverContextoAsync(acao, ct);
            if (contexto is null)
                return await FalharAsync(acao, CodigoProjetoNaoEncontrado, ct);

            var mr = await gitlab.ObterMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, ct);
            if (!mr.IsSuccess)
                return await TratarErroAsync(acao, mr.Errors[0], ct);

            var estado = mr.Value.Estado;

            if (EstaAberta(estado))
            {
                // Critério 2: a pipeline da cabeça deixou de ser success depois do pedido -> falha sem mergear.
                if (!string.Equals(mr.Value.StatusPipeline, StatusVerde, StringComparison.OrdinalIgnoreCase))
                    return await FalharAsync(acao, CodigoCiNaoVerde, ct);

                // Critério 1: merge commit, sem squash e sem apagar a branch de origem (a porta já manda assim).
                var merge = await gitlab.MergearMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, ct);
                if (!merge.IsSuccess)
                    return await TratarErroAsync(acao, merge.Errors[0], ct);
                if (!string.Equals(merge.Value.Estado, "merged", StringComparison.OrdinalIgnoreCase))
                    return await FalharAsync(acao, CodigoMergeRecusado, ct);
            }
            else if (!string.Equals(estado, "merged", StringComparison.OrdinalIgnoreCase))
            {
                return await FalharAsync(acao, CodigoMrNaoEstaAberta, ct);
            }

            // Comentário depois do merge e melhor-esforço: se falhar, não desfaz o merge nem falha a Ação (só log).
            var erroDoComentario = await ComentarSeAusenteAsync(acao, contexto, ct);
            if (erroDoComentario is not null)
                log.LogWarning("Ação {Acao}: comentário não postado depois do merge ({Codigo}); o merge já vale.",
                    acao.PublicId, erroDoComentario.Code);

            return await ConfirmarAsync(acao, ct);
        }

        private async Task<ResultadoDaExecucao> ExecutarReprovarAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var contexto = await ResolverContextoAsync(acao, ct);
            if (contexto is null)
                return await FalharAsync(acao, CodigoProjetoNaoEncontrado, ct);

            var mr = await gitlab.ObterMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, ct);
            if (!mr.IsSuccess)
                return await TratarErroAsync(acao, mr.Errors[0], ct);

            var estado = mr.Value.Estado;
            var fechada = string.Equals(estado, "closed", StringComparison.OrdinalIgnoreCase);

            // merged ou locked: a MR não está aberta para reprovar.
            if (!EstaAberta(estado) && !fechada)
                return await FalharAsync(acao, CodigoMrNaoEstaAberta, ct);

            // Critério 3: o comentário vem antes de fechar e é parte da Ação — se falhar, não fecha.
            var erroDoComentario = await ComentarSeAusenteAsync(acao, contexto, ct);
            if (erroDoComentario is not null)
                return await TratarErroAsync(acao, erroDoComentario, ct);

            // Já closed: idempotente, confirma sem repetir o fechamento (critério 10).
            if (fechada)
                return await ConfirmarAsync(acao, ct);

            var resultadoFechar = await gitlab.FecharMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, ct);
            if (!resultadoFechar.IsSuccess)
                return await TratarErroAsync(acao, resultadoFechar.Errors[0], ct);
            if (!string.Equals(resultadoFechar.Value.Estado, "closed", StringComparison.OrdinalIgnoreCase))
                return await FalharAsync(acao, CodigoMrNaoEstaAberta, ct);

            return await ConfirmarAsync(acao, ct);
        }

        // Procura o marcador da Ação nas notas; só comenta quando ele não existe. Devolve o erro (sem o texto do
        // comentário) quando a leitura ou a escrita falha, ou null quando o comentário está garantido (postado ou já presente).
        private async Task<Erro?> ComentarSeAusenteAsync(AcaoBotEntity acao, ContextoGitLab contexto, CancellationToken ct)
        {
            var marcador = $"<!-- esteira-acao:{acao.ChaveIdempotencia} -->";

            var notas = await gitlab.ListarNotasMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, ct);
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

            var comentario = await gitlab.ComentarMergeRequestAsync(contexto.Grupo, contexto.ProjetoGitLabId, acao.MrIid, corpo, ct);
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

        private async Task<ResultadoDaExecucao> ConfirmarAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            // Republica a mensagem dos dependentes ainda pedidos ANTES de salvar: o outbox do MassTransit grava no
            // mesmo SaveChanges do RegistrarProgressoDaAcao, então a retomada da Ação do front é atômica com a confirmação.
            var daFatia = await acoes.ListarDaFatiaAsync(acao.AplicacaoId, acao.FatiaCodigo, ct);
            foreach (var dependente in daFatia.Where(a => a.DependeDeAcaoId == acao.Id && a.Estado == EstadoAcaoBot.Pedida))
                await publicador.Publish(
                    new AcaoBotPedida(dependente.PublicId, EnumKebab.Texto(dependente.Tipo), dependente.FatiaCodigo, dependente.ChaveIdempotencia),
                    ct);

            var confirmada = await progresso.RegistrarAsync(acao.PublicId, EstadoAcaoBot.Confirmada, null, ct);
            if (!confirmada.IsSuccess)
            {
                log.LogWarning("Ação {Acao}: não foi possível confirmar ({Codigo}).", acao.PublicId, confirmada.Errors[0].Code);
                return ResultadoDaExecucao.Falhou(confirmada.Errors[0].Code);
            }

            return ResultadoDaExecucao.Concluida;
        }

        private async Task<ResultadoDaExecucao> FalharAsync(AcaoBotEntity acao, string codigo, CancellationToken ct)
        {
            var falha = await progresso.RegistrarAsync(acao.PublicId, EstadoAcaoBot.Falhou, codigo, ct);
            if (!falha.IsSuccess)
                log.LogWarning("Ação {Acao}: não foi possível registrar a falha ({Codigo}).", acao.PublicId, falha.Errors[0].Code);

            return ResultadoDaExecucao.Falhou(codigo);
        }

        // Transitório (retry reentrega) x definitivo (a Ação já foi marcada falhou).
        private async Task<ResultadoDaExecucao> TratarErroAsync(AcaoBotEntity acao, Erro erro, CancellationToken ct)
            => EhTransitorio(erro)
                ? ResultadoDaExecucao.Transitoria(erro.Code)
                : await FalharAsync(acao, erro.Code, ct);

        private static bool EhTransitorio(Erro erro)
            => erro.Code == CodigoIndisponivel || erro.Type == TipoErro.TooManyRequests;

        private static bool EstaAberta(string estado)
            => string.Equals(estado, "opened", StringComparison.OrdinalIgnoreCase);

        private async Task<ContextoGitLab?> ResolverContextoAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            var aplicacao = await aplicacoes.ObterAsync(acao.AplicacaoId, ct);
            if (aplicacao is null)
            {
                log.LogError("Ação {Acao}: Aplicação {Aplicacao} não existe.", acao.PublicId, acao.AplicacaoId);
                return null;
            }

            var doProjeto = await projetos.ProjetosDaAplicacaoAsync(acao.AplicacaoId, ct);
            var projeto = doProjeto.FirstOrDefault(p => p.Id == acao.ProjetoId);
            if (projeto is null)
            {
                log.LogError("Ação {Acao}: Projeto {Projeto} não existe na Aplicação.", acao.PublicId, acao.ProjetoId);
                return null;
            }

            return new ContextoGitLab(aplicacao.GrupoGitLabCaminho, projeto.ProjetoGitLabId);
        }

        private sealed record ContextoGitLab(string Grupo, long ProjetoGitLabId);
    }
}
