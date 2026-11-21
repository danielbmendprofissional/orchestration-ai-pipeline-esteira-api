namespace Esteira.Application.Interfaces.UnitOfWork
{
    // Violação de índice único (Postgres 23505) vinda do banco, traduzida pela Data. É a rede de segurança da
    // corrida: a verificação prévia não pega duas requisições simultâneas. O handler a converte em Result.
    public sealed class ViolacaoDeUnicidadeException(string? restricao, Exception? inner = null)
        : Exception($"Violação de unicidade: {restricao}", inner)
    {
        public string? Restricao { get; } = restricao;
    }
}
