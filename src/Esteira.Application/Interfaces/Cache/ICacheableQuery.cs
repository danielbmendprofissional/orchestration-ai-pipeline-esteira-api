namespace Esteira.Application.Interfaces.Cache
{
    public interface ICacheableQuery
    {
        string ChaveCache { get; }
        TimeSpan DuracaoCache { get; }
    }
}
