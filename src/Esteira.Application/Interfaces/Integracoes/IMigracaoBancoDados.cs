namespace Esteira.Application.Interfaces.Integracoes
{
    // Porta reservada para a ferramenta de migração automática de banco de dados.
    // Ainda sem uso: implementar o adaptador em Esteira.AcL e ligar à etapa de banco de dados quando a ferramenta for definida.
    public interface IMigracaoBancoDados
    {
        Task<IReadOnlyList<string>> ObterScriptsAsync(string projeto, string versao, CancellationToken cancellationToken = default);
    }
}
