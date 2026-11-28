namespace Esteira.Application.Eventos
{
    // Eventos do tempo real (ADR-0011, T1). Só identificadores públicos (GUID), código da Fatia e estado/status: nunca
    // comentário, título de MR ou texto vindo do GitLab; quem precisa do detalhe reconsulta a rota autorizada.
    // `AplicacaoId` roteia o evento no fan-out (cada stream só recebe as Aplicações que a pessoa pode ver).
    public abstract record EventoDeTempoReal(string Tipo, Guid AplicacaoId);

    public static class TiposDeEvento
    {
        public const string AcaoAtualizada = "acao.atualizada";
        public const string PipelineAtualizada = "pipeline.atualizada";
        public const string FatiasMudaram = "fatias.mudaram";
        public const string EntregaAtualizada = "entrega.atualizada";
    }

    // E1b: uma Etapa do Fluxo mudou de estado. `Etapa` e `Estado` em kebab-case (qa, aguardando-aprovacao); o front recarrega a Entrega.
    public sealed record EntregaAtualizada(Guid AplicacaoId, Guid EntregaId, Guid FluxoId, string Etapa, string Estado)
        : EventoDeTempoReal(TiposDeEvento.EntregaAtualizada, AplicacaoId);

    // `Estado` em kebab-case (pedida, executando, confirmada, falhou, cancelada).
    public sealed record AcaoAtualizada(Guid AplicacaoId, string FatiaCodigo, Guid AcaoId, string Estado)
        : EventoDeTempoReal(TiposDeEvento.AcaoAtualizada, AplicacaoId);

    // `PipelineId` é o id da pipeline no GitLab; `Status` o status bruto dela.
    public sealed record PipelineAtualizada(Guid AplicacaoId, Guid ProjetoId, long PipelineId, string Status, string Ref, string Sha)
        : EventoDeTempoReal(TiposDeEvento.PipelineAtualizada, AplicacaoId);

    // `FatiaCodigo` nulo = a mudança não é de uma Fatia identificável (recarrega a lista).
    public sealed record FatiasMudaram(Guid AplicacaoId, string? FatiaCodigo)
        : EventoDeTempoReal(TiposDeEvento.FatiasMudaram, AplicacaoId);
}
