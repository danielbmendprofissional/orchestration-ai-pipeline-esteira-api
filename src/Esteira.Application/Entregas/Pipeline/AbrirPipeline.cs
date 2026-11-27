using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;

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
        TimeProvider relogio) : IAbrirPipeline
    {
        private static readonly Erro NaoSuportado = new("pipeline.processo-nao-suportado", "O processo não é suportado por esta versão da Esteira.", TipoErro.Failure);
        private static readonly Erro SemEntrega = new("pipeline.sem-entrega-aberta", "Não há Entrega aberta para este Fluxo.", TipoErro.Conflict);
        private static readonly Erro Desabilitado = new("ambiente.desabilitado", "O Ambiente não está habilitado no Projeto.", TipoErro.Failure);
        private static readonly Erro SemArtefato = new("pipeline.sem-artefato", "O Fluxo ainda não tem Artefato registrado.", TipoErro.Conflict);
        private static readonly Erro ShaDiferente = new("pipeline.sha-diferente", "O SHA difere do registrado no Fluxo.", TipoErro.Conflict);
        private static readonly Erro AtalhoObrigatorio = new("pipeline.atalho-obrigatorio", "O Atalho exige atalho_por e atalho_motivo.", TipoErro.Failure);

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
            if (fluxoProcesso && ambiente == AmbienteDeploy.Producao
                && (string.IsNullOrWhiteSpace(entrada.AtalhoPor) || string.IsNullOrWhiteSpace(entrada.AtalhoMotivo)))
                return AtalhoObrigatorio;

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
                    var gravado = await entregas.ObterShaGravadoDoFluxoAsync(fluxo.Id, cancellationToken);
                    if (gravado != entrada.Sha) return ShaDiferente;
                    reserva = await ocupacoes.ReservarAsync(fluxo.ProjetoId, ambiente, fluxo.Id, quando, cancellationToken);
                }
                if (!reserva.IsSuccess) return Result<AbrirPipelineResposta>.Falha(reserva.Errors.ToArray());
            }

            if (fluxoProcesso)
            {
                try
                {
                    await unitOfWork.SaveChangesAsync();
                }
                catch (ConcorrenciaOtimistaException)
                {
                    // Outra chamada gravou o Fluxo entre a leitura e a gravação: vale o SHA que ela gravou.
                    var gravado = await entregas.ObterShaGravadoDoFluxoAsync(fluxo.Id, cancellationToken);
                    if (gravado != entrada.Sha) return ShaDiferente;
                }
            }

            // 9. Resposta.
            var lista = await destinos.ListarAsync(fluxo.ProjetoId, ambiente, cancellationToken);
            return Result<AbrirPipelineResposta>.Sucesso(new AbrirPipelineResposta(fluxo.FeatureFlags(ambiente), lista));
        }
    }
}
