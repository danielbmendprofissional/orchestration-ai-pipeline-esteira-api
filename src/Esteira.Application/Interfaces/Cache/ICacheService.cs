namespace Esteira.Application.Interfaces.Cache
{
    public interface ICacheService
    {
        Task<T> ObterOuCriarAsync<T>(string chave, Func<Task<T>> fabrica, TimeSpan duracao, Func<T, bool>? deveGuardar = null);
        void Remover(string chave);
        void RemoverPorPrefixo(string prefixo);
    }
}
