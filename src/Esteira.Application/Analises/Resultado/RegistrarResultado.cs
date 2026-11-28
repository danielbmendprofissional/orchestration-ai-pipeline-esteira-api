using Esteira.Application.Analises.Gate;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Analises.Resultado
{
    public interface IResultadoDaPipeline
    {
        // Mesmo corpo do gate, com `escopo: deploy` e `destino` em cada Análise. `projetoDoToken` já autenticado (a Api devolve 401 antes).
        Task<Result<GateResposta>> ExecutarAsync(string? projetoDoToken, GateCorpo? corpo, CancellationToken cancellationToken = default);
    }

    // `esteira-resultado` (spec 03-analises-e-gate.md): depois do deploy; só REGISTRA (ADR-0021). Responde 200 mesmo quando a
    // Política seria violada; em qa/hml isso leva a Etapa a `bloqueada` (barra a próxima promoção); em production nunca bloqueia.
    // Texto de terceiro (Achados) nunca vai a log nem a erro.
    public sealed class RegistrarResultadoHandler(
        IProjetoPorCaminhoQuery projetosPorCaminho,
        IEntregaRepositorio entregas,
        IProjetoRepositorio projetos,
        IAplicacaoRepositorio aplicacoes,
        IAnaliseRepositorio analises,
        IExcecoesVigentesQuery excecoes,
        IUnitOfWork unitOfWork,
        TimeProvider relogio,
        IAcessoAplicacaoQuery? acesso = null,
        IPublicadorDeEventos? eventos = null,
        IOpcoesDaEntrega? opcoes = null,
        ILogger<RegistrarResultadoHandler>? log = null) : IResultadoDaPipeline
    {
        private const int Tentativas = 3;

        private static readonly Erro CorpoInvalido = PorteiroDaPipeline.CorpoInvalido;
        private static readonly Erro NaoSuportado = new("pipeline.processo-nao-suportado", "O processo não é suportado por esta versão da Esteira.", TipoErro.Failure);
        private static readonly Erro SemEntrega = new("pipeline.sem-entrega-aberta", "Não há Entrega aberta para este Fluxo.", TipoErro.Conflict);
        private static readonly Erro Desabilitado = new("ambiente.desabilitado", "O Ambiente não está habilitado no Projeto.", TipoErro.Failure);
        private static readonly Erro SemArtefato = new("pipeline.sem-artefato", "O Fluxo ainda não tem Artefato registrado.", TipoErro.Conflict);
        private static readonly Erro ShaDiferente = new("pipeline.sha-diferente", "O SHA difere do registrado no Fluxo.", TipoErro.Conflict);
        private static readonly Erro SemAnalises = new("gate.sem-analises", "O resultado não recebeu nenhuma Análise.", TipoErro.Failure);
        private static readonly Erro ConflitoDeGravacao = new("pipeline.conflito-de-gravacao", "Outra gravação concorrente no mesmo Fluxo; tente de novo.", TipoErro.Conflict);
        private static readonly HashSet<string> Processos = ["fluxo", "promocao", "liberacao", "flag", "rollback", "rotulo"];
        private static readonly HashSet<string> Ambientes = ["qa", "hml", "production"];

        public async Task<Result<GateResposta>> ExecutarAsync(string? projetoDoToken, GateCorpo? corpo, CancellationToken cancellationToken = default)
        {
            if (corpo is null)
                return CorpoInvalido;
            if (string.IsNullOrWhiteSpace(projetoDoToken) || !string.Equals(corpo.Projeto, projetoDoToken, StringComparison.Ordinal))
                return PorteiroDaPipeline.ProjetoNaoReconhecido;
            if (!Validar(corpo, out var entregaId, out var fluxoId, out var pipelineId))
                return CorpoInvalido;
            if (Construir(0, AmbienteDeploy.Qa, corpo, pipelineId, default) is null)
                return CorpoInvalido;

            var projetoId = await projetosPorCaminho.ObterIdAtivoAsync(projetoDoToken, cancellationToken);
            if (projetoId is null)
                return PorteiroDaPipeline.ProjetoNaoReconhecido;

            var fluxoProcesso = corpo.Process == "fluxo";
            var promocao = corpo.Process == "promocao";
            AmbienteDeploy ambiente;
            if (fluxoProcesso && corpo.Env == "qa") ambiente = AmbienteDeploy.Qa;
            else if (fluxoProcesso && corpo.Env == "production") ambiente = AmbienteDeploy.Producao;
            else if (promocao && corpo.Env == "hml") ambiente = AmbienteDeploy.Hml;
            else if (promocao && corpo.Env == "production") ambiente = AmbienteDeploy.Producao;
            else return NaoSuportado;

            var entrega = await entregas.ObterPorPublicIdAsync(entregaId, cancellationToken);
            var fluxo = entrega?.Fluxos.FirstOrDefault(f => f.PublicId == fluxoId);
            if (entrega is null || entrega.Situacao != SituacaoEntrega.Aberta || fluxo is null || fluxo.ProjetoId != projetoId)
                return SemEntrega;

            var cadastro = await projetos.ObterAmbienteAsync(fluxo.ProjetoId, ambiente, cancellationToken);
            if (cadastro is null || !cadastro.Habilitado)
                return Desabilitado;

            if (fluxo.Sha is null) return SemArtefato;
            if (fluxo.Sha != corpo.Sha) return ShaDiferente;

            if (corpo.Analises is not { Count: > 0 })
                return SemAnalises;

            var politica = await aplicacoes.ObterPoliticaAsync(entrega.AplicacaoId, ParaPolitica(ambiente), cancellationToken);
            if (politica is null)
                return Result<GateResposta>.Falha(new Erro("politica.nao-encontrada", "A Aplicação não tem Política para este Ambiente.", TipoErro.Conflict));
            var cobertas = await excecoes.ObterChavesCobertasAsync(entrega.AplicacaoId, ambiente, cancellationToken);
            var avaliacao = AvaliadorDoGate.Avaliar(Construir(fluxo.Id, ambiente, corpo, pipelineId, default)!, politica, cobertas, MomentoDoGate.Resultado);

            // Em production o resultado só alerta (ADR-0023): o excesso vira `avisou` e nada bloqueia.
            if (ambiente == AmbienteDeploy.Producao && avaliacao.Decisao == DecisaoGate.Bloqueado)
                avaliacao = new AvaliacaoDoGate(DecisaoGate.Avisou, avaliacao.Contagem, null);

            var etapa = fluxo.Etapa(EtapaDoAmbiente(ambiente));
            var bloquear = avaliacao.Decisao == DecisaoGate.Bloqueado && opcoes?.E1bLigada == true;
            var bloqueou = false;
            var gravou = false;

            for (var tentativa = 1; tentativa <= Tentativas && !gravou; tentativa++)
            {
                var em = relogio.GetUtcNow().UtcDateTime;
                await analises.SubstituirAsync(fluxo.Id, ambiente, EscopoAnalise.Deploy, pipelineId, Construir(fluxo.Id, ambiente, corpo, pipelineId, em)!, cancellationToken);
                await analises.RegistrarDecisaoAsync(fluxo.Id, ambiente, MomentoDoGate.Resultado, avaliacao, pipelineId, em, cancellationToken);
                bloqueou = bloquear && etapa.Bloquear(avaliacao.Motivo!, em).IsSuccess;
                try
                {
                    await unitOfWork.SaveChangesAsync();
                    gravou = true;
                }
                catch (ViolacaoDeUnicidadeException)
                {
                    // Reenvio simultâneo do mesmo job: o outro gravou primeiro. Regrava do zero (substituição).
                    analises.DescartarPendentes();
                }
                catch (ConcorrenciaOtimistaException)
                {
                    // O webhook (ou o esteira-abrir) gravou a Etapa antes: relê o vencedor e reaplica o bloqueio.
                    analises.DescartarPendentes();
                    await entregas.RecarregarAsync(etapa, cancellationToken);
                }
            }

            if (!gravou)
                return ConflitoDeGravacao;

            log?.LogInformation("esteira-resultado: decisão {Decisao} (crítica {Critica}, alta {Alta}, média {Media}, baixa {Baixa}) no Fluxo {FluxoId}",
                avaliacao.Decisao, avaliacao.Contagem.Critica, avaliacao.Contagem.Alta, avaliacao.Contagem.Media, avaliacao.Contagem.Baixa, fluxo.PublicId);

            if (bloqueou)
                await PublicarAsync(entrega, fluxo, etapa, cancellationToken);

            return Result<GateResposta>.Sucesso(new GateResposta(EnumKebab.Texto(avaliacao.Decisao), avaliacao.Contagem));
        }

        private static AmbientePolitica ParaPolitica(AmbienteDeploy a) => a switch
        {
            AmbienteDeploy.Qa => AmbientePolitica.Qa,
            AmbienteDeploy.Hml => AmbientePolitica.Hml,
            _ => AmbientePolitica.Producao
        };

        private static EtapaDoFluxo EtapaDoAmbiente(AmbienteDeploy a) => a switch
        {
            AmbienteDeploy.Qa => EtapaDoFluxo.Qa,
            AmbienteDeploy.Hml => EtapaDoFluxo.Hml,
            _ => EtapaDoFluxo.Prd
        };

        private static bool Validar(GateCorpo c, out Guid entrega, out Guid fluxo, out long pipelineId)
        {
            entrega = fluxo = Guid.Empty;
            pipelineId = 0;
            if (c.Process is null || !Processos.Contains(c.Process))
                return false;
            if (c.Env is null || !Ambientes.Contains(c.Env))
                return false;
            if (c.Sha is null || c.Sha.Length != 40 || !c.Sha.All(ch => ch is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
            if (!Guid.TryParseExact(c.EntregaId, "D", out entrega) || !Guid.TryParseExact(c.FluxoId, "D", out fluxo))
                return false;
            return long.TryParse(c.PipelineId, out pipelineId) && pipelineId > 0 && c.Analises is not null && c.Analises.Count <= AvaliarGateHandler.MaxAnalises;
        }

        // Monta as Análises do corpo (escopo `deploy`, com Destino). Nulo = alguma inválida; a Domain valida o resto.
        private static List<AnaliseEntity>? Construir(long fluxoId, AmbienteDeploy ambiente, GateCorpo corpo, long pipelineId, DateTime em)
        {
            var lista = new List<AnaliseEntity>();
            foreach (var a in corpo.Analises ?? [])
            {
                if (a is null || a.Escopo != "deploy" || string.IsNullOrEmpty(a.Destino))
                    return null;
                TipoAnalise? tipo = a.Tipo switch { "ferramenta" => TipoAnalise.Ferramenta, "agente" => TipoAnalise.Agente, _ => null };
                VereditoAnalise? veredito = a.Veredito switch
                {
                    "aprovado" => VereditoAnalise.Aprovado,
                    "reprovado" => VereditoAnalise.Reprovado,
                    "erro" => VereditoAnalise.Erro,
                    _ => null
                };
                if (tipo is null || veredito is null || a.Achados is { Count: > AnaliseEntity.MaxAchados })
                    return null;

                var achados = new List<NovoAchado>();
                foreach (var x in a.Achados ?? [])
                {
                    SeveridadeAchado? severidade = x?.Severidade?.ToLowerInvariant() switch
                    {
                        "critical" => SeveridadeAchado.Critica,
                        "high" => SeveridadeAchado.Alta,
                        "medium" => SeveridadeAchado.Media,
                        "low" => SeveridadeAchado.Baixa,
                        "info" => SeveridadeAchado.Info,
                        _ => null
                    };
                    if (x is null || severidade is null)
                        return null;
                    achados.Add(new NovoAchado(severidade.Value, x.Titulo!, x.RegraId, x.Arquivo, x.Linha, x.Descricao));
                }

                var analise = AnaliseEntity.Criar(fluxoId, ambiente, EscopoAnalise.Deploy, a.Destino, a.Fonte!, tipo.Value, veredito.Value, a.CustoUsd, pipelineId, em, achados);
                if (!analise.IsSuccess)
                    return null;
                lista.Add(analise.Value);
            }
            return lista;
        }

        // Depois do commit. Falha do tempo real nunca derruba o resultado (o front recarrega ao reconectar).
        private async Task PublicarAsync(EntregaEntity entrega, FluxoEntity fluxo, EtapaFluxoEntity etapa, CancellationToken ct)
        {
            if (eventos is null || acesso is null)
                return;
            try
            {
                var aplicacao = await acesso.ObterPublicIdAsync(entrega.AplicacaoId, ct);
                if (aplicacao is null)
                    return;
                await eventos.PublicarAsync(new EntregaAtualizada(aplicacao.Value, entrega.PublicId, fluxo.PublicId, EnumKebab.Texto(etapa.Etapa), EnumKebab.Texto(etapa.Estado)), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log?.LogWarning(ex, "esteira-resultado: falha ao publicar entrega.atualizada");
            }
        }
    }
}
