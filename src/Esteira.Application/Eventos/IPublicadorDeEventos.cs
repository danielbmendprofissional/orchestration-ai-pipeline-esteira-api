namespace Esteira.Application.Eventos
{
    // Porta de saída do tempo real: publica para todas as instâncias da API (Redis pub/sub no adaptador).
    // Chamar SEMPRE depois do commit. Falha de transporte não pode derrubar quem publica: o adaptador loga e segue
    // (sem replay, o front recarrega ao reconectar e a consulta periódica é a rede de segurança).
    public interface IPublicadorDeEventos
    {
        Task PublicarAsync(EventoDeTempoReal evento, CancellationToken cancellationToken = default);
    }
}
