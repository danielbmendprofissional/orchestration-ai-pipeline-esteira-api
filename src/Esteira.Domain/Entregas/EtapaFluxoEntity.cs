using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Entregas
{
    // Persistidos como texto em kebab-case (abertura, nao-iniciado). O enum cresce com as fatias que decidem
    // os demais estados (bloqueada, cancelado: E2/E3/E6).
    public enum EtapaDoFluxo { Abertura, Qa, Hml, Prd }

    public enum EstadoEtapa { NaoIniciado, Executando, AguardandoAprovacao, Concluido, Pulada, Falhou, Bloqueada }

    // Uma Etapa de um Fluxo (E1b). Só os métodos nomeados abaixo mudam o Estado, e recusam transição fora da tabela
    // (spec 02-tela-da-entrega.md, "Backend"). Quem chama (esteira-abrir, webhook) trata a recusa como no-op: a Etapa é só exibição.
    // E2/E3/E4 gravam novos estados pelo mesmo padrão, sem mudar o modelo.
    public sealed class EtapaFluxoEntity : EntityBase
    {
        private EtapaFluxoEntity() { }

        internal EtapaFluxoEntity(EtapaDoFluxo etapa, EstadoEtapa estado, DateTime em)
        {
            Etapa = etapa;
            Estado = estado;
            UltimoEventoEm = em;
            AtualizadaEm = em;
        }

        public long FluxoId { get; private set; }
        public EtapaDoFluxo Etapa { get; private set; }
        public EstadoEtapa Estado { get; private set; }
        // Nulo até o esteira-abrir informar o pipeline_id; o webhook só casa por ele.
        public long? PipelineGitLabId { get; private set; }
        public string? CodigoFalha { get; private set; }
        public string? PuladaPor { get; private set; }
        public string? PuladaMotivo { get; private set; }
        // Data do último evento aplicado: evento mais velho que isto é descartado.
        public DateTime UltimoEventoEm { get; private set; }
        public DateTime AtualizadaEm { get; private set; }

        // esteira-abrir aceito: nao-iniciado, falhou, bloqueada (reexecucao depois da correcao), executando ou aguardando-aprovacao -> executando.
        public Result IniciarExecucao(long? pipelineId, DateTime em)
        {
            if (Estado is not (EstadoEtapa.NaoIniciado or EstadoEtapa.Falhou or EstadoEtapa.Bloqueada or EstadoEtapa.Executando or EstadoEtapa.AguardandoAprovacao))
                return TransicaoInvalida();

            Estado = EstadoEtapa.Executando;
            PipelineGitLabId = pipelineId;
            CodigoFalha = null;
            Marcar(em);
            return Result.Sucesso();
        }

        // Gate/resultado (E2): executando ou aguardando-aprovacao -> bloqueada, guardando o motivo em CodigoFalha. Fora disso,
        // transicao invalida (no-op para quem chama). O webhook nao tira a Etapa daqui: so a reexecucao (IniciarExecucao).
        public Result Bloquear(string motivo, DateTime em)
        {
            if (Estado is not (EstadoEtapa.Executando or EstadoEtapa.AguardandoAprovacao))
                return TransicaoInvalida();

            Estado = EstadoEtapa.Bloqueada;
            CodigoFalha = motivo;
            Marcar(em);
            return Result.Sucesso();
        }

        // Atalho: só a partir de nao-iniciado.
        public Result Pular(string por, string motivo, DateTime em)
        {
            if (Estado != EstadoEtapa.NaoIniciado)
                return TransicaoInvalida();

            Estado = EstadoEtapa.Pulada;
            PuladaPor = por;
            PuladaMotivo = motivo;
            Marcar(em);
            return Result.Sucesso();
        }

        // Webhook: status da pipeline guardada na Etapa. Só vale com a Etapa executando e com evento posterior ao último aplicado.
        public Result AplicarStatusDaPipeline(string? status, DateTime eventoEm)
        {
            if (eventoEm <= UltimoEventoEm)
                return Result.Falha(new Erro("etapa.evento-antigo", "O evento é mais antigo que o último aplicado à Etapa.", TipoErro.Conflict));

            (EstadoEtapa? Estado, string? Codigo) alvo = (Etapa, status) switch
            {
                (EtapaDoFluxo.Qa or EtapaDoFluxo.Hml, "success") => (EstadoEtapa.AguardandoAprovacao, null),
                (EtapaDoFluxo.Prd, "success") => (EstadoEtapa.Concluido, null),
                (EtapaDoFluxo.Qa or EtapaDoFluxo.Hml or EtapaDoFluxo.Prd, "failed") => (EstadoEtapa.Falhou, "pipeline.falhou"),
                (EtapaDoFluxo.Qa or EtapaDoFluxo.Hml or EtapaDoFluxo.Prd, "canceled") => (EstadoEtapa.Falhou, "pipeline.cancelada"),
                _ => (null, null)
            };
            var (novo, codigo) = alvo;

            // created/pending/running/outros: sem mudança (e sem avançar o relógio do último evento).
            if (novo is null)
                return Estado == EstadoEtapa.Executando ? Result.Sucesso() : TransicaoInvalida();

            if (Estado != EstadoEtapa.Executando)
                return TransicaoInvalida();

            Estado = novo.Value;
            CodigoFalha = codigo;
            Marcar(eventoEm);
            return Result.Sucesso();
        }

        private void Marcar(DateTime em)
        {
            UltimoEventoEm = em;
            AtualizadaEm = em;
        }

        private static Result TransicaoInvalida()
            => Result.Falha(new Erro("etapa.transicao-invalida", "A transição não está prevista para o estado atual da Etapa.", TipoErro.Conflict));
    }
}
