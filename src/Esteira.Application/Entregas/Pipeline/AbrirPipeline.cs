using Esteira.Application.Common.Extensions;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esteira.Application.Entregas.Pipeline
{
    // Entrada do `esteira-abrir` já validada (passo 2): o que a E1.4 recebe. `Projeto` é o caminho já igualado ao
    // `project_path` do id_token e conferido como Projeto ativo cadastrado (passo 1).
    public sealed record AbrirPipelineEntrada(
        string Process,
        string Env,
        Guid EntregaId,
        Guid FluxoId,
        string Sha,
        string Projeto,
        string? PipelineId,
        string? Versao,
        string? AtalhoPor,
        string? AtalhoMotivo,
        bool FlagSomenteLeitura);

    public sealed record DestinoNaResposta(string Nome, int Ordem, IReadOnlyDictionary<string, string> Configuracao);

    public sealed record AbrirPipelineResposta(string FeatureFlags, IReadOnlyList<DestinoNaResposta> Destinos);

    // Passos 3–9 do `esteira-abrir` (processo suportado, Entrega e Fluxo, Ambiente, SHA, Atalho, ocupação, resposta).
    public interface IAbrirPipeline
    {
        Task<Result<AbrirPipelineResposta>> ExecutarAsync(AbrirPipelineEntrada entrada, CancellationToken cancellationToken = default);
    }

    // Porta de leitura: Id do Projeto ativo cadastrado com esse caminho do GitLab (nulo se não existe ou inativo).
    public interface IProjetoPorCaminhoQuery
    {
        Task<long?> ObterIdAtivoAsync(string caminho, CancellationToken cancellationToken = default);
    }

    // Porta de leitura: Destinos do Projeto no Ambiente, por Ordem, com a configuração cadastrada na A1.
    public interface IDestinosDaPipelineQuery
    {
        Task<IReadOnlyList<DestinoNaResposta>> ListarAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default);
    }

    // Passos 3 a 9 do `esteira-abrir`, em ordem e fail-closed. Nunca consulta o GitLab.
    public sealed class AbrirPipelineHandler(
        IProjetoPorCaminhoQuery projetosPorCaminho,
        IEntregaRepositorio entregas,
        IProjetoRepositorio projetos,
        IOcupacaoAmbienteRepositorio ocupacoes,
        IDestinosDaPipelineQuery destinos,
        IUnitOfWork unitOfWork,
        TimeProvider relogio,
        IAcessoAplicacaoQuery? acesso = null,
        IPublicadorDeEventos? eventos = null,
        IOpcoesDaEntrega? opcoes = null,
        ILogger<AbrirPipelineHandler>? log = null) : IAbrirPipeline
    {
        private static readonly Erro NaoSuportado = new("pipeline.processo-nao-suportado", "O processo não é suportado por esta versão da Esteira.", TipoErro.Failure);
        private static readonly Erro SemEntrega = new("pipeline.sem-entrega-aberta", "Não há Entrega aberta para este Fluxo.", TipoErro.Conflict);
        private static readonly Erro Desabilitado = new("ambiente.desabilitado", "O Ambiente não está habilitado no Projeto.", TipoErro.Failure);
        private static readonly Erro SemArtefato = new("pipeline.sem-artefato", "O Fluxo ainda não tem Artefato registrado.", TipoErro.Conflict);
        private static readonly Erro ShaDiferente = new("pipeline.sha-diferente", "O SHA difere do registrado no Fluxo.", TipoErro.Conflict);
        private static readonly Erro AtalhoObrigatorio = new("pipeline.atalho-obrigatorio", "O Atalho exige atalho_por e atalho_motivo.", TipoErro.Failure);
        private static readonly Erro CorpoInvalido = new("pipeline.corpo-invalido", "O corpo da requisição é inválido.", TipoErro.Failure);

        public async Task<Result<AbrirPipelineResposta>> ExecutarAsync(AbrirPipelineEntrada entrada, CancellationToken cancellationToken = default)
        {
            // 3. Processo suportado.
            var promocao = entrada.Process == "promocao";
            var fluxoProcesso = entrada.Process == "fluxo";
            AmbienteDeploy ambiente;
            if (fluxoProcesso && entrada.Env == "qa") ambiente = AmbienteDeploy.Qa;
            else if (fluxoProcesso && entrada.Env == "production") ambiente = AmbienteDeploy.Producao;
            else if (promocao && entrada.Env == "hml") ambiente = AmbienteDeploy.Hml;
            else if (promocao && entrada.Env == "production") ambiente = AmbienteDeploy.Producao;
            else return NaoSuportado;

            // 4. Entrega aberta e Fluxo dela, do Projeto do token.
            var projetoId = await projetosPorCaminho.ObterIdAtivoAsync(entrada.Projeto, cancellationToken);
            var entrega = projetoId is null ? null : await entregas.ObterPorPublicIdAsync(entrada.EntregaId, cancellationToken);
            var fluxo = entrega?.Fluxos.FirstOrDefault(f => f.PublicId == entrada.FluxoId);
            if (entrega is null || entrega.Situacao != SituacaoEntrega.Aberta || fluxo is null || fluxo.ProjetoId != projetoId)
                return SemEntrega;

            // 5. Ambiente habilitado.
            var cadastro = await projetos.ObterAmbienteAsync(fluxo.ProjetoId, ambiente, cancellationToken);
            if (cadastro is null || !cadastro.Habilitado)
                return Desabilitado;

            // 6. SHA.
            if (promocao)
            {
                if (fluxo.Sha is null) return SemArtefato;
                if (fluxo.Sha != entrada.Sha) return ShaDiferente;
            }
            else
            {
                var registro = fluxo.RegistrarSha(entrada.Sha);
                if (!registro.IsSuccess) return Result<AbrirPipelineResposta>.Falha(registro.Errors.ToArray());
            }

            // 7. Atalho (só `fluxo` em production; a promocao nunca o honra).
            var etapasLigadas = opcoes?.E1bLigada == true;
            if (fluxoProcesso && ambiente == AmbienteDeploy.Producao)
            {
                if (string.IsNullOrWhiteSpace(entrada.AtalhoPor) || string.IsNullOrWhiteSpace(entrada.AtalhoMotivo))
                    return AtalhoObrigatorio;
                // E1b: o texto vira PuladaPor/PuladaMotivo (200/1000); acima disso recusa antes de gravar qualquer coisa.
                if (etapasLigadas && (entrada.AtalhoPor.Length > 200 || entrada.AtalhoMotivo.Length > 1000))
                    return CorpoInvalido;
            }

            // E1b: a Etapa muda na mesma gravação do SHA e da ocupação (nunca recusa a chamada: transição fora da tabela é no-op).
            var mudancas = etapasLigadas ? AplicarEtapas(fluxo, entrada, ambiente, relogio.GetUtcNow().UtcDateTime) : [];
            var gravou = true;

            // 8. Ocupação (hml e production). Grava na hora, junto com o SHA pendente.
            if (ambiente != AmbienteDeploy.Qa)
            {
                var quando = relogio.GetUtcNow().UtcDateTime;
                Result reserva;
                try
                {
                    reserva = await ocupacoes.ReservarAsync(fluxo.ProjetoId, ambiente, fluxo.Id, quando, cancellationToken);
                }
                catch (ConcorrenciaOtimistaException)
                {
                    // A gravação da reserva levou junto o Fluxo, que outra chamada já gravou: vale o SHA dela. Igual: reserva de novo.
                    // As mudanças pendentes (inclusive a Etapa) foram soltas: relê a Etapa do vencedor e reaplica o fato desta
                    // chamada (o pipeline_id não se perde); a nova reserva grava junto e o evento sai depois do commit.
                    var gravado = await entregas.ObterShaGravadoDoFluxoAsync(fluxo.Id, cancellationToken);
                    if (gravado != entrada.Sha) return ShaDiferente;
                    if (mudancas.Count > 0)
                    {
                        foreach (var e in mudancas)
                            await entregas.RecarregarAsync(e, cancellationToken);
                        mudancas = AplicarEtapas(fluxo, entrada, ambiente, relogio.GetUtcNow().UtcDateTime);
                    }
                    reserva = await ocupacoes.ReservarAsync(fluxo.ProjetoId, ambiente, fluxo.Id, quando, cancellationToken);
                }
                if (!reserva.IsSuccess) return Result<AbrirPipelineResposta>.Falha(reserva.Errors.ToArray());
            }

            // A promoção só mexe na Etapa (o SHA já estava gravado): se a reserva não gravou, grava agora.
            if (fluxoProcesso || (gravou && mudancas.Count > 0))
            {
                try
                {
                    await unitOfWork.SaveChangesAsync();
                }
                catch (ConcorrenciaOtimistaException)
                {
                    // Outra chamada gravou o Fluxo (ou a Etapa) entre a leitura e a gravação: vale o SHA que ela gravou.
                    gravou = false;
                    var gravado = await entregas.ObterShaGravadoDoFluxoAsync(fluxo.Id, cancellationToken);
                    if (fluxoProcesso && gravado != entrada.Sha) return ShaDiferente;
                    log?.LogInformation("esteira-abrir: conflito ao gravar a Etapa; relê o vencedor e reaplica");
                    // E1b.3: o webhook pode ter gravado a Etapa primeiro. Relê o estado dele e reaplica o fato desta chamada
                    // (o pipeline_id novo não se perde); segundo conflito: o vencedor prevalece, sem erro.
                    if (mudancas.Count > 0)
                    {
                        foreach (var e in mudancas)
                            await entregas.RecarregarAsync(e, cancellationToken);
                        mudancas = AplicarEtapas(fluxo, entrada, ambiente, relogio.GetUtcNow().UtcDateTime);
                        try
                        {
                            await unitOfWork.SaveChangesAsync();
                            gravou = true;
                        }
                        catch (ConcorrenciaOtimistaException)
                        {
                            log?.LogInformation("esteira-abrir: segundo conflito na Etapa; o estado do vencedor prevalece");
                        }
                    }
                }
            }

            if (gravou && mudancas.Count > 0)
                await PublicarAsync(entrega, fluxo, mudancas, cancellationToken);

            // 9. Resposta.
            var lista = await destinos.ListarAsync(fluxo.ProjetoId, ambiente, cancellationToken);
            return Result<AbrirPipelineResposta>.Sucesso(new AbrirPipelineResposta(fluxo.FeatureFlags(ambiente), lista));
        }

        // Tabela de fatos do `esteira-abrir` (spec E1b). Devolve só as Etapas que de fato mudaram.
        private List<EtapaFluxoEntity> AplicarEtapas(FluxoEntity fluxo, AbrirPipelineEntrada entrada, AmbienteDeploy ambiente, DateTime em)
        {
            var mudadas = new List<EtapaFluxoEntity>();
            long? pipelineId = long.TryParse(entrada.PipelineId, out var id) && id > 0 ? id : null;

            void Aplicar(EtapaDoFluxo etapa, Func<EtapaFluxoEntity, Result> transicao)
            {
                var e = fluxo.Etapa(etapa);
                var r = transicao(e);
                if (r.IsSuccess)
                    mudadas.Add(e);
                else
                    log?.LogInformation("esteira-abrir: transição da Etapa {Etapa} fora da tabela (no-op): {Codigo}", EnumKebab.Texto(etapa), r.Errors.FirstOrDefault()?.Code);
            }

            if (entrada.Process == "fluxo" && ambiente == AmbienteDeploy.Producao)
            {
                Aplicar(EtapaDoFluxo.Qa, e => e.Pular(entrada.AtalhoPor!, entrada.AtalhoMotivo!, em));
                Aplicar(EtapaDoFluxo.Hml, e => e.Pular(entrada.AtalhoPor!, entrada.AtalhoMotivo!, em));
                Aplicar(EtapaDoFluxo.Prd, e => e.IniciarExecucao(pipelineId, em));
            }
            else
            {
                var etapa = ambiente switch { AmbienteDeploy.Qa => EtapaDoFluxo.Qa, AmbienteDeploy.Hml => EtapaDoFluxo.Hml, _ => EtapaDoFluxo.Prd };
                Aplicar(etapa, e => e.IniciarExecucao(pipelineId, em));
            }
            return mudadas;
        }

        // Depois do commit. Falha do tempo real nunca derruba o esteira-abrir (o front recarrega ao reconectar).
        private async Task PublicarAsync(EntregaEntity entrega, FluxoEntity fluxo, List<EtapaFluxoEntity> mudancas, CancellationToken ct)
        {
            if (eventos is null || acesso is null)
                return;
            try
            {
                var aplicacao = await acesso.ObterPublicIdAsync(entrega.AplicacaoId, ct);
                if (aplicacao is null)
                    return;
                foreach (var e in mudancas)
                    await eventos.PublicarAsync(new EntregaAtualizada(aplicacao.Value, entrega.PublicId, fluxo.PublicId, EnumKebab.Texto(e.Etapa), EnumKebab.Texto(e.Estado)), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log?.LogWarning(ex, "esteira-abrir: falha ao publicar entrega.atualizada");
            }
        }
    }
}
