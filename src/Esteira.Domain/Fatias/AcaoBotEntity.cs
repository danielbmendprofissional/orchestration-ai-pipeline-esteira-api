using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Fatias
{
    // Persistidos como texto em kebab-case (abrir-review, aprovar, reprovar / pedida, executando...).
    public enum TipoAcaoBot { AbrirReview, Aprovar, Reprovar }

    public enum EstadoAcaoBot { Pedida, Executando, Confirmada, Falhou, Cancelada }

    // Ação do bot (ADR-0010; spec P2 "Dados"): a decisão do humano e o pedido da escrita no GitLab são a mesma linha.
    // pedida -> executando -> confirmada | falhou; pedida -> cancelada; nunca volta.
    public sealed class AcaoBotEntity : EntityBase
    {
        public const int ComentarioMaximo = 2000;

        private AcaoBotEntity() { }

        public long AplicacaoId { get; private set; }
        public string FatiaCodigo { get; private set; } = default!;
        public long ProjetoId { get; private set; }
        public long MrIid { get; private set; }
        public TipoAcaoBot Tipo { get; private set; }
        public EstadoAcaoBot Estado { get; private set; }
        // <aplicação>:<fatia>:<tipo>:<projeto>:<mr>:<tentativa>; única no banco.
        public string ChaveIdempotencia { get; private set; } = default!;
        public long? DependeDeAcaoId { get; private set; }
        public long PedidaPorPessoaId { get; private set; }
        public string? Comentario { get; private set; }
        public DateTime PedidaEm { get; private set; }
        public DateTime AtualizadaEm { get; private set; }
        public string? CodigoErro { get; private set; }

        public static string MontarChave(long aplicacaoId, string fatiaCodigo, TipoAcaoBot tipo, long projetoId, long mrIid, int tentativa)
            => $"{aplicacaoId}:{fatiaCodigo}:{NomeDoTipo(tipo)}:{projetoId}:{mrIid}:{tentativa}";

        public static Result<AcaoBotEntity> Criar(
            long aplicacaoId, string fatiaCodigo, long projetoId, long mrIid, TipoAcaoBot tipo, int tentativa,
            long? dependeDeAcaoId, long pedidaPorPessoaId, string? comentario, DateTime pedidaEm)
        {
            Guard.AgainstNullOrWhiteSpace(fatiaCodigo, nameof(fatiaCodigo));
            Guard.AgainstInvalidEnum<TipoAcaoBot>(tipo, nameof(tipo));

            var invalido = tipo == TipoAcaoBot.Reprovar
                ? string.IsNullOrWhiteSpace(comentario) || comentario.Length > ComentarioMaximo
                : comentario is not null;
            if (invalido)
                return new Erro("fatia.comentario-invalido",
                    "O comentário é obrigatório (1 a 2000 caracteres) ao reprovar e não existe nas outras ações.", TipoErro.Validation);

            return Result<AcaoBotEntity>.Sucesso(new AcaoBotEntity
            {
                AplicacaoId = aplicacaoId,
                FatiaCodigo = fatiaCodigo,
                ProjetoId = projetoId,
                MrIid = mrIid,
                Tipo = tipo,
                Estado = EstadoAcaoBot.Pedida,
                ChaveIdempotencia = MontarChave(aplicacaoId, fatiaCodigo, tipo, projetoId, mrIid, tentativa),
                DependeDeAcaoId = dependeDeAcaoId,
                PedidaPorPessoaId = pedidaPorPessoaId,
                Comentario = comentario,
                PedidaEm = pedidaEm,
                AtualizadaEm = pedidaEm
            });
        }

        public Result Iniciar(DateTime quando) => Transitar(EstadoAcaoBot.Pedida, EstadoAcaoBot.Executando, quando, null);

        public Result Confirmar(DateTime quando) => Transitar(EstadoAcaoBot.Executando, EstadoAcaoBot.Confirmada, quando, null);

        public Result Falhar(string? codigoErro, DateTime quando) => Transitar(EstadoAcaoBot.Executando, EstadoAcaoBot.Falhou, quando, codigoErro);

        public Result Cancelar(DateTime quando) => Transitar(EstadoAcaoBot.Pedida, EstadoAcaoBot.Cancelada, quando, null);

        private Result Transitar(EstadoAcaoBot de, EstadoAcaoBot para, DateTime quando, string? codigoErro)
        {
            if (Estado != de)
                return Result.Falha(new Erro("acao-bot.transicao-invalida",
                    $"A ação não pode ir de {Estado} para {para}.", TipoErro.Conflict));

            Estado = para;
            AtualizadaEm = quando;
            CodigoErro = codigoErro;
            return Result.Sucesso();
        }

        private static string NomeDoTipo(TipoAcaoBot tipo) => tipo switch
        {
            TipoAcaoBot.AbrirReview => "abrir-review",
            TipoAcaoBot.Aprovar => "aprovar",
            TipoAcaoBot.Reprovar => "reprovar",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
